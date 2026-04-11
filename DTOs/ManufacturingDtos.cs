namespace ReactPosApi.DTOs;

// ────────────────────────────────────────────
// Bill of Materials DTOs
// ────────────────────────────────────────────

public class BomItemDto
{
    public int Id { get; set; }
    public int RawMaterialId { get; set; }
    public string RawMaterialName { get; set; } = string.Empty;
    public string? RawMaterialSku { get; set; }
    public string? RawMaterialImage { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}

public class BomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? FinishedProductId { get; set; }
    public string FinishedProductName { get; set; } = string.Empty;
    public string? FinishedProductCategory { get; set; }
    public string? FinishedProductSubCategory { get; set; }
    public decimal SalePrice { get; set; }
    public string? FinishedProductSku { get; set; }
    public string? FinishedProductImage { get; set; }
    public int OutputQuantity { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal TotalMaterialCost => Items?.Sum(i => i.TotalCost) ?? 0;
    public decimal TotalCost => TotalMaterialCost + LaborCost + OverheadCost;
    public string? Notes { get; set; }
    public string Status { get; set; } = "active";
    public List<BomItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateBomItemDto
{
    public int RawMaterialId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitCost { get; set; }
    public int? SupplierId { get; set; }
}

public class CreateBomDto
{
    public string Name { get; set; } = string.Empty;
    public string? FinishedProductName { get; set; }
    public string? FinishedProductCategory { get; set; }
    public string? FinishedProductSubCategory { get; set; }
    public decimal SalePrice { get; set; }
    public int OutputQuantity { get; set; } = 1;
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "active";
    public List<CreateBomItemDto> Items { get; set; } = new();
}

public class UpdateBomDto : CreateBomDto { }

// ────────────────────────────────────────────
// Manufacturing Order DTOs
// ────────────────────────────────────────────

public class ManufacturingOrderItemDto
{
    public int Id { get; set; }
    public int RawMaterialId { get; set; }
    public string RawMaterialName { get; set; } = string.Empty;
    public string? RawMaterialSku { get; set; }
    public string? RawMaterialImage { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}

public class ManufacturingOrderDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int BomId { get; set; }
    public string BomName { get; set; } = string.Empty;
    public int? FinishedProductId { get; set; }
    public string FinishedProductName { get; set; } = string.Empty;
    public string? FinishedProductImage { get; set; }
    public int Quantity { get; set; }
    public int? TargetStoreId { get; set; }
    public string? TargetStoreName { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal TotalMaterialCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? Notes { get; set; }
    public List<ManufacturingOrderItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateManufacturingOrderItemDto
{
    public int RawMaterialId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public int? SupplierId { get; set; }
}

public class CreateManufacturingOrderDto
{
    public string Reference { get; set; } = string.Empty;
    public int BomId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? TargetStoreId { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public string? Notes { get; set; }
    public List<CreateManufacturingOrderItemDto> Items { get; set; } = new();
}

public class UpdateManufacturingOrderDto : CreateManufacturingOrderDto { }

// ────────────────────────────────────────────
// Supplier Ledger DTOs
// ────────────────────────────────────────────

public class SupplierLedgerEntryDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public int? ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSupplierLedgerEntryDto
{
    public int SupplierId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public int? ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
}

// ────────────────────────────────────────────
// Supplier Payment DTOs
// ────────────────────────────────────────────

public class SupplierPaymentDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Description { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSupplierPaymentDto
{
    public int SupplierId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Description { get; set; }
    public DateTime? PaymentDate { get; set; }
}

// ────────────────────────────────────────────
// Supplier Balance Summary
// ────────────────────────────────────────────

public class SupplierBalanceDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierPhone { get; set; }
    public string? SupplierEmail { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal Balance { get; set; }
}
