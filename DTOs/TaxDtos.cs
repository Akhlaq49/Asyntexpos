using System.Text.Json;

namespace ReactPosApi.DTOs;

public class TaxTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveTaxTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One tax applied to a product / quotation / purchase order / sale (name copied so it survives renames)</summary>
public class AppliedTaxDto
{
    public int? TaxTypeId { get; set; }
    public string TaxName { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    /// <summary>Calculated by the server: base amount x Percent / 100</summary>
    public decimal Amount { get; set; }
}

/// <summary>Applied taxes are stored as a JSON list on the document (TaxesJson column)</summary>
public static class AppliedTaxes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Drops empty rows, caps the rate at 100% and recalculates every amount from the base</summary>
    public static List<AppliedTaxDto> Calculate(IEnumerable<AppliedTaxDto>? taxes, decimal baseAmount)
    {
        var taxBase = Math.Max(0, baseAmount);
        return (taxes ?? Enumerable.Empty<AppliedTaxDto>())
            .Where(t => !string.IsNullOrWhiteSpace(t.TaxName) && t.Percent > 0)
            .Select(t =>
            {
                var percent = Math.Round(Math.Min(t.Percent, 100), 2);
                return new AppliedTaxDto
                {
                    TaxTypeId = t.TaxTypeId,
                    TaxName = t.TaxName.Trim(),
                    Percent = percent,
                    Amount = Math.Round(taxBase * percent / 100, 2),
                };
            })
            .ToList();
    }

    public static string? Serialize(List<AppliedTaxDto> taxes) => taxes.Count == 0 ? null : JsonSerializer.Serialize(taxes, Json);

    public static List<AppliedTaxDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<AppliedTaxDto>>(json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>"GST 17%, WHT 2%" - short text for old screens that show a single tax field</summary>
    public static string? Summary(List<AppliedTaxDto> taxes, int maxLength = 50)
    {
        if (taxes.Count == 0) return null;
        var text = string.Join(", ", taxes.Select(t => $"{t.TaxName} {t.Percent:0.##}%"));
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }
}
