namespace ReactPosApi.DTOs;

public class SaleDto
{
    public int Id { get; set; }
    /// <summary>Set when this sale was created by converting a quotation (its terms & conditions go on the receipt)</summary>
    public string? QuotationReference { get; set; }
    /// <summary>The source quotation's own terms & conditions, when they were edited for it</summary>
    public string? QuotationTerms { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string Biller { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ExpectedDate { get; set; }
    public string SaleDate { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    public List<SaleItemDto> Items { get; set; } = new();
    public List<SalePaymentDto> Payments { get; set; } = new();
}

public class SaleItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class SalePaymentDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
}

public class CreateSaleDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string Biller { get; set; } = "Admin";
    public decimal GrandTotal { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public string Source { get; set; } = "online";
    public string? OrderNumber { get; set; }
    public string? ExpectedDate { get; set; }
    public List<CreateSaleItemDto> Items { get; set; } = new();
}

public class CreateSaleItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>Convert a quotation into a sale: stock is deducted and the payment received now is recorded</summary>
public class ConvertQuotationToSaleDto
{
    /// <summary>Amount received from the customer now (0 = unpaid, the rest stays due)</summary>
    public decimal PaidAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Notes { get; set; }
}

/// <summary>Sale queue -> sale: the balance received from the customer when the order is converted</summary>
public class MarkSoldDto
{
    /// <summary>Amount received now (0 = nothing, the rest stays due)</summary>
    public decimal PaidAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
}

public class CreateSalePaymentDto
{
    public string Reference { get; set; } = string.Empty;
    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Description { get; set; }
}

/// <summary>Apply one payment across all outstanding POS (or other source) sales for a customer, oldest first (FIFO).</summary>
public class CustomerFifoPaymentRequestDto
{
    public int? CustomerId { get; set; }
    /// <summary>Required when CustomerId is null (walk-in / name-only match).</summary>
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Defaults to "pos". Only sales with this Source are included.</summary>
    public string Source { get; set; } = "pos";
}

public class FifoAllocationDto
{
    public int SaleId { get; set; }
    public string SaleReference { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}

public class CustomerFifoPaymentResponseDto
{
    public decimal TotalApplied { get; set; }
    public List<FifoAllocationDto> Allocations { get; set; } = new();
}

public class SalesReturnDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string ReturnDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    /// <summary>The POS sale this return belongs to</summary>
    public int? SaleId { get; set; }
    public string? SaleReference { get; set; }
    public List<SalesReturnItemDto> Items { get; set; } = new();
}

public class SalesReturnItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal NetUnitPrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Subtotal { get; set; }
}

public class CreateSalesReturnDto
{
    public string? Reference { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = "Pending";
    public string PaymentStatus { get; set; } = "Unpaid";
    public string? ReturnDate { get; set; }
    public List<CreateSalesReturnItemDto> Items { get; set; } = new();
}

public class CreateSalesReturnItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal NetUnitPrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Subtotal { get; set; }
}

public class QuotationDto
{
    public int Id { get; set; }
    /// <summary>From the customer's record (shown and printed on the quotation)</summary>
    public string? CustomerAddress { get; set; }
    public string? CustomerCity { get; set; }
    public string? CustomerPhone { get; set; }
    /// <summary>House, Villa, Apartment ...</summary>
    public string? CustomerBuildingType { get; set; }
    /// <summary>Size of the house, e.g. 10 Marla</summary>
    public string? CustomerBuildingSpace { get; set; }
    /// <summary>Condition / stage of the house, e.g. Under construction</summary>
    public string? CustomerBuildingStage { get; set; }
    public int? SaleId { get; set; }
    public string? SaleReference { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuotationDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    /// <summary>Terms edited for this quotation (null = standard terms); only filled when loading one quotation</summary>
    public string? TermsContent { get; set; }
    /// <summary>Revisions: original id (null on the original), 0/1/2... and the newer version that replaced this one</summary>
    public int? RootQuotationId { get; set; }
    public int RevisionNo { get; set; }
    public int? SupersededById { get; set; }
    /// <summary>Who made it (staff only see their own)</summary>
    public int? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    /// <summary>The maker's current role (looked up when listed)</summary>
    public string? CreatedByRole { get; set; }
    /// <summary>Staff approval: null, Pending, Approved, Discarded</summary>
    public string? ApprovalStatus { get; set; }
    public string? ApprovalSentTo { get; set; }
    public string? ApprovalBy { get; set; }
    public DateTime? ApprovalAt { get; set; }
    public string? ApprovalNote { get; set; }
    /// <summary>Number of product lines and their names (for the list, which has no line items)</summary>
    public int ItemCount { get; set; }
    public List<string> ProductNames { get; set; } = new();
    public List<QuotationItemDto> Items { get; set; } = new();
}

public class QuotationItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class CreateQuotationDto
{
    /// <summary>Set by the server from the signed-in user (not by the client)</summary>
    [System.Text.Json.Serialization.JsonIgnore] public int? CreatedById { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? CreatedByName { get; set; }
    public string? Reference { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Description { get; set; }
    public string? QuotationDate { get; set; }
    /// <summary>When sent, OrderTax and GrandTotal are calculated from these (base = items total - discount)</summary>
    public List<AppliedTaxDto>? Taxes { get; set; }
    /// <summary>Terms edited for this quotation. null = keep as is (standard on a new quotation); "" = back to the standard terms</summary>
    public string? TermsContent { get; set; }
    public List<CreateQuotationItemDto> Items { get; set; } = new();
}

public class CreateQuotationItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
