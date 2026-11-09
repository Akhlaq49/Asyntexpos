using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// The price the company pays for an inventory product (INVT_PPRC): one row per product, set on Inventory > Purchase
/// Prices. Purchase orders take their unit cost from it, and a quotation's profit is worked out against it.
/// The selling (end user) price stays on the product (Products.Price).
/// </summary>
public class ProductPurchasePrice : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(150)]
    public string? UpdatedBy { get; set; }
}
