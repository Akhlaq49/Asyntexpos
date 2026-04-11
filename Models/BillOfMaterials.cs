using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Bill of Materials - defines a recipe for manufacturing a finished product
/// </summary>
public class BillOfMaterials : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int? FinishedProductId { get; set; }

    [MaxLength(300)]
    public string? FinishedProductName { get; set; }

    [MaxLength(200)]
    public string? FinishedProductCategory { get; set; }

    [MaxLength(200)]
    public string? FinishedProductSubCategory { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalePrice { get; set; }

    public int OutputQuantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LaborCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OverheadCost { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product? FinishedProduct { get; set; }
    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}

/// <summary>
/// BOM line item - a raw material needed for a BOM
/// </summary>
public class BomItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int BomId { get; set; }

    public int RawMaterialId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    public int? SupplierId { get; set; }

    // Navigation
    public BillOfMaterials? Bom { get; set; }
    public Product? RawMaterial { get; set; }
    public Party? Supplier { get; set; }
}
