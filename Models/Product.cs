using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Product : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [MaxLength(100)]
    public string? Store { get; set; }

    [MaxLength(100)]
    public string? Warehouse { get; set; }

    [Required, MaxLength(300)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Slug { get; set; }

    [MaxLength(100)]
    public string? SKU { get; set; }

    [MaxLength(50)]
    public string? SellingType { get; set; }

    [MaxLength(200)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? SubCategory { get; set; }

    [MaxLength(200)]
    public string? Brand { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [MaxLength(50)]
    public string? BarcodeSymbology { get; set; }

    [MaxLength(200)]
    public string? ItemBarcode { get; set; }

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string ProductType { get; set; } = "single";

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    [MaxLength(50)]
    public string? TaxType { get; set; }

    [MaxLength(50)]
    public string? Tax { get; set; }

    [MaxLength(50)]
    public string? DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public int QuantityAlert { get; set; }

    [MaxLength(200)]
    public string? Warranty { get; set; }

    [MaxLength(200)]
    public string? Manufacturer { get; set; }

    [MaxLength(50)]
    public string? ManufacturedDate { get; set; }

    [MaxLength(50)]
    public string? ExpiryDate { get; set; }

    // ── Pharmacy fields ──
    [MaxLength(200)]
    public string? GenericName { get; set; }

    [MaxLength(100)]
    public string? Strength { get; set; }

    /// <summary>Tablet, Capsule, Syrup, Injection, Cream, Drops, ...</summary>
    [MaxLength(50)]
    public string? DosageForm { get; set; }

    [MaxLength(100)]
    public string? RackLocation { get; set; }

    public bool IsPrescriptionOnly { get; set; }

    public bool IsControlled { get; set; }

    /// <summary>Latest purchase (trade) cost per base unit. Price is the retail price per base unit.</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>Base units (e.g. tablets) in one strip. 1 = item is not sold loose.</summary>
    public int UnitsPerStrip { get; set; } = 1;

    /// <summary>Strips in one box.</summary>
    public int StripsPerBox { get; set; } = 1;

    /// <summary>Days one purchase normally lasts; 0 = no refill reminder.</summary>
    public int RefillDays { get; set; }

    public int? MedicineMasterId { get; set; }

    // Variants
    [MaxLength(100)]
    public string? ArticleNo { get; set; }
    [MaxLength(50)]
    public string? Season { get; set; }
    public bool HasVariants { get; set; }

    /// <summary>Listed on the web store and Shopify.</summary>
    public bool SellOnline { get; set; } = true;

    // Restaurant
    public bool IsMenuItem { get; set; }
    public int? KitchenStationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
