namespace ReactPosApi.DTOs;

/// <summary>
/// DTO for a purchase line item (Product in purchase)
/// </summary>
public class PurchaseItemDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// DTO for reading purchase data (response)
/// </summary>
public class PurchaseDto
{
    public int Id { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierRef { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Received";
    public string PaymentStatus { get; set; } = "Unpaid";
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Due => Total - Paid;
    public string? Notes { get; set; }
    public List<PurchaseItemDto>? Items { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new purchase line item
/// </summary>
public class CreatePurchaseItemDto
{
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; } = 0;
    public decimal TaxPercentage { get; set; } = 0;
    public decimal TaxAmount { get; set; } = 0;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// DTO for creating a new purchase
/// </summary>
public class CreatePurchaseDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierRef { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Received";
    public decimal OrderTax { get; set; } = 0;
    public decimal Discount { get; set; } = 0;
    public decimal Shipping { get; set; } = 0;
    public decimal Total { get; set; }
    public decimal Paid { get; set; } = 0;
    public string? Notes { get; set; }
    public List<CreatePurchaseItemDto>? Items { get; set; }
}

/// <summary>
/// DTO for updating an existing purchase
/// </summary>
public class UpdatePurchaseDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierRef { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Received";
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseItemDto>? Items { get; set; }
}

