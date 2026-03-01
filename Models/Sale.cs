using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Sale
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

    [MaxLength(100)]
    public string Biller { get; set; } = "Admin";

    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }

    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Pending";

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Unpaid";

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(20)]
    public string Source { get; set; } = "online";

    /// <summary>Customer-facing order number for online orders (e.g. ONL-1234567)</summary>
    [MaxLength(50)]
    public string? OrderNumber { get; set; }

    /// <summary>FK to PartyAddress for billing (online orders)</summary>
    public int? BillingAddressId { get; set; }
    public PartyAddress? BillingAddress { get; set; }

    /// <summary>FK to PartyAddress for shipping (online orders)</summary>
    public int? ShippingAddressId { get; set; }
    public PartyAddress? ShippingAddress { get; set; }

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SaleItem> Items { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

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

public class SalePayment
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }

    [MaxLength(50)]
    public string PaymentType { get; set; } = "Cash";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
