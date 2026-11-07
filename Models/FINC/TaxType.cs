using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

/// <summary>
/// A kind of tax the company charges (GST, WHT, Sales Tax ...), managed under Inventory > Tax Types.
/// The percentage is entered each time the tax is applied to a product, quotation or purchase order.
/// </summary>
public class TaxType : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
