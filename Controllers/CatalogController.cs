using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Inventory > Solution Packages: the company's product catalogue - products and their SKUs, package quantities
/// per tier, price lists, the products of each drawing rule, and importing the company's three files.
/// Everything is the logged-in company's only (tenant filter); ids from other companies are not found.
/// </summary>
[Authorize]
[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CatalogService _catalog;
    private readonly CatalogImportService _import;
    private readonly DrawingService _drawings;
    public CatalogController(AppDbContext db, CatalogService catalog, CatalogImportService import, DrawingService drawings)
    { _db = db; _catalog = catalog; _import = import; _drawings = drawings; }

    private static string? Clean(string? s, int max) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : s.Length > max ? s[..max] : s; }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await _catalog.SyncSkusAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    private async Task<object> LoadAsync(CancellationToken ct)
    {
        var tiers = await _catalog.TiersAsync(ct);
        var skus = (await _db.Products.AsNoTracking().Where(p => p.CatalogProductId != null).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.ProductName, p.Model, p.Color, p.IsDefaultSku, p.CatalogProductId, p.Price }).ToListAsync(ct)).ToLookup(p => p.CatalogProductId!.Value);
        var aliases = (await _db.CatalogAliases.AsNoTracking().ToListAsync(ct));
        var familyTiers = (await _db.CatalogFamilyTiers.AsNoTracking().ToListAsync(ct)).ToLookup(x => x.FamilyId, x => x.TierId);
        var groups = await _db.SolutionGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync(ct);
        var families = await _db.SolutionProductLines.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync(ct);
        var priceLists = await _db.CatalogPriceLists.AsNoTracking().OrderByDescending(l => l.IsActive).ThenByDescending(l => l.ImportedAt).ToListAsync(ct);
        var listCounts = await _db.CatalogPriceListItems.AsNoTracking().GroupBy(i => i.PriceListId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return new
        {
            tiers = tiers.Select(t => new { t.Id, t.Name, t.Code }),
            sizes = await _db.PackageSizes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => new { s.Id, s.Name }).ToListAsync(ct),
            groups = groups.Select(g => new
            {
                g.Id, g.Name,
                families = families.Where(f => f.GroupId == g.Id).Select(f => new
                {
                    f.Id, f.Name, tierIds = familyTiers[f.Id].ToList(),
                    aliases = aliases.Where(a => a.FamilyId == f.Id).Select(a => new { a.Id, a.Alias, a.Source }),
                }),
            }),
            products = (await _db.CatalogProducts.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct)).Select(p => new
            {
                p.Id, p.Name, p.FamilyId, p.Unit, p.FallbackPrice, p.Description,
                skus = skus[p.Id].Select(s => new { s.Id, name = s.ProductName, s.Model, s.Color, isDefault = s.IsDefaultSku, price = (decimal?)s.Price }),
                aliases = aliases.Where(a => a.CatalogProductId == p.Id).Select(a => new { a.Id, a.Alias, a.Source }),
            }),
            packageLines = await _db.CatalogPackageLines.AsNoTracking().Select(l => new { sizeId = l.PackageSizeId, l.TierId, productId = l.CatalogProductId, l.Quantity }).ToListAsync(ct),
            priceLists = priceLists.Select(l => new { l.Id, l.Name, l.IsActive, l.SourceFile, l.ImportedAt, items = listCounts.GetValueOrDefault(l.Id) }),
            ruleItems = await _db.CatalogDrawingRuleItems.AsNoTracking().OrderBy(r => r.RuleName).ThenBy(r => r.Gang).ThenBy(r => r.Rank)
                .Select(r => new { r.RuleName, r.Gang, r.Rank, productId = r.CatalogProductId }).ToListAsync(ct),
            unmatched = await _db.CatalogUnmatched.AsNoTracking().OrderBy(u => u.Kind).ThenBy(u => u.Name).Select(u => new { u.Id, u.Kind, u.Name, u.RowJson, u.CreatedAt }).ToListAsync(ct),
        };
    }

    // ── package quantities (per tier) ─────────────────────────────────────────────────────────────────────
    public record LineDto(int SizeId, int TierId, int ProductId, decimal? Quantity);

    /// <summary>Saves cells of the package grid; an empty or 0 quantity removes the line</summary>
    [HttpPut("package-lines")]
    public async Task<IActionResult> SaveLines([FromBody] List<LineDto> cells, CancellationToken ct)
    {
        var sizeIds = (await _db.PackageSizes.Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var tierIds = (await _db.CatalogTiers.Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var productIds = (await _db.CatalogProducts.Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        if (cells.Any(c => !sizeIds.Contains(c.SizeId) || !tierIds.Contains(c.TierId) || !productIds.Contains(c.ProductId)))
            return BadRequest(new { message = "A package size, tier or product was not found." });
        if (cells.Any(c => c.Quantity < 0)) return BadRequest(new { message = "A quantity can't be negative." });
        var existing = await _db.CatalogPackageLines.ToListAsync(ct);
        foreach (var c in cells)
        {
            var line = existing.FirstOrDefault(l => l.PackageSizeId == c.SizeId && l.TierId == c.TierId && l.CatalogProductId == c.ProductId);
            var q = Math.Round(c.Quantity ?? 0, 3);
            if (q <= 0) { if (line != null) _db.CatalogPackageLines.Remove(line); }
            else if (line == null) _db.CatalogPackageLines.Add(new CatalogPackageLine { PackageSizeId = c.SizeId, TierId = c.TierId, CatalogProductId = c.ProductId, Quantity = q });
            else line.Quantity = q;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    // ── products ─────────────────────────────────────────────────────────────────────────────────────────
    public record ProductDto(string? Name, int? FamilyId, string? Unit, decimal? FallbackPrice, string? Description);

    /// <summary>A product that is not on the price list yet (its price is the fallback price)</summary>
    [HttpPost("products")]
    public async Task<IActionResult> AddProduct([FromBody] ProductDto dto, CancellationToken ct)
    {
        var name = Clean(dto.Name, 255);
        if (name == null) return BadRequest(new { message = "Enter the product name." });
        if (await _db.CatalogProducts.AnyAsync(p => p.Name == name, ct)) return Conflict(new { message = $"\"{name}\" is already a product." });
        if (dto.FamilyId != null && !await _db.SolutionProductLines.AnyAsync(f => f.Id == dto.FamilyId, ct)) return BadRequest(new { message = "Family not found." });
        _db.CatalogProducts.Add(new CatalogProduct { Name = name, FamilyId = dto.FamilyId, Unit = Clean(dto.Unit, 20) ?? "pc", FallbackPrice = dto.FallbackPrice, Description = dto.Description });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDto dto, CancellationToken ct)
    {
        var p = await _db.CatalogProducts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return NotFound();
        var name = Clean(dto.Name, 255);
        if (name == null) return BadRequest(new { message = "Enter the product name." });
        if (await _db.CatalogProducts.AnyAsync(x => x.Id != id && x.Name == name, ct)) return Conflict(new { message = $"\"{name}\" is already a product." });
        if (dto.FamilyId != null && !await _db.SolutionProductLines.AnyAsync(f => f.Id == dto.FamilyId, ct)) return BadRequest(new { message = "Family not found." });
        p.Name = name; p.FamilyId = dto.FamilyId; p.Unit = Clean(dto.Unit, 20) ?? p.Unit; p.FallbackPrice = dto.FallbackPrice;
        if (dto.Description != null) p.Description = dto.Description;
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    /// <summary>Only a product without SKUs (inventory rows) can be deleted; its aliases, package lines and rule links go with it</summary>
    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var p = await _db.CatalogProducts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return NotFound();
        if (await _db.Products.IgnoreQueryFilters().AnyAsync(s => s.TenantId == p.TenantId && s.CatalogProductId == id, ct))
            return Conflict(new { message = $"{p.Name} has inventory SKUs - delete or move them on the Products screen first." });
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.CatalogAliases.Where(a => a.CatalogProductId == id).ExecuteDeleteAsync(ct);
        await _db.CatalogPackageLines.Where(l => l.CatalogProductId == id).ExecuteDeleteAsync(ct);
        await _db.CatalogDrawingRuleItems.Where(r => r.CatalogProductId == id).ExecuteDeleteAsync(ct);
        await _db.PackageItems.IgnoreQueryFilters().Where(i => i.TenantId == p.TenantId && i.CatalogProductId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.CatalogProductId, (int?)null), ct);
        _db.CatalogProducts.Remove(p);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    /// <summary>The SKU (colour) a product's quotation line starts with</summary>
    [HttpPut("products/{id}/default-sku/{skuId}")]
    public async Task<IActionResult> SetDefaultSku(int id, int skuId, CancellationToken ct)
    {
        var sku = await _db.Products.FirstOrDefaultAsync(s => s.Id == skuId && s.CatalogProductId == id, ct);
        if (sku == null) return NotFound(new { message = "That SKU is not one of this product's." });
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Products.Where(s => s.CatalogProductId == id && s.IsDefaultSku).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefaultSku, false), ct);
        await _db.Products.Where(s => s.Id == skuId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefaultSku, true), ct);
        await tx.CommitAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    // ── aliases ──────────────────────────────────────────────────────────────────────────────────────────
    public record AliasDto(string? Alias, int? ProductId, int? FamilyId);

    [HttpPost("aliases")]
    public async Task<IActionResult> AddAlias([FromBody] AliasDto dto, CancellationToken ct)
    {
        var alias = Clean(dto.Alias, 255);
        if (alias == null) return BadRequest(new { message = "Enter the other name." });
        if ((dto.ProductId == null) == (dto.FamilyId == null)) return BadRequest(new { message = "Choose a product or a family." });
        if (dto.ProductId != null && !await _db.CatalogProducts.AnyAsync(p => p.Id == dto.ProductId, ct)) return NotFound();
        if (dto.FamilyId != null && !await _db.SolutionProductLines.AnyAsync(f => f.Id == dto.FamilyId, ct)) return NotFound();
        var kind = dto.ProductId != null ? "product" : "family";
        var key = CatalogService.Key(alias);
        if (await _db.CatalogAliases.AnyAsync(a => a.TargetKind == kind && a.AliasKey == key, ct)) return Conflict(new { message = $"\"{alias}\" is already another name of a {kind}." });
        _db.CatalogAliases.Add(new CatalogAlias { Alias = alias, AliasKey = key, Source = "manual", TargetKind = kind, CatalogProductId = dto.ProductId, FamilyId = dto.FamilyId });
        await _db.SaveChangesAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    [HttpDelete("aliases/{id}")]
    public async Task<IActionResult> DeleteAlias(int id, CancellationToken ct)
    {
        await _db.CatalogAliases.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    // ── price lists ──────────────────────────────────────────────────────────────────────────────────────
    [HttpPost("price-lists/{id}/activate")]
    public async Task<IActionResult> ActivatePriceList(int id, CancellationToken ct)
    {
        try { await _catalog.ActivatePriceListAsync(id, ct); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        return Ok(await LoadAsync(ct));
    }

    // ── drawing rules ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The reader's rules (name, gangs) and which products each uses</summary>
    [HttpGet("rules")]
    public async Task<IActionResult> Rules(CancellationToken ct)
    {
        object rules;
        try { rules = await _drawings.RulesAsync(ct); }
        catch (DrawingServiceException e) { return StatusCode(e.StatusCode, new { message = e.Message }); }
        return Ok(new { rules });
    }

    public record RuleDto(string? RuleName, string? Gang, List<int>? ProductIds);

    /// <summary>Replaces the products of one rule (and gang), best first</summary>
    [HttpPut("rules")]
    public async Task<IActionResult> SaveRule([FromBody] RuleDto dto, CancellationToken ct)
    {
        var rule = Clean(dto.RuleName, 100);
        if (rule == null) return BadRequest(new { message = "Rule name missing." });
        var gang = Clean(dto.Gang, 10) ?? "";
        var ids = (dto.ProductIds ?? new()).Distinct().ToList();
        if (ids.Count > 0 && await _db.CatalogProducts.CountAsync(p => ids.Contains(p.Id), ct) != ids.Count) return BadRequest(new { message = "A product was not found." });
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.CatalogDrawingRuleItems.Where(r => r.RuleName == rule && r.Gang == gang).ExecuteDeleteAsync(ct);
        for (var i = 0; i < ids.Count; i++) _db.CatalogDrawingRuleItems.Add(new CatalogDrawingRuleItem { RuleName = rule, Gang = gang, Rank = i + 1, CatalogProductId = ids[i] });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(await LoadAsync(ct));
    }

    // ── import ───────────────────────────────────────────────────────────────────────────────────────────
    [HttpGet("import/profiles")]
    public async Task<IActionResult> Profiles(CancellationToken ct)
    {
        var result = new Dictionary<string, JsonObject>();
        foreach (var kind in CatalogImportService.Kinds) result[kind] = await _import.ProfileAsync(kind, ct);
        return Ok(result);
    }

    [HttpPut("import/profiles/{kind}")]
    public async Task<IActionResult> SaveProfile(string kind, [FromBody] JsonObject profile, CancellationToken ct)
    {
        if (!CatalogImportService.Kinds.Contains(kind)) return NotFound();
        if (profile["headers"] is not JsonObject) return BadRequest(new { message = "The profile needs a \"headers\" object." });
        await _import.SaveProfileAsync(kind, profile, ct);
        return Ok(await _import.ProfileAsync(kind, ct));
    }

    [HttpDelete("import/profiles/{kind}")]
    public async Task<IActionResult> ResetProfile(string kind, CancellationToken ct)
    {
        await _db.CatalogImportProfiles.Where(p => p.Kind == kind).ExecuteDeleteAsync(ct);
        return Ok(await _import.ProfileAsync(kind, ct));
    }

    public record ImportDto(string? FileName, List<List<string?>>? Rows, bool? Activate);

    /// <summary>Imports one file (its rows, read by the browser). Returns the import report.</summary>
    [HttpPost("import/{kind}")]
    public async Task<IActionResult> Import(string kind, [FromBody] ImportDto dto, CancellationToken ct)
    {
        if (dto.Rows == null || dto.Rows.Count == 0) return BadRequest(new { message = "The file has no rows." });
        var file = Clean(dto.FileName, 255) ?? kind;
        try
        {
            JsonObject report = kind switch
            {
                CatalogImportService.PriceList => await _import.ImportPriceListAsync(file, dto.Rows, dto.Activate ?? true, ct),
                CatalogImportService.Categorization => await _import.ImportCategorizationAsync(file, dto.Rows, ct),
                CatalogImportService.Packages => await _import.ImportPackagesAsync(file, dto.Rows, ct),
                _ => throw new KeyNotFoundException("Unknown file kind."),
            };
            return Ok(report);
        }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
    }

    [HttpPost("unmatched/{id}/map")]
    public async Task<IActionResult> Map(int id, [FromBody] CatalogImportService.MapDto dto, CancellationToken ct)
    {
        try { await _import.MapAsync(id, dto, ct); }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        return Ok(await LoadAsync(ct));
    }

    /// <summary>Not needed: the held name is dropped (it comes back if a later import still has it)</summary>
    [HttpDelete("unmatched/{id}")]
    public async Task<IActionResult> Dismiss(int id, CancellationToken ct)
    {
        await _db.CatalogUnmatched.Where(u => u.Id == id).ExecuteDeleteAsync(ct);
        return Ok(await LoadAsync(ct));
    }
}
