using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Manufacturing Order - a production run that consumes raw materials and produces finished goods
/// </summary>
public class ManufacturingOrder : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    public int BomId { get; set; }

    public int? FinishedProductId { get; set; }

    public int Quantity { get; set; } = 1;

    public int? TargetStoreId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft | InProgress | Completed | Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal LaborCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OverheadCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalMaterialCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public BillOfMaterials? Bom { get; set; }
    public Product? FinishedProduct { get; set; }
    public Store? TargetStore { get; set; }
    public ICollection<ManufacturingOrderItem> Items { get; set; } = new List<ManufacturingOrderItem>();
}

/// <summary>
/// Manufacturing Order Item - raw material consumed in a manufacturing order
/// </summary>
public class ManufacturingOrderItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int ManufacturingOrderId { get; set; }

    public int RawMaterialId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequiredQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ConsumedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public int? SupplierId { get; set; }

    // Navigation
    public ManufacturingOrder? ManufacturingOrder { get; set; }
    public Product? RawMaterial { get; set; }
    public Party? Supplier { get; set; }
}
