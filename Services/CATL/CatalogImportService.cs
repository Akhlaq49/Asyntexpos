using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// One importer for a company's files: package items (the products to suggest and their quantity per building size -
/// matched to the inventory products, whose price and description are used) and categorization. (The price-list
/// import is kept for the API but is not offered in the app: prices come from the inventory.) The browser reads the sheet
/// and sends its rows; where the columns are comes from the company's import profile (CatalogImportProfiles), never
/// from code. Names are matched model code -> exact product name -> alias, in this company only; a name that
/// matches nothing is never guessed - it waits in CatalogUnmatched for a person to map it (saved as an alias).
/// Each file is imported in one transaction. Source data is never "fixed" - problems are listed in the report.
/// </summary>
public class CatalogImportService
{
    public const string Categorization = "categorization", Packages = "packages";
    public static readonly string[] Kinds = { Categorization, Packages };

    private readonly AppDbContext _db;
    private readonly CatalogService _catalog;
    public CatalogImportService(AppDbContext db, CatalogService catalog) { _db = db; _catalog = catalog; }

    private static string Key(string? s) => CatalogService.Key(s);

    // ── import profiles ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>Default layout: header names to look for (any of them, case and spacing ignored)</summary>
    public static JsonObject DefaultProfile(string kind) => kind switch
    {
        Categorization => new JsonObject
        {
            ["headers"] = new JsonObject
            {
                ["name"] = new JsonArray("product name", "name", "product"),
                // tier code -> its tick column
                ["tiers"] = new JsonObject
                {
                    [CatalogService.Standard] = new JsonArray("standard solution", "standard"),
                    [CatalogService.Premium] = new JsonArray("premium solution", "premium"),
                },
            },
        },
        _ => new JsonObject
        {
            ["headers"] = new JsonObject
            {
                ["name"] = new JsonArray("product", "product name", "name"),
            },
            // a size column's header starts with the package size name ("1 BHK Automation" -> 1 BHK); these words are
            // dropped when a company without package sizes gets them from the header; other columns are ignored
            // (prices are never read from this file: they come from the inventory)
            ["sizeHeaderDropWords"] = new JsonArray("automation", "solution", "package"),
            ["ignoreHeaders"] = new JsonArray("price", "unit price", "price per unit", "total", "sn", "s no", "sr", "description"),
        },
    };

