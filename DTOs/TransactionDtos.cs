namespace ReactPosApi.DTOs;

/// <summary>One money movement: sale payment (in), supplier payment (out), supplier refund (in), expense (out)</summary>
public class TransactionDto
{
    /// <summary>Unique across sources, e.g. "SP-12" (sale payment 12), "PP-4" (purchase payment 4), "EX-9"</summary>
    public string Key { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    /// <summary>Sale payment, Supplier payment, Supplier refund, Expense</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>"In" (money received) or "Out" (money paid)</summary>
    public string Direction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    /// <summary>Reference of the transaction itself (payment ref)</summary>
    public string? Reference { get; set; }
    /// <summary>The document it belongs to: sale, purchase receipt or expense reference</summary>
    public string? Document { get; set; }
    public string? PurchaseOrderNo { get; set; }
    public int? PurchaseOrderId { get; set; }
    /// <summary>Customer, supplier or expense name</summary>
    public string? Party { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}

public class TransactionsSummaryDto
{
    public decimal MoneyIn { get; set; }
    public decimal MoneyOut { get; set; }
    public decimal Net { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}
