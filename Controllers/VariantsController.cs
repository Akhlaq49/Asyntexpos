using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

public class GenerateVariantsDto
{
    public string? ArticleNo { get; set; }
    public string? Season { get; set; }
    /// <summary>In grid order: first = rows (e.g. Colour), second = columns (e.g. Size).</summary>
    public List<GenerateOptionDto> Options { get; set; } = new();
}

public class GenerateOptionDto
{
    public int AttributeId { get; set; }
    public List<string> Values { get; set; } = new();
}

public class UpdateVariantDto
{
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal? Price { get; set; }
    public decimal? CostPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Article / colour / size matrix: variant generation, grids, barcode lookup and size-wise sales.</summary>
[Authorize]
[ApiController]
[Route("api/variants")]
public class VariantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public VariantsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Values of every attribute, kept in step with the comma list on the attribute.</summary>
    [HttpGet("attributes")]
    public async Task<IActionResult> Attributes()
    {
        var attrs = await _db.VariantAttributes.Where(a => a.Status == "active").OrderBy(a => a.Name).ToListAsync();
        var values = await _db.VariantAttributeValues.ToListAsync();
        var changed = false;
        foreach (var a in attrs)
        {
            var listed = a.Values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            for (var i = 0; i < listed.Count; i++)
            {
                var v = values.FirstOrDefault(x => x.VariantAttributeId == a.Id && x.Value == listed[i]);
                if (v == null)
                {
                    v = new VariantAttributeValue { VariantAttributeId = a.Id, Value = listed[i], Code = CodeOf(listed[i]), SortOrder = i };
                    _db.VariantAttributeValues.Add(v);
                    values.Add(v);
                    changed = true;
                }
                else if (v.SortOrder != i) { v.SortOrder = i; changed = true; }
            }
        }
        if (changed) await _db.SaveChangesAsync();
        return Ok(attrs.Select(a => new
        {
            a.Id, a.Name,
            values = values.Where(v => v.VariantAttributeId == a.Id).OrderBy(v => v.SortOrder).Select(v => new { v.Id, v.Value, v.Code })
        }));
    }

    private static string CodeOf(string value)
    {
        var letters = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return letters.Length <= 4 ? letters : letters[..3];
    }

