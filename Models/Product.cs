using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
