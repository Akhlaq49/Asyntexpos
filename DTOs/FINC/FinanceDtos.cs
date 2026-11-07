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
