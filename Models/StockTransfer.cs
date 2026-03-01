using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class StockTransfer
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string WarehouseFrom { get; set; } = string.Empty;

    [MaxLength(200)]
    public string WarehouseTo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<StockTransferItem> Items { get; set; } = new();
}

public class StockTransferItem
{
    public int Id { get; set; }

    public int StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;

    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}