    [HttpGet("products/{productId:int}")]
    public async Task<IActionResult> ForProduct(int productId, [FromQuery] int? locationId)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (p == null) return NotFound();
        var options = await (from o in _db.ProductOptions
                             join a in _db.VariantAttributes on o.VariantAttributeId equals a.Id
                             where o.ProductId == productId
                             orderby o.Position
                             select new { o.VariantAttributeId, a.Name, o.Position }).ToListAsync();
        var variants = await _db.ProductVariants.Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToListAsync();
        var stock = await _db.ProductBatches.Where(b => b.ProductId == productId && b.VariantId != null && (locationId == null || b.LocationId == locationId))
            .GroupBy(b => b.VariantId!.Value).Select(g => new { Id = g.Key, Qty = g.Sum(b => b.Quantity) }).ToListAsync();
        return Ok(new
        {
            product = new { p.Id, p.ProductName, p.ArticleNo, p.Season, p.HasVariants, p.Price, p.SKU },
            options,
            variants = variants.Select(v => new { v.Id, v.Sku, v.Barcode, v.Option1, v.Option2, v.Option3, v.Price, v.CostPrice, v.IsActive, stock = stock.FirstOrDefault(s => s.Id == v.Id)?.Qty ?? 0 })
        });
    }

    /// <summary>Creates every combination of the chosen values (existing combinations are kept).</summary>
    [HttpPost("products/{productId:int}/generate")]
    public async Task<IActionResult> Generate(int productId, [FromBody] GenerateVariantsDto dto)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (p == null) return NotFound();
        var opts = dto.Options.Where(o => o.Values.Count > 0).Take(3).ToList();
        if (opts.Count == 0) throw new BusinessRuleException("Pick at least one attribute and its values.");
        if (p.Quantity != 0 && !p.HasVariants)
            throw new BusinessRuleException($"{p.ProductName} already holds {p.Quantity} without size/colour. Bring its stock to zero (or move it to a new article) before adding variants.");

        p.HasVariants = true;
        p.ArticleNo = string.IsNullOrWhiteSpace(dto.ArticleNo) ? p.ArticleNo ?? p.SKU : dto.ArticleNo.Trim();
        p.Season = dto.Season ?? p.Season;

        var existingOptions = await _db.ProductOptions.Where(o => o.ProductId == productId).ToListAsync();
        _db.ProductOptions.RemoveRange(existingOptions);
        for (var i = 0; i < opts.Count; i++)
            _db.ProductOptions.Add(new ProductOption { ProductId = productId, VariantAttributeId = opts[i].AttributeId, Position = i + 1 });

        var allValues = await _db.VariantAttributeValues.ToListAsync();
        var combos = new List<List<string>> { new() };
        foreach (var o in opts)
            combos = combos.SelectMany(c => o.Values.Select(v => c.Append(v.Trim()).ToList())).ToList();

        var existing = await _db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
        var prefix = (p.ArticleNo ?? p.SKU ?? $"P{p.Id}").Trim();
        var created = 0;
        foreach (var combo in combos)
        {
            string? O(int i) => i < combo.Count ? combo[i] : null;
            if (existing.Any(v => v.Option1 == O(0) && v.Option2 == O(1) && v.Option3 == O(2))) continue;
            var codes = combo.Select((val, i) => allValues.FirstOrDefault(x => x.VariantAttributeId == opts[i].AttributeId && x.Value == val)?.Code ?? CodeOf(val));
            var sku = $"{prefix}-{string.Join("-", codes)}";
            var n = 2;
            while (await _db.ProductVariants.AnyAsync(v => v.Sku == sku) || _db.ChangeTracker.Entries<ProductVariant>().Any(e => e.Entity.Sku == sku)) sku = $"{prefix}-{string.Join("-", codes)}-{n++}";
            var variant = new ProductVariant { ProductId = productId, Sku = sku, Barcode = sku, Option1 = O(0), Option2 = O(1), Option3 = O(2) };
            _db.ProductVariants.Add(variant);
            created++;
        }
        _audit.Log("VariantsGenerated", "Product", productId, new { created, options = opts.Select(o => new { o.AttributeId, o.Values }) });
        await _db.SaveChangesAsync();

        // Link variants to their attribute values (used by filters and reports).
        var variants = await _db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
        var vids = variants.Select(v => v.Id).ToList();
        _db.ProductVariantValues.RemoveRange(_db.ProductVariantValues.Where(x => vids.Contains(x.VariantId)));
        foreach (var v in variants)
        {
            var vals = new[] { v.Option1, v.Option2, v.Option3 };
            for (var i = 0; i < opts.Count; i++)
            {
                var av = allValues.FirstOrDefault(x => x.VariantAttributeId == opts[i].AttributeId && x.Value == vals[i]);
                if (av != null) _db.ProductVariantValues.Add(new ProductVariantValue { VariantId = v.Id, AttributeValueId = av.Id });
            }
        }
        await _db.SaveChangesAsync();
        return Ok(new { created, total = variants.Count });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVariantDto dto)
    {
        var v = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Sku) && dto.Sku != v.Sku)
        {
            if (await _db.ProductVariants.AnyAsync(x => x.Sku == dto.Sku && x.Id != id)) throw new BusinessRuleException($"SKU {dto.Sku} is already used.");
            v.Sku = dto.Sku.Trim();
        }
        v.Barcode = dto.Barcode;
        v.Price = dto.Price;
        v.CostPrice = dto.CostPrice;
        v.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return Ok(v);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        if (await _db.ProductBatches.AnyAsync(b => b.VariantId == id && b.Quantity != 0)) throw new BusinessRuleException("This variant still has stock.");
        if (await _db.SaleItems.AnyAsync(s => s.VariantId == id) || await _db.PurchaseItems.AnyAsync(s => s.VariantId == id))
        {
            v.IsActive = false; // used on bills; keep for history
        }
        else
        {
            _db.ProductVariantValues.RemoveRange(_db.ProductVariantValues.Where(x => x.VariantId == id));
            _db.ProductVariants.Remove(v);
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Scan at the counter: barcode or SKU of a variant, or of a plain product.</summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest();
        var c = code.Trim();
        var v = await _db.ProductVariants.FirstOrDefaultAsync(x => x.IsActive && (x.Barcode == c || x.Sku == c));
        if (v == null) return NotFound();
        var p = await _db.Products.FirstAsync(x => x.Id == v.ProductId);
        return Ok(new
        {
            variant = new { v.Id, v.Sku, v.Barcode, v.Option1, v.Option2, v.Option3, price = v.Price ?? p.Price },
            product = new { p.Id, p.ProductName, p.Price, p.TaxType, p.Tax, p.Category },
            label = string.Join(" / ", new[] { v.Option1, v.Option2, v.Option3 }.Where(x => x != null))
        });
    }

    /// <summary>Colour × size grid of stock (or of sales in a date range).</summary>
    [HttpGet("products/{productId:int}/matrix")]
    public async Task<IActionResult> Matrix(int productId, [FromQuery] int? locationId, [FromQuery] string? measure, [FromQuery] string? from, [FromQuery] string? to)
    {
        var variants = await _db.ProductVariants.Where(v => v.ProductId == productId && v.IsActive).ToListAsync();
        Dictionary<int, int> values;
        if (measure == "sales")
        {
            var start = DateTime.TryParse(from, out var f) ? f.Date : DateTime.UtcNow.Date.AddDays(-29);
            var end = (DateTime.TryParse(to, out var t) ? t.Date : DateTime.UtcNow.Date).AddDays(1);
            values = await (from i in _db.SaleItems
                            join s in _db.Sales on i.SaleId equals s.Id
                            where i.ProductId == productId && i.VariantId != null && s.SaleDate >= start && s.SaleDate < end && (locationId == null || s.LocationId == locationId)
                            group i by i.VariantId!.Value into g
                            select new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        }
        else
        {
            values = await _db.ProductBatches.Where(b => b.ProductId == productId && b.VariantId != null && (locationId == null || b.LocationId == locationId))
                .GroupBy(b => b.VariantId!.Value).Select(g => new { g.Key, Qty = g.Sum(b => b.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        }
        var rows = variants.Select(v => v.Option1 ?? "").Distinct().ToList();
        var cols = variants.Select(v => v.Option2 ?? "").Distinct().ToList();
        // Keep attribute value order (S, M, L) instead of alphabetical.
        var order = await _db.VariantAttributeValues.ToDictionaryAsync(v => v.Value + "|" + v.VariantAttributeId, v => v.SortOrder);
        var opts = await _db.ProductOptions.Where(o => o.ProductId == productId).OrderBy(o => o.Position).Select(o => o.VariantAttributeId).ToListAsync();
        int Sort(string val, int pos) => opts.Count > pos && order.TryGetValue(val + "|" + opts[pos], out var s) ? s : 999;
        rows = rows.OrderBy(r => Sort(r, 0)).ToList();
        cols = cols.OrderBy(c => Sort(c, 1)).ToList();
        return Ok(new
        {
            rows, columns = cols,
            cells = variants.Select(v => new { variantId = v.Id, row = v.Option1 ?? "", column = v.Option2 ?? "", v.Sku, value = values.GetValueOrDefault(v.Id) })
        });
    }

    /// <summary>Which sizes and colours sell, across all articles.</summary>
    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] string? from, [FromQuery] string? to, [FromQuery] int? locationId)
    {
        var start = DateTime.TryParse(from, out var f) ? f.Date : DateTime.UtcNow.Date.AddDays(-29);
        var end = (DateTime.TryParse(to, out var t) ? t.Date : DateTime.UtcNow.Date).AddDays(1);
        var lines = await (from i in _db.SaleItems
                           join s in _db.Sales on i.SaleId equals s.Id
                           join v in _db.ProductVariants on i.VariantId equals v.Id
                           where s.SaleDate >= start && s.SaleDate < end && (locationId == null || s.LocationId == locationId)
                           select new { v.Option1, v.Option2, i.Quantity, i.TotalCost, i.CostPrice }).ToListAsync();
        var byFirst = lines.GroupBy(l => l.Option1 ?? "").Select(g => new
        {
            value = g.Key, units = g.Sum(x => x.Quantity), sales = g.Sum(x => x.TotalCost), margin = g.Sum(x => x.TotalCost - x.CostPrice * x.Quantity)
        }).OrderByDescending(x => x.units).ToList();
        var bySecond = lines.GroupBy(l => l.Option2 ?? "").Select(g => new
        {
            value = g.Key, units = g.Sum(x => x.Quantity), sales = g.Sum(x => x.TotalCost), margin = g.Sum(x => x.TotalCost - x.CostPrice * x.Quantity)
        }).OrderByDescending(x => x.units).ToList();
        return Ok(new { from = start.ToString("yyyy-MM-dd"), to = end.AddDays(-1).ToString("yyyy-MM-dd"), byFirst, bySecond });
    }
}
