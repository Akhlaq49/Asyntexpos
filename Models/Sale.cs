using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Sale : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

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

    /// <summary>Expected payment date. When set, PaymentStatus starts as Pending.</summary>
    public DateTime? ExpectedDate { get; set; }

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    // ── Pharmacy counter fields ──
    /// <summary>Branch / store that made the sale.</summary>
    [MaxLength(100)]
    public string? Store { get; set; }
    public int? TillSessionId { get; set; }
    [MaxLength(50)]
    public string? CouponCode { get; set; }
    [MaxLength(30)]
    public string? PaymentMethod { get; set; }
    [MaxLength(200)]
    public string? PatientName { get; set; }
    [MaxLength(200)]
    public string? DoctorName { get; set; }
    [MaxLength(500)]
    public string? PrescriptionImage { get; set; }
    [MaxLength(100)]
    public string? FbrInvoiceNumber { get; set; }
    /// <summary>NotSent, Sent, Failed</summary>
    [MaxLength(20)]
    public string? FbrStatus { get; set; }
    public int? LocationId { get; set; }
    public int? PriceLevelId { get; set; }

    /// <summary>Customer balance just before this bill, frozen for reprints.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PreviousBalance { get; set; }

    [MaxLength(200)]
    public string? CreditOverrideBy { get; set; }

    /// <summary>POS | Web | Shopify</summary>
    [MaxLength(30)]
    public string Channel { get; set; } = "POS";

    /// <summary>Restaurant: DineIn | Takeaway | Delivery</summary>
    [MaxLength(20)]
    public string? OrderType { get; set; }
    public int? DiningTableId { get; set; }
    public int? Covers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SaleItem> Items { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

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

    // ── Pharmacy fields ──
    /// <summary>Average purchase cost per base unit taken from the batches sold (for margin).</summary>
    public decimal CostPrice { get; set; }
    /// <summary>Unit the cashier sold in: Unit, Strip or Box.</summary>
    [MaxLength(20)]
    public string SaleUnit { get; set; } = "Unit";
    /// <summary>Quantity in SaleUnit. Quantity holds the same amount in base units.</summary>
    public int UnitQuantity { get; set; }
    /// <summary>Batch numbers and expiries printed on the receipt, e.g. "PA2291 (08/2027)".</summary>
    [MaxLength(500)]
    public string? BatchInfo { get; set; }

    public int? VariantId { get; set; }

    /// <summary>Restaurant modifiers chosen for this line, as JSON.</summary>
    public string? ModifiersJson { get; set; }
}

public class SalePayment : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }

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

    /// <summary>Set when this payment is part of an on-account receipt or credit note.</summary>
    public int? CustomerReceiptId { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
