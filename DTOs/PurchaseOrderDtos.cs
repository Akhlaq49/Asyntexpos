namespace ReactPosApi.DTOs;

public class PurchaseOrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ProductImage { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RemainingQuantity => Math.Max(0, Quantity - ReceivedQuantity);
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalCost { get; set; }
    /// <summary>Current stock of the product</summary>
    public int InStock { get; set; }
}

/// <summary>A receipt = one Purchase created by receiving goods against the order</summary>
public class PurchaseOrderReceiptDto
{
    public int PurchaseId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int Units { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    /// <summary>Total - Paid (negative = paid in advance / overpaid)</summary>
    public decimal Due { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    /// <summary>Every payment and refund on this receipt, oldest first</summary>
    public List<PurchasePaymentDto> Payments { get; set; } = new();
}

/// <summary>One payment to the supplier (money out) or refund from the supplier (money in)</summary>
public class PurchasePaymentDto
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SavePurchasePaymentDto
{
    /// <summary>"Payment" (pay the supplier) or "Refund" (money back from the supplier)</summary>
    public string Type { get; set; } = "Payment";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public DateTime? Date { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public int OrderedUnits { get; set; }
    public int ReceivedUnits { get; set; }
    public decimal ReceivedValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
    public List<PurchaseOrderReceiptDto> Receipts { get; set; } = new();
    /// <summary>Revisions: original id (null on the original), 0/1/2..., and the newer version (if any)</summary>
    public int? RootPurchaseOrderId { get; set; }
    public int RevisionNo { get; set; }
    public int? SupersededById { get; set; }
    /// <summary>Paid to the supplier across all receipts (payments - refunds)</summary>
    public decimal PaidTotal { get; set; }
    /// <summary>The converted quotation / sale this order buys goods for (null = stock order)</summary>
    public int? QuotationId { get; set; }
    public string? QuotationReference { get; set; }
    public string? SaleReference { get; set; }
}

public class SavePurchaseOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
}

public class SavePurchaseOrderDto
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    /// <summary>"Draft" (save only) or "Ordered" (save and mark as sent to supplier)</summary>
    public string Status { get; set; } = "Draft";
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public string? Notes { get; set; }
    /// <summary>When sent, OrderTax is their total (base = items total - discount)</summary>
    public List<AppliedTaxDto>? Taxes { get; set; }
    public List<SavePurchaseOrderItemDto> Items { get; set; } = new();
    /// <summary>Create only: the converted quotation this order buys goods for</summary>
    public int? QuotationId { get; set; }
}

public class ReceivePurchaseOrderLineDto
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
}

public class ReceivePurchaseOrderDto
{
    public DateTime? Date { get; set; }
    /// <summary>Amount paid to the supplier for this delivery</summary>
    public decimal Paid { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
    public List<ReceivePurchaseOrderLineDto> Lines { get; set; } = new();
}
