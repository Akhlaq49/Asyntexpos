using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Quotation
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Party? Customer { get; set; }

    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CustomerImage { get; set; }

    // Primary product shown in list row
    public int? ProductId { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? ProductImage { get; set; }

    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Sent, Pending, Ordered

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<QuotationItem> Items { get; set; } = new();
}

public class QuotationItem
{
    public int Id { get; set; }

    public int QuotationId { get; set; }
    public Quotation Quotation { get; set; } = null!;

    public int ProductId { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
