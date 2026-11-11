namespace ReactPosApi.DTOs;

// ── Locations ──
public class LocationDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Branch";
    public int? ParentId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? DrugLicenceNo { get; set; }
    public bool IsDefault { get; set; }
    public bool SellsOnline { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockLevelDto
{
    public int ProductId { get; set; }
    public int MinQty { get; set; }
    public int MaxQty { get; set; }
}

// ── Requisitions ──
public class StockRequestDto
{
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public string? Notes { get; set; }
    public bool Submit { get; set; } = true;
    public List<StockRequestItemDto> Items { get; set; } = new();
}

public class StockRequestItemDto
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int RequestedQty { get; set; }
}

public class ApproveStockRequestDto
{
    public bool Dispatch { get; set; } = true;
    public List<ApproveStockRequestLineDto> Lines { get; set; } = new();
}

public class ApproveStockRequestLineDto
{
    public int ItemId { get; set; }
    public int ApprovedQty { get; set; }
}

public class RejectDto
{
    public string? Reason { get; set; }
}

// ── Wholesale and credit ──
public class PriceLevelDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class ProductPriceDto
{
    public int? Id { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int PriceLevelId { get; set; }
    public string SaleUnit { get; set; } = "Unit";
    public int MinQty { get; set; } = 1;
    public decimal Price { get; set; }
}

public class CustomerAccountDto
{
    public string? CustomerType { get; set; }
    public int? PriceLevelId { get; set; }
    public decimal? CreditLimit { get; set; }
    public int? CreditDays { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public bool CreditBlocked { get; set; }
}

public class CustomerReceiptDto
{
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? ChequeNo { get; set; }
    public string? ChequeDate { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptDate { get; set; }
    public int? LocationId { get; set; }
}

public class CustomerAdjustmentDto
{
    public int CustomerId { get; set; }
    /// <summary>Opening | Adjustment | WriteOff</summary>
    public string EntryType { get; set; } = "Adjustment";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Narration { get; set; }
    public string? EntryDate { get; set; }
}

// ── Translations ──
public class ProductTranslationDto
{
    public string Lang { get; set; } = "ur";
    public string Name { get; set; } = string.Empty;
    public string? UsageNote { get; set; }
    public string? Description { get; set; }
}
