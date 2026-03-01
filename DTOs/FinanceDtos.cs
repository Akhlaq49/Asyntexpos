namespace ReactPosApi.DTOs;

// ── Expense Category ──
public class ExpenseCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateExpenseCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ── Expense ──
public class ExpenseDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string ExpenseName { get; set; } = string.Empty;
    public int ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "active";
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateExpenseDto
{
    public string ExpenseName { get; set; } = string.Empty;
    public int ExpenseCategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "active";
}

// ── Income Category ──
public class IncomeCategoryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateIncomeCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// ── Finance Income ──
public class FinanceIncomeDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public int IncomeCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Account { get; set; } = string.Empty;
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateFinanceIncomeDto
{
    public DateTime Date { get; set; }
    public string Store { get; set; } = string.Empty;
    public int IncomeCategoryId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Account { get; set; } = string.Empty;
}

// ── Account Type ──
public class AccountTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateAccountTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ── Bank Account ──
public class BankAccountDto
{
    public int Id { get; set; }
    public string HolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public int AccountTypeId { get; set; }
    public string AccountTypeName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public bool IsDefault { get; set; }
    public string CreatedOn { get; set; } = string.Empty;
}

public class CreateBankAccountDto
{
    public string HolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public int AccountTypeId { get; set; }
    public decimal OpeningBalance { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public bool IsDefault { get; set; }
}

// ── Finance Report DTOs ──
public class BalanceSheetDto
{
    public List<BalanceSheetLineItem> Assets { get; set; } = new();
    public decimal TotalAssets { get; set; }
    public List<BalanceSheetLineItem> Liabilities { get; set; } = new();
    public decimal TotalLiabilities { get; set; }
    public decimal Equity { get; set; }
}

public class BalanceSheetLineItem
{
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TrialBalanceDto
{
    public List<TrialBalanceLineItem> Items { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

public class TrialBalanceLineItem
{
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class CashFlowDto
{
    public List<CashFlowLineItem> OperatingActivities { get; set; } = new();
    public decimal TotalOperating { get; set; }
    public List<CashFlowLineItem> InvestingActivities { get; set; } = new();
    public decimal TotalInvesting { get; set; }
    public List<CashFlowLineItem> FinancingActivities { get; set; } = new();
    public decimal TotalFinancing { get; set; }
    public decimal NetCashFlow { get; set; }
}

public class CashFlowLineItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class AccountStatementDto
{
    public List<AccountStatementEntry> Entries { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class AccountStatementEntry
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}
