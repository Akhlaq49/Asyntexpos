using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class StockTransfer : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [MaxLength(200)]
    public string WarehouseFrom { get; set; } = string.Empty;

    [MaxLength(200)]
    public string WarehouseTo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }

    /// <summary>Draft | Dispatched | Received | Cancelled</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Received";

    [MaxLength(200)]
    public string? DispatchedBy { get; set; }
    public DateTime? DispatchedAt { get; set; }
    [MaxLength(200)]
    public string? ReceivedBy { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public int? StockRequestId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<StockTransferItem> Items { get; set; } = new();
}

public class StockTransferItem : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;

    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int? BatchId { get; set; }
    [MaxLength(100)]
    public string? BatchNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    /// <summary>Destination batch created on receipt.</summary>
    public int? ReceivedBatchId { get; set; }
    public ProductBatch? ReceivedBatch { get; set; }
    public int? ReceivedQty { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
}
