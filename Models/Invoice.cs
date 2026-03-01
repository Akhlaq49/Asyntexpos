using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Invoice
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Party? Customer { get; set; }

    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? CustomerImage { get; set; }

    [MaxLength(200)]
    public string? CustomerAddress { get; set; }

    [MaxLength(100)]
    public string? CustomerEmail { get; set; }

    [MaxLength(50)]
    public string? CustomerPhone { get; set; }

    [MaxLength(100)]
    public string FromName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FromAddress { get; set; }

    [MaxLength(100)]
    public string? FromEmail { get; set; }

    [MaxLength(50)]
    public string? FromPhone { get; set; }

    [MaxLength(200)]
    public string? InvoiceFor { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Tax { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public decimal AmountDue { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Unpaid";

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Terms { get; set; }

    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InvoiceItem> Items { get; set; } = new();
}

public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public decimal Cost { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}
