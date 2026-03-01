using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class SalesReturn
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

    // Product shown in table header row
    public int? ProductId { get; set; }

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? ProductImage { get; set; }

    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, Received

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Unpaid"; // Paid, Unpaid, Overdue

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SalesReturnItem> Items { get; set; } = new();
}

public class SalesReturnItem
{
    public int Id { get; set; }

    public int SalesReturnId { get; set; }
    public SalesReturn SalesReturn { get; set; } = null!;

    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public decimal NetUnitPrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Subtotal { get; set; }
}
