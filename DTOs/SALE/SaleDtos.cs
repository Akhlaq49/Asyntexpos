namespace ReactPosApi.DTOs;

public class SaleDto
{
    public int Id { get; set; }
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
