using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Shared medicine catalogue. Every tenant can search it and copy entries into its own products.
/// Loading the catalogue (CSV) needs the platform key in configuration "Platform:AdminKey".
/// </summary>
[Authorize]
[ApiController]
[Route("api/medicine-master")]
public class MedicineMasterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    public MedicineMasterController(AppDbContext db, IConfiguration config, IInventoryService inventory, IAuditService audit)
    {
        _db = db;
        _config = config;
        _inventory = inventory;
        _audit = audit;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? barcode, [FromQuery] int take = 20)
    {
        var query = _db.MedicineMasters.Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(barcode)) query = query.Where(m => m.Barcode == barcode.Trim());
        else if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.BrandName.StartsWith(term) || m.GenericName.StartsWith(term) || m.BrandName.Contains(" " + term));
        }
        else return Ok(Array.Empty<object>());
        var rows = await query.OrderBy(m => m.BrandName).ThenBy(m => m.Strength).Take(Math.Clamp(take, 1, 100)).ToListAsync();
        var ids = rows.Select(r => r.Id).ToList();
        var mine = await _db.Products.Where(p => p.MedicineMasterId != null && ids.Contains(p.MedicineMasterId.Value))
            .Select(p => new { p.MedicineMasterId, p.Id }).ToListAsync();
        return Ok(rows.Select(m => new { medicine = m, productId = mine.FirstOrDefault(x => x.MedicineMasterId == m.Id)?.Id }));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
        => Ok(new
        {
            medicines = await _db.MedicineMasters.CountAsync(m => m.IsActive),
            interactions = await _db.DrugInteractions.CountAsync(),
            lastPriceUpdate = await _db.MedicineMasters.MaxAsync(m => (DateTime?)m.PriceUpdatedOn),
            linkedProducts = await _db.Products.CountAsync(p => p.MedicineMasterId != null)
        });

    /// <summary>Copies a catalogue entry into this pharmacy's products (or returns the existing link).</summary>
    [HttpPost("{id:int}/import")]
    public async Task<IActionResult> Import(int id, [FromBody] ImportMedicineDto? dto)
    {
        var m = await _db.MedicineMasters.FirstOrDefaultAsync(x => x.Id == id);
        if (m == null) return NotFound();
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.MedicineMasterId == id);
        if (existing != null) return Ok(new { productId = existing.Id, created = false });

        var name = string.Join(" ", new[] { m.BrandName, m.Strength, m.DosageForm }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var product = new Product
        {
            ProductName = name.Length > 300 ? name[..300] : name,
            SKU = $"MED{m.Id:D6}", ItemBarcode = m.Barcode, Manufacturer = m.Manufacturer,
            Category = dto?.Category ?? m.TherapeuticClass ?? "Medicines", Unit = "Tablet", ProductType = "single", SellingType = "online",
            GenericName = m.GenericName, Strength = m.Strength, DosageForm = m.DosageForm,
            UnitsPerStrip = m.UnitsPerStrip ?? 1, StripsPerBox = m.StripsPerBox ?? 1,
            IsPrescriptionOnly = m.Schedule is "Rx" or "Controlled", IsControlled = m.Schedule == "Controlled",
            Price = dto?.Price ?? (m.MaxRetailPrice.HasValue && (m.UnitsPerStrip ?? 1) * (m.StripsPerBox ?? 1) > 0
                ? Math.Round(m.MaxRetailPrice.Value / ((m.UnitsPerStrip ?? 1) * (m.StripsPerBox ?? 1)), 2) : 0),
            MedicineMasterId = m.Id, RackLocation = dto?.RackLocation, Description = $"Reg. {m.RegistrationNo}".Trim(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Products.Add(product);
        _audit.Log("ImportedFromCatalogue", "Product", m.Id, new { name });
        await _db.SaveChangesAsync();
        return Ok(new { productId = product.Id, created = true });
    }

    /// <summary>Products whose catalogue retail price changed since they were priced. Prices are per box in the catalogue, per tablet in products.</summary>
    [HttpGet("price-changes")]
    public async Task<IActionResult> PriceChanges()
    {
        var rows = await (from p in _db.Products
                          join m in _db.MedicineMasters on p.MedicineMasterId equals m.Id
                          where m.MaxRetailPrice != null
                          select new { p.Id, p.ProductName, p.Price, p.UnitsPerStrip, p.StripsPerBox, m.MaxRetailPrice, m.PriceUpdatedOn }).ToListAsync();
        var changes = rows.Select(r =>
        {
            var perUnit = Math.Round(r.MaxRetailPrice!.Value / Math.Max(1, r.UnitsPerStrip * r.StripsPerBox), 2);
            return new { productId = r.Id, r.ProductName, currentPrice = r.Price, newPrice = perUnit, boxPrice = r.MaxRetailPrice, r.PriceUpdatedOn };
        }).Where(r => Math.Abs(r.currentPrice - r.newPrice) >= 0.01m).OrderBy(r => r.ProductName).ToList();
        return Ok(changes);
    }

    /// <summary>Applies catalogue prices to the chosen products.</summary>
    [HttpPost("price-changes/apply")]
    public async Task<IActionResult> ApplyPriceChanges([FromBody] List<int> productIds)
    {
        if (!_audit.IsAdmin) return Forbid();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id) && p.MedicineMasterId != null).ToListAsync();
        var masterIds = products.Select(p => p.MedicineMasterId!.Value).ToList();
        var masters = await _db.MedicineMasters.Where(m => masterIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        var changed = 0;
        foreach (var p in products)
        {
            var m = masters.GetValueOrDefault(p.MedicineMasterId!.Value);
            if (m?.MaxRetailPrice == null) continue;
            var newPrice = Math.Round(m.MaxRetailPrice.Value / Math.Max(1, p.UnitsPerStrip * p.StripsPerBox), 2);
            _audit.Log("PriceChanged", "Product", p.Id, new { product = p.ProductName, retailFrom = p.Price, retailTo = newPrice, via = "Catalogue" });
            p.Price = newPrice;
            p.UpdatedAt = DateTime.UtcNow;
            changed++;
        }
        await _db.SaveChangesAsync();
        return Ok(new { changed });
    }

    /// <summary>Warns about known interactions between the medicines on a bill.</summary>
    [HttpPost("interactions")]
    public async Task<IActionResult> Interactions([FromBody] List<int> productIds)
    {
        var generics = await _db.Products.Where(p => productIds.Contains(p.Id) && p.GenericName != null)
            .Select(p => new { p.Id, p.ProductName, Generic = p.GenericName! }).ToListAsync();
        var names = generics.Select(g => g.Generic.Trim()).Distinct().ToList();
        if (names.Count < 2) return Ok(Array.Empty<object>());
        var hits = (await _db.DrugInteractions.Where(d => names.Contains(d.GenericA) && names.Contains(d.GenericB)).ToListAsync())
            .Where(d => string.CompareOrdinal(d.GenericA, d.GenericB) < 0).ToList(); // each pair is stored both ways
        return Ok(hits.Select(h => new
        {
            h.Severity, h.Note, h.GenericA, h.GenericB,
            productsA = generics.Where(g => g.Generic.Trim() == h.GenericA).Select(g => g.ProductName),
            productsB = generics.Where(g => g.Generic.Trim() == h.GenericB).Select(g => g.ProductName)
        }).OrderBy(h => h.Severity == "Major" ? 0 : h.Severity == "Moderate" ? 1 : 2));
    }

    // ── Platform maintenance (Asyntex staff only) ──

    private bool PlatformKeyOk()
    {
        var key = _config["Platform:AdminKey"];
        return !string.IsNullOrWhiteSpace(key) && Request.Headers.TryGetValue("X-Platform-Key", out var sent) && sent == key;
    }

    /// <summary>
    /// Loads or updates the catalogue from CSV. Header names: BrandName, GenericName, Strength, DosageForm, PackSize,
    /// UnitsPerStrip, StripsPerBox, Manufacturer, RegistrationNo, TherapeuticClass, Schedule, Barcode, MaxRetailPrice.
    /// Rows match on RegistrationNo (else brand + strength + form).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("platform/import")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> PlatformImport(IFormFile file)
    {
        if (!PlatformKeyOk()) return Unauthorized(new { message = "Platform key required." });
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var rows = Csv.Read(await reader.ReadToEndAsync());
        if (rows.Count < 2) return BadRequest(new { message = "The file has no data rows." });
        var head = rows[0].Select((h, i) => (h.Trim().ToLowerInvariant(), i)).ToDictionary(x => x.Item1, x => x.i);
        string? Col(List<string> r, string name) => head.TryGetValue(name.ToLowerInvariant(), out var i) && i < r.Count && !string.IsNullOrWhiteSpace(r[i]) ? r[i].Trim() : null;
        int? Int(List<string> r, string n) => int.TryParse(Col(r, n), out var v) ? v : null;
        decimal? Dec(List<string> r, string n) => decimal.TryParse(Col(r, n), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

        var existing = await _db.MedicineMasters.ToListAsync();
        var byReg = existing.Where(e => e.RegistrationNo != null).GroupBy(e => e.RegistrationNo!).ToDictionary(g => g.Key, g => g.First());
        int added = 0, updated = 0, skipped = 0;
        foreach (var r in rows.Skip(1))
        {
            var brand = Col(r, "BrandName");
            var generic = Col(r, "GenericName");
            if (brand == null || generic == null) { skipped++; continue; }
            var reg = Col(r, "RegistrationNo");
            var strength = Col(r, "Strength");
            var form = Col(r, "DosageForm");
            var m = reg != null && byReg.TryGetValue(reg, out var hit) ? hit
                : existing.FirstOrDefault(e => e.BrandName == brand && e.Strength == strength && e.DosageForm == form);
            if (m == null) { m = new MedicineMaster(); _db.MedicineMasters.Add(m); existing.Add(m); added++; } else updated++;
            var mrp = Dec(r, "MaxRetailPrice");
            if (mrp.HasValue && mrp != m.MaxRetailPrice) m.PriceUpdatedOn = DateTime.UtcNow.Date;
            m.BrandName = brand; m.GenericName = generic; m.Strength = strength; m.DosageForm = form;
            m.PackSize = Col(r, "PackSize"); m.UnitsPerStrip = Int(r, "UnitsPerStrip"); m.StripsPerBox = Int(r, "StripsPerBox");
            m.Manufacturer = Col(r, "Manufacturer"); m.RegistrationNo = reg; m.TherapeuticClass = Col(r, "TherapeuticClass");
            m.Schedule = Col(r, "Schedule") switch { "Rx" or "rx" or "RX" => "Rx", "Controlled" or "controlled" => "Controlled", null => null, _ => "OTC" };
            m.Barcode = Col(r, "Barcode"); m.MaxRetailPrice = mrp ?? m.MaxRetailPrice; m.IsActive = true;
            if (reg != null) byReg[reg] = m;
        }
        await _db.SaveChangesAsync();
        return Ok(new { added, updated, skipped });
    }

    /// <summary>Loads interactions from CSV: GenericA, GenericB, Severity, Note. Replaces the list.</summary>
    [AllowAnonymous]
    [HttpPost("platform/interactions")]
    public async Task<IActionResult> PlatformInteractions(IFormFile file)
    {
        if (!PlatformKeyOk()) return Unauthorized(new { message = "Platform key required." });
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var rows = Csv.Read(await reader.ReadToEndAsync()).Skip(1).Where(r => r.Count >= 2).ToList();
        _db.DrugInteractions.RemoveRange(_db.DrugInteractions);
        foreach (var r in rows)
        {
            var sev = r.Count > 2 ? r[2].Trim() : "Moderate";
            var a = r[0].Trim(); var b = r[1].Trim();
            var note = r.Count > 3 ? r[3].Trim() : null;
            sev = sev is "Minor" or "Major" ? sev : "Moderate";
            _db.DrugInteractions.Add(new DrugInteraction { GenericA = a, GenericB = b, Severity = sev, Note = note });
            _db.DrugInteractions.Add(new DrugInteraction { GenericA = b, GenericB = a, Severity = sev, Note = note });
        }
        await _db.SaveChangesAsync();
        return Ok(new { pairs = rows.Count });
    }
}

public class ImportMedicineDto
{
    public decimal? Price { get; set; }
    public string? Category { get; set; }
    public string? RackLocation { get; set; }
}

/// <summary>Minimal RFC-4180 CSV reader (quoted fields, doubled quotes, CRLF).</summary>
public static class Csv
{
    public static List<List<string>> Read(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n': row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        if (rows.Count > 0 && rows[0].Count > 0) rows[0][0] = rows[0][0].TrimStart('﻿');
        return rows.Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
    }
}

/// <summary>Urdu / Arabic product names and dosage notes for the counter and receipts.</summary>
[Authorize]
[ApiController]
[Route("api/product-translations")]
public class ProductTranslationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductTranslationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string lang = "ur", [FromQuery] string? productIds = null)
    {
        var q = _db.ProductTranslations.Where(t => t.Lang == lang);
        var ids = (productIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
        if (ids.Count > 0) q = q.Where(t => ids.Contains(t.ProductId));
        return Ok(await q.Select(t => new { t.ProductId, t.Lang, t.Name, t.UsageNote }).ToListAsync());
    }

    [HttpGet("{productId:int}")]
    public async Task<IActionResult> ForProduct(int productId)
        => Ok(await _db.ProductTranslations.Where(t => t.ProductId == productId).ToListAsync());

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Save(int productId, [FromBody] List<ProductTranslationDto> rows)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId)) return NotFound();
        var existing = await _db.ProductTranslations.Where(t => t.ProductId == productId).ToListAsync();
        foreach (var r in rows)
        {
            var lang = r.Lang is "ar" ? "ar" : "ur";
            var row = existing.FirstOrDefault(e => e.Lang == lang);
            if (string.IsNullOrWhiteSpace(r.Name))
            {
                if (row != null) _db.ProductTranslations.Remove(row);
                continue;
            }
            if (row == null) { row = new ProductTranslation { ProductId = productId, Lang = lang }; _db.ProductTranslations.Add(row); existing.Add(row); }
            row.Name = r.Name.Trim();
            row.UsageNote = r.UsageNote;
            row.Description = r.Description;
        }
        await _db.SaveChangesAsync();
        return Ok(await _db.ProductTranslations.Where(t => t.ProductId == productId).ToListAsync());
    }
}

/// <summary>The signed-in user's own preferences.</summary>
[Authorize]
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    public MeController(AppDbContext db) => _db = db;

    private int? PartyId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences()
    {
        var id = PartyId;
        var p = id == null ? null : await _db.Parties.Where(x => x.Id == id).Select(x => new { x.Language, x.LocationId, x.RestrictToLocation }).FirstOrDefaultAsync();
        var tenantLang = await _db.Tenants.Where(t => t.Id == _db.CurrentTenantId).Select(t => t.DefaultLanguage).FirstOrDefaultAsync();
        return Ok(new { language = p?.Language ?? tenantLang ?? "en", tenantLanguage = tenantLang ?? "en", locationId = p?.LocationId, restrictToLocation = p?.RestrictToLocation ?? false });
    }

    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage([FromBody] LanguageDto dto)
    {
        var lang = dto.Language is "ur" or "ar" ? dto.Language : "en";
        var id = PartyId;
        var p = id == null ? null : await _db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        p.Language = lang;
        await _db.SaveChangesAsync();
        return Ok(new { language = lang });
    }
}

public class LanguageDto
{
    public string Language { get; set; } = "en";
}