    public async Task<JsonObject> ProfileAsync(string kind, CancellationToken ct = default)
    {
        var saved = await _db.CatalogImportProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Kind == kind, ct);
        return saved != null && JsonNode.Parse(saved.ProfileJson) is JsonObject o ? o : DefaultProfile(kind);
    }

    public async Task SaveProfileAsync(string kind, JsonObject profile, CancellationToken ct = default)
    {
        var saved = await _db.CatalogImportProfiles.FirstOrDefaultAsync(p => p.Kind == kind, ct);
        if (saved == null) _db.CatalogImportProfiles.Add(new CatalogImportProfile { Kind = kind, ProfileJson = profile.ToJsonString() });
        else saved.ProfileJson = profile.ToJsonString();
        await _db.SaveChangesAsync(ct);
    }

    // ── reading rows ─────────────────────────────────────────────────────────────────────────────────────
    private static string Cell(IReadOnlyList<string?> row, int col) => col >= 0 && col < row.Count ? (row[col] ?? "").Trim() : "";

    private static decimal? Number(string s)
    {
        s = s.Replace(",", "").Replace("Rs", "", StringComparison.OrdinalIgnoreCase).Replace("PKR", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static List<string> Names(JsonNode? node) =>
        node is JsonArray a ? a.Select(x => Key(x?.ToString())).Where(x => x.Length > 0).ToList()
        : node is JsonValue v ? new List<string> { Key(v.ToString()) } : new List<string>();

    /// <summary>The header row: the first of the top 20 rows that has every required header</summary>
    private static (int Row, Dictionary<string, int> Cols) FindHeader(List<List<string?>> rows, Dictionary<string, List<string>> wanted, string[] required)
    {
        for (var r = 0; r < Math.Min(rows.Count, 20); r++)
        {
            var cells = rows[r].Select(c => Key(c)).ToList();
            var cols = new Dictionary<string, int>();
            foreach (var (field, names) in wanted)
            {
                var i = cells.FindIndex(c => names.Contains(c));
                if (i >= 0) cols[field] = i;
            }
            if (required.All(cols.ContainsKey)) return (r, cols);
        }
        throw new InvalidOperationException("The header row was not found. Expected columns: " +
            string.Join(", ", required.Select(f => $"{f} ({string.Join(" / ", wanted[f])})")) + ". Change the import profile if the file uses other names.");
    }

    // ── matching ─────────────────────────────────────────────────────────────────────────────────────────
    private sealed class Index
    {
        public Dictionary<string, CatalogProduct> ByName = new();
        public Dictionary<string, int> ByAlias = new();
        public List<CatalogProduct> All = new();
        public CatalogProduct? Find(string name)
        {
            var k = Key(name);
            if (ByName.TryGetValue(k, out var p)) return p;
            return ByAlias.TryGetValue(k, out var id) ? All.FirstOrDefault(x => x.Id == id) : null;
        }
        public void Add(CatalogProduct p) { All.Add(p); ByName.TryAdd(Key(p.Name), p); }
    }

    private async Task<Index> ProductIndexAsync(CancellationToken ct)
    {
        var index = new Index();
        foreach (var p in await _db.CatalogProducts.ToListAsync(ct)) index.Add(p);
        foreach (var a in await _db.CatalogAliases.Where(a => a.TargetKind == "product").ToListAsync(ct)) index.ByAlias.TryAdd(a.AliasKey, a.CatalogProductId!.Value);
        return index;
    }

    private async Task<SolutionProductLine?> FindFamilyAsync(string name, CancellationToken ct)
    {
        var k = Key(name);
        var families = await _db.SolutionProductLines.ToListAsync(ct);
        var family = families.FirstOrDefault(f => Key(f.Name) == k);
        if (family != null) return family;
        var alias = await _db.CatalogAliases.FirstOrDefaultAsync(a => a.TargetKind == "family" && a.AliasKey == k, ct);
        return alias == null ? null : families.FirstOrDefault(f => f.Id == alias.FamilyId);
    }

    private async Task HoldAsync(string kind, string name, JsonObject row, CancellationToken ct)
    {
        var k = Key(name);
        var held = await _db.CatalogUnmatched.FirstOrDefaultAsync(u => u.Kind == kind && u.NameKey == k, ct);
        if (held == null) _db.CatalogUnmatched.Add(new CatalogUnmatched { Kind = kind, Name = name.Trim(), NameKey = k, RowJson = row.ToJsonString() });
        else held.RowJson = row.ToJsonString();
    }

    private async Task AddAliasAsync(string alias, string source, int? productId, int? familyId, CancellationToken ct)
    {
        var k = Key(alias);
        var kind = productId != null ? "product" : "family";
        if (await _db.CatalogAliases.AnyAsync(a => a.TargetKind == kind && a.AliasKey == k, ct)) return;
        _db.CatalogAliases.Add(new CatalogAlias { Alias = alias.Trim(), AliasKey = k, Source = source, TargetKind = kind, CatalogProductId = productId, FamilyId = familyId });
    }

    private static decimal? Dec(JsonNode? n) => n is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : null;
    private static int? Int(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    // ── categorization ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>A row without ticks is a category, a row with ticks a family of the category above. Ticks set the
    /// family's tiers. A company without categorization gets it created; otherwise a family that matches no family
    /// name or alias waits to be mapped (a category name that matches none is only reported).</summary>
    public async Task<JsonObject> ImportCategorizationAsync(string fileName, List<List<string?>> rows, CancellationToken ct = default)
    {
        var profile = await ProfileAsync(Categorization, ct);
        var h = profile["headers"]!.AsObject();
        var tierHeaders = h["tiers"]!.AsObject().ToDictionary(x => x.Key, x => Names(x.Value));
        var wanted = new Dictionary<string, List<string>> { ["name"] = Names(h["name"]) };
        foreach (var (code, names) in tierHeaders) wanted["tier:" + code] = names;
        var (headerRow, cols) = FindHeader(rows, wanted, wanted.Keys.ToArray());
        var tiers = await _catalog.TiersAsync(ct);

        var report = new JsonObject { ["file"] = fileName, ["kind"] = Categorization };
        var fresh = !await _db.SolutionProductLines.AnyAsync(ct);
        int categories = 0, families = 0, created = 0, changed = 0, held = 0;
        var unknownCategories = new JsonArray();
        var changes = new JsonArray();
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        SolutionGroup? group = null;
        var groups = await _db.SolutionGroups.ToListAsync(ct);
        for (var r = headerRow + 1; r < rows.Count; r++)
        {
            var name = Cell(rows[r], cols["name"]);
            if (name.Length == 0) continue;
            var ticked = tierHeaders.Keys.Where(code => Cell(rows[r], cols["tier:" + code]).Length > 0).ToHashSet();
            if (ticked.Count == 0)
            {
                categories++;
                group = groups.FirstOrDefault(g => Key(g.Name) == Key(name));
                if (group == null && fresh)
                {
                    group = new SolutionGroup { Name = name, SortOrder = groups.Count + 1 };
                    _db.SolutionGroups.Add(group);
                    await _db.SaveChangesAsync(ct);
                    groups.Add(group);
                }
                else if (group == null) unknownCategories.Add($"{name} (row {r + 1}) - its families are matched by their own names");
                continue;
            }
            families++;
            var family = await FindFamilyAsync(name, ct);
            if (family == null && fresh && group != null)
            {
                family = new SolutionProductLine { GroupId = group.Id, Name = name, SortOrder = await _db.SolutionProductLines.CountAsync(l => l.GroupId == group.Id, ct) + 1 };
                _db.SolutionProductLines.Add(family);
                await _db.SaveChangesAsync(ct);
                created++;
            }
            if (family == null)
            {
                held++;
                await HoldAsync(Categorization, name, new JsonObject { ["category"] = group?.Name, ["groupId"] = group?.Id, ["tiers"] = new JsonArray(ticked.Select(t => (JsonNode)t).ToArray()) }, ct);
                continue;
            }
            if (await ApplyTiersAsync(family, ticked, tiers, ct)) { changed++; changes.Add($"{family.Name}: {string.Join(" + ", ticked)}"); }
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        report["categories"] = categories;
        report["families"] = families;
        report["familiesCreated"] = created;
        report["tiersChanged"] = changed;
        report["changes"] = changes;
        report["unmatched"] = held;
        report["unknownCategories"] = unknownCategories;
        return report;
    }

    /// <summary>Sets a family's tiers to the ticked ones; true when they changed</summary>
    private async Task<bool> ApplyTiersAsync(SolutionProductLine family, HashSet<string> tickedCodes, List<CatalogTier> tiers, CancellationToken ct)
    {
        var want = tiers.Where(t => tickedCodes.Contains(t.Code)).Select(t => t.Id).ToHashSet();
        var have = (await _db.CatalogFamilyTiers.Where(x => x.FamilyId == family.Id).Select(x => x.TierId).ToListAsync(ct)).ToHashSet();
        if (want.SetEquals(have)) return false;
        await _db.CatalogFamilyTiers.Where(x => x.FamilyId == family.Id).ExecuteDeleteAsync(ct);
        foreach (var id in want) _db.CatalogFamilyTiers.Add(new CatalogFamilyTier { FamilyId = family.Id, TierId = id });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── packages ─────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>One row per product, one quantity column per package size. The file has one quantity per size, so it
    /// goes to every tier the product's family allows. Unit price = the price of a product not on the price list.
    /// Re-importing replaces the quantities (an empty cell removes the line).</summary>
    public async Task<JsonObject> ImportPackagesAsync(string fileName, List<List<string?>> rows, CancellationToken ct = default)
    {
        var profile = await ProfileAsync(Packages, ct);
        var h = profile["headers"]!.AsObject();
        var wanted = h.ToDictionary(x => x.Key, x => Names(x.Value));
        var (headerRow, cols) = FindHeader(rows, wanted, new[] { "name" });
        int col(string f) => cols.TryGetValue(f, out var c) ? c : -1;
        var drop = Names(profile["sizeHeaderDropWords"]);
        var ignore = Names(profile["ignoreHeaders"]).ToHashSet();
        foreach (var n in new[] { "price", "unit price", "price per unit" }) ignore.Add(n);

        // every inventory product is a SKU of a catalogue product first: the file's names are matched to them
        await _catalog.SyncSkusAsync(ct);
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        // the file is the whole list: names held from an earlier file are dropped
        _db.CatalogUnmatched.RemoveRange(await _db.CatalogUnmatched.Where(u => u.Kind == Packages).ToListAsync(ct));
        // size columns: a header that starts with a package size's name; a company without sizes gets them from the headers
        var sizes = await _db.PackageSizes.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        var header = rows[headerRow].Select(c => Key(c)).ToList();
        var createSizes = sizes.Count == 0;
        var sizeCols = new Dictionary<int, int>();   // column -> size id
        var newSizes = new JsonArray();
        for (var c = 0; c < header.Count; c++)
        {
            if (c == col("name") || c == col("price") || header[c].Length == 0 || ignore.Contains(header[c])) continue;
            var size = sizes.Where(s => header[c] == Key(s.Name) || header[c].StartsWith(Key(s.Name) + " ")).OrderByDescending(s => s.Name.Length).FirstOrDefault();
            if (size == null && createSizes)
            {
                var text = (rows[headerRow][c] ?? "").Replace("\n", " ");
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !drop.Contains(Key(w))).ToArray();
                size = new PackageSize { Name = string.Join(' ', words), SortOrder = sizes.Count + 1 };
                _db.PackageSizes.Add(size);
                await _db.SaveChangesAsync(ct);
                sizes.Add(size);
                newSizes.Add(size.Name);
            }
            if (size != null && !sizeCols.ContainsValue(size.Id)) sizeCols[c] = size.Id;
        }
        if (sizeCols.Count == 0) throw new InvalidOperationException("No package size columns were found (a column whose header starts with a package size name, e.g. \"10 Marla\").");

        var tiers = await _catalog.TiersAsync(ct);
        var familyTiers = (await _db.CatalogFamilyTiers.ToListAsync(ct)).ToLookup(x => x.FamilyId, x => x.TierId);
        var index = await ProductIndexAsync(ct);
        var skuProducts = (await _db.Products.Where(p => p.CatalogProductId != null).Select(p => p.CatalogProductId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        int matched = 0, held = 0, lines = 0, removed = 0;
        var noFamily = new JsonArray();
        var notInInventory = new JsonArray();
        var inFile = new HashSet<int>();
        var rowsRead = 0;
        for (var r = headerRow + 1; r < rows.Count; r++)
        {
            var name = Cell(rows[r], col("name"));
            if (name.Length == 0) continue;
            rowsRead++;
            var qty = new JsonObject();
            foreach (var (c, sizeId) in sizeCols)
                if (Number(Cell(rows[r], c)) is decimal q && q > 0) qty[sizeId.ToString()] = q;
            var row = new JsonObject { ["quantities"] = qty, ["line"] = r + 1 };
            var product = index.Find(name);
            if (product == null)
            {
                held++;
                await HoldAsync(Packages, name, row, ct);
                continue;
            }
            matched++;
            inFile.Add(product.Id);
            if (!skuProducts.Contains(product.Id)) notInInventory.Add(product.Name);
            if (product.FamilyId == null)
            {
                // its family is chosen on the Import tab (the product is already picked); the row is applied then
                held++;
                noFamily.Add(product.Name);
                row["productId"] = product.Id;
                await HoldAsync(Packages, name, row, ct);
                continue;
            }
            var (added, gone) = await ApplyPackageRowAsync(product, row, tiers, familyTiers, ct);
            lines += added; removed += gone;
        }
        // products not in the file are no longer suggested: their package lines go
        var dropped = await _db.CatalogPackageLines.Where(l => !inFile.Contains(l.CatalogProductId)).ToListAsync(ct);
        var droppedIds = dropped.Select(l => l.CatalogProductId).Distinct().ToList();
        var droppedNames = await _db.CatalogProducts.Where(p => droppedIds.Contains(p.Id)).Select(p => p.Name).ToListAsync(ct);
        _db.CatalogPackageLines.RemoveRange(dropped);
        removed += dropped.Count;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var conflicts = await _db.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(`PackageName`, ' / ', `TierName`, ': ', `ProductName`) AS `Value` FROM `" + DbNames.CatlViewTrcf + "` WHERE `TenantId` = {0}",
            _db.CurrentTenantId).ToListAsync(ct);
        return new JsonObject
        {
            ["file"] = fileName, ["kind"] = Packages, ["rows"] = rowsRead, ["matched"] = matched, ["unmatched"] = held,
            ["packageSizes"] = new JsonArray(sizeCols.Values.Select(id => (JsonNode)sizes.First(s => s.Id == id).Name).ToArray()),
            ["newPackageSizes"] = newSizes,
            ["linesWritten"] = lines, ["linesRemoved"] = removed,
            ["noFamily"] = noFamily, ["notInInventory"] = notInInventory,
            ["productsRemoved"] = new JsonArray(droppedNames.OrderBy(n => n).Select(n => (JsonNode)n).ToArray()),
            ["tierConflicts"] = new JsonArray(conflicts.Select(c => (JsonNode)c).ToArray()),
        };
    }

    /// <summary>Writes one package-items row for a product: quantities to every tier its family allows (an empty
    /// size removes the line; lines of tiers the family left are removed). Returns lines written / removed.</summary>
    private async Task<(int Added, int Removed)> ApplyPackageRowAsync(CatalogProduct product, JsonObject row, List<CatalogTier> tiers,
        ILookup<int, int> familyTiers, CancellationToken ct)
    {
        if (product.FamilyId == null) return (0, 0);
        var tierIds = familyTiers[product.FamilyId.Value].ToList();
        var qty = row["quantities"]!.AsObject().Where(x => Dec(x.Value) > 0).ToDictionary(x => int.Parse(x.Key), x => Dec(x.Value)!.Value);
        var existing = await _db.CatalogPackageLines.Where(l => l.CatalogProductId == product.Id).ToListAsync(ct);
        int added = 0, removed = 0;
        foreach (var l in existing.Where(l => !qty.ContainsKey(l.PackageSizeId) || !tierIds.Contains(l.TierId))) { _db.CatalogPackageLines.Remove(l); removed++; }
        foreach (var tierId in tierIds)
            foreach (var (sizeId, q) in qty)
            {
                var line = existing.FirstOrDefault(l => l.TierId == tierId && l.PackageSizeId == sizeId);
                if (line == null) _db.CatalogPackageLines.Add(new CatalogPackageLine { TierId = tierId, PackageSizeId = sizeId, CatalogProductId = product.Id, Quantity = q });
                else line.Quantity = q;
                added++;
            }
        await _db.SaveChangesAsync(ct);
        return (added, removed);
    }

    // ── names waiting to be mapped ───────────────────────────────────────────────────────────────────────
    /// <summary>packages name: CatalogProductId (map) or Create; NewFamilyId = the product's family.
    /// categorization name: FamilyId (map) or Create under GroupId.</summary>
    public record MapDto(int? CatalogProductId, int? FamilyId, bool Create, int? GroupId, int? NewFamilyId);

    /// <summary>A person maps a held name: to an existing product / family (saved as an alias, so the next import
    /// matches it), or creates it. The held row is then applied.</summary>
    public async Task<object> MapAsync(int unmatchedId, MapDto dto, CancellationToken ct = default)
    {
        var held = await _db.CatalogUnmatched.FirstOrDefaultAsync(u => u.Id == unmatchedId, ct)
            ?? throw new KeyNotFoundException("This name was already handled.");
        var row = JsonNode.Parse(held.RowJson)!.AsObject();
        var tiers = await _catalog.TiersAsync(ct);
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (held.Kind == Packages)
        {
            CatalogProduct? product;
            if (dto.Create)
            {
                if (await _db.CatalogProducts.AnyAsync(p => p.Name == held.Name, ct)) throw new InvalidOperationException($"A product named {held.Name} already exists - map to it instead.");
                if (dto.NewFamilyId != null && !await _db.SolutionProductLines.AnyAsync(f => f.Id == dto.NewFamilyId, ct)) throw new KeyNotFoundException("Family not found.");
                product = new CatalogProduct { Name = held.Name, FamilyId = dto.NewFamilyId };
                _db.CatalogProducts.Add(product);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                product = await _db.CatalogProducts.FirstOrDefaultAsync(p => p.Id == dto.CatalogProductId, ct) ?? throw new KeyNotFoundException("Choose a product.");
                if (Key(held.Name) != Key(product.Name)) await AddAliasAsync(held.Name, "package", product.Id, null, ct);
                if (dto.NewFamilyId != null)      // its family, chosen at the same time (its quantities go to that family's tiers)
                {
                    if (!await _db.SolutionProductLines.AnyAsync(f => f.Id == dto.NewFamilyId, ct)) throw new KeyNotFoundException("Family not found.");
                    product.FamilyId = dto.NewFamilyId;
                }
                await _db.SaveChangesAsync(ct);
            }
            // (no family: the product is kept but not suggested - its quantities have no plan to go to)
            var familyTiers = (await _db.CatalogFamilyTiers.ToListAsync(ct)).ToLookup(x => x.FamilyId, x => x.TierId);
            await ApplyPackageRowAsync(product, row, tiers, familyTiers, ct);
        }
        else if (held.Kind == Categorization)
        {
            SolutionProductLine? family;
            if (dto.Create)
            {
                var groupId = dto.GroupId ?? Int(row["groupId"]) ?? throw new InvalidOperationException("Choose the category for the new family.");
                if (!await _db.SolutionGroups.AnyAsync(g => g.Id == groupId, ct)) throw new KeyNotFoundException("Category not found.");
                family = new SolutionProductLine { GroupId = groupId, Name = held.Name, SortOrder = await _db.SolutionProductLines.CountAsync(l => l.GroupId == groupId, ct) + 1 };
                _db.SolutionProductLines.Add(family);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                family = await _db.SolutionProductLines.FirstOrDefaultAsync(f => f.Id == dto.FamilyId, ct) ?? throw new KeyNotFoundException("Choose a family.");
                await AddAliasAsync(held.Name, "categorization", null, family.Id, ct);
            }
            var ticked = (row["tiers"] as JsonArray ?? new JsonArray()).Select(x => x!.ToString()).ToHashSet();
            await ApplyTiersAsync(family, ticked, tiers, ct);
        }
        _db.CatalogUnmatched.Remove(held);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new { mapped = held.Name };
    }
}
