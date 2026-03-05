using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ReactPosApi.Data;

namespace ReactPosApi.Services;

/// <summary>
/// Executes tool calls requested by the LLM agent by querying the database.
/// </summary>
public interface IAgentToolExecutor
{
    Task<string> ExecuteAsync(string toolName, JsonElement arguments, int tenantId);
}

public class AgentToolExecutor : IAgentToolExecutor
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AgentToolExecutor(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<string> ExecuteAsync(string toolName, JsonElement arguments, int tenantId)
    {
        return toolName switch
        {
            "execute_sql_query" => await ExecuteSqlQuery(arguments, tenantId),
            "get_database_schema" => GetDatabaseSchema(),
            "get_sales_summary" => await GetSalesSummary(arguments),
            "get_today_sales" => await GetTodaySales(),
            "get_low_stock_products" => await GetLowStockProducts(arguments),
            "search_products" => await SearchProducts(arguments),
            "get_product_stock" => await GetProductStock(arguments),
            "get_customer_info" => await GetCustomerInfo(arguments),
            "get_overdue_installments" => await GetOverdueInstallments(arguments),
            "get_installment_plan_details" => await GetInstallmentPlanDetails(arguments),
            "get_daily_collection" => await GetDailyCollection(arguments),
            "get_expenses_summary" => await GetExpensesSummary(arguments),
            "get_top_selling_products" => await GetTopSellingProducts(arguments),
            "get_recent_purchases" => await GetRecentPurchases(arguments),
            "get_dashboard_stats" => await GetDashboardStats(),
            "get_customer_ledger" => await GetCustomerLedger(arguments),
            "get_defaulters" => await GetDefaulters(arguments),
            "get_employee_attendance" => await GetEmployeeAttendance(arguments),
            _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
        };
    }

    // ──────────────────── DYNAMIC SQL EXECUTION ────────────────────

    private async Task<string> ExecuteSqlQuery(JsonElement args, int tenantId)
    {
        var sql = GetStr(args, "query").Trim();
        if (string.IsNullOrWhiteSpace(sql))
            return ToJson(new { error = "No SQL query provided." });

        // Security: Only allow SELECT statements
        var normalized = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline); // strip line comments
        normalized = Regex.Replace(normalized, @"/\*.*?\*/", "", RegexOptions.Singleline); // strip block comments
        normalized = normalized.Trim();
        
        if (!normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return ToJson(new { error = "Only SELECT queries are allowed." });

        // Block dangerous keywords
        var forbidden = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "CALL" };
        foreach (var kw in forbidden)
        {
            if (Regex.IsMatch(normalized, $@"\b{kw}\b", RegexOptions.IgnoreCase))
                return ToJson(new { error = $"Forbidden keyword '{kw}' detected. Only SELECT queries are allowed." });
        }

        // Enforce LIMIT if not present
        if (!Regex.IsMatch(sql, @"\bLIMIT\b", RegexOptions.IgnoreCase))
            sql += " LIMIT 50";

        var connStr = _config.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@TenantId", tenantId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return ToJson(new { row_count = rows.Count, rows });
        }
        catch (Exception ex)
        {
            return ToJson(new { error = $"SQL execution error: {ex.Message}" });
        }
    }

    private string GetDatabaseSchema()
    {
        return AgentDbSchema.GetSchemaDescription();
    }

    private string GetStr(JsonElement args, string key, string? def = null)
    {
        if (args.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString()!;
        return def ?? "";
    }

    private int GetInt(JsonElement args, string key, int def = 0)
    {
        if (args.TryGetProperty(key, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number) return val.GetInt32();
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var i)) return i;
        }
        return def;
    }

    private DateTime? ParseDate(string? s) =>
        !string.IsNullOrEmpty(s) && DateTime.TryParse(s, out var d) ? d : null;

    private string ToJson(object obj) =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false });

    // ──────────────────── TOOL IMPLEMENTATIONS ────────────────────

    private async Task<string> GetSalesSummary(JsonElement args)
    {
        var from = ParseDate(GetStr(args, "from"));
        var to = ParseDate(GetStr(args, "to"));

        var sales = await _db.Sales.ToListAsync();
        if (from.HasValue) sales = sales.Where(s => s.SaleDate >= from.Value).ToList();
        if (to.HasValue) sales = sales.Where(s => s.SaleDate <= to.Value.AddDays(1)).ToList();

        return ToJson(new
        {
            total_sales_count = sales.Count,
            total_amount = sales.Sum(s => s.GrandTotal),
            total_paid = sales.Sum(s => s.Paid),
            total_unpaid = sales.Sum(s => s.Due),
            overdue = sales.Where(s => s.PaymentStatus == "Overdue").Sum(s => s.Due)
        });
    }

    private async Task<string> GetTodaySales()
    {
        var today = DateTime.UtcNow.Date;
        var sales = await _db.Sales.Where(s => s.SaleDate >= today && s.SaleDate < today.AddDays(1)).ToListAsync();
        return ToJson(new
        {
            date = today.ToString("yyyy-MM-dd"),
            count = sales.Count,
            total_amount = sales.Sum(s => s.GrandTotal),
            total_paid = sales.Sum(s => s.Paid),
            total_due = sales.Sum(s => s.Due)
        });
    }

    private async Task<string> GetLowStockProducts(JsonElement args)
    {
        var limit = GetInt(args, "limit", 20);
        var products = await _db.Products
            .Where(p => p.Quantity <= p.QuantityAlert)
            .OrderBy(p => p.Quantity)
            .Take(limit)
            .Select(p => new { p.Id, p.ProductName, p.SKU, p.Quantity, p.QuantityAlert, p.Category })
            .ToListAsync();
        return ToJson(new { count = products.Count, products });
    }

    private async Task<string> SearchProducts(JsonElement args)
    {
        var query = GetStr(args, "query").ToLower();
        var products = await _db.Products
            .Where(p => p.ProductName.ToLower().Contains(query)
                || p.SKU.ToLower().Contains(query)
                || p.Category.ToLower().Contains(query)
                || p.Brand.ToLower().Contains(query))
            .Take(20)
            .Select(p => new { p.Id, p.ProductName, p.SKU, p.Category, p.Brand, p.Price, p.Quantity })
            .ToListAsync();
        return ToJson(new { count = products.Count, products });
    }

    private async Task<string> GetProductStock(JsonElement args)
    {
        var query = GetStr(args, "query").ToLower();
        var product = await _db.Products
            .Where(p => p.ProductName.ToLower().Contains(query) || p.SKU.ToLower().Contains(query))
            .Select(p => new { p.Id, p.ProductName, p.SKU, p.Quantity, p.Price, p.Category, p.QuantityAlert })
            .FirstOrDefaultAsync();
        if (product == null) return ToJson(new { error = "Product not found." });
        return ToJson(product);
    }

    private async Task<string> GetCustomerInfo(JsonElement args)
    {
        var query = GetStr(args, "query");
        var customers = await _db.Parties
            .Where(p => (p.Role == "Customer" || p.Role == "customer")
                && (p.FullName.ToLower().Contains(query.ToLower())
                    || p.Phone.Contains(query)
                    || p.Id.ToString() == query))
            .Take(5)
            .ToListAsync();

        var results = new List<object>();
        foreach (var c in customers)
        {
            var plans = await _db.InstallmentPlans
                .Include(p => p.Product)
                .Where(p => p.CustomerId == c.Id)
                .Select(p => new { p.Id, p.Product!.ProductName, p.Status, p.TotalPayable, p.DownPayment, p.FinancedAmount, p.StartDate })
                .ToListAsync();

            results.Add(new
            {
                c.Id, c.FullName, c.Phone, c.Email, c.Address,
                installment_plans = plans
            });
        }
        if (results.Count == 0) return ToJson(new { error = "No customer found." });
        return ToJson(results);
    }

    private async Task<string> GetOverdueInstallments(JsonElement args)
    {
        var limit = GetInt(args, "limit", 20);
        var entries = await _db.RepaymentEntries
            .Include(e => e.Plan).ThenInclude(p => p!.Customer)
            .Include(e => e.Plan).ThenInclude(p => p!.Product)
            .Where(e => e.Status == "overdue" && e.Plan!.Status == "active")
            .Take(limit)
            .ToListAsync();

        var items = entries.Select(e => new
        {
            plan_id = e.PlanId,
            installment_no = e.InstallmentNo,
            customer = e.Plan?.Customer?.FullName ?? "Unknown",
            phone = e.Plan?.Customer?.Phone,
            product = e.Plan?.Product?.ProductName ?? "Unknown",
            due_date = e.DueDate,
            amount_due = e.EmiAmount - (e.ActualPaidAmount ?? 0) - (e.MiscAdjustedAmount ?? 0)
        });
        return ToJson(new { count = entries.Count, overdue_entries = items });
    }

    private async Task<string> GetInstallmentPlanDetails(JsonElement args)
    {
        var planId = GetInt(args, "plan_id");
        var plan = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product)
            .Include(p => p.Schedule)
            .Include(p => p.PlanGuarantors)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null) return ToJson(new { error = "Plan not found." });

        return ToJson(new
        {
            plan.Id, plan.Status, plan.StartDate,
            customer = plan.Customer?.FullName, phone = plan.Customer?.Phone,
            product = plan.Product?.ProductName,
            plan.ProductPrice, plan.DownPayment, plan.FinancedAmount,
            plan.TotalPayable, plan.TotalInterest, plan.InterestRate, plan.Tenure,
            schedule = plan.Schedule.OrderBy(s => s.InstallmentNo).Select(s => new
            {
                s.InstallmentNo, s.DueDate, s.EmiAmount, s.Status,
                paid = s.Status == "paid" ? s.EmiAmount
                     : s.Status == "partial" ? (s.ActualPaidAmount ?? 0) + (s.MiscAdjustedAmount ?? 0)
                     : 0m,
                s.PaidDate
            })
        });
    }

    private async Task<string> GetDailyCollection(JsonElement args)
    {
        var dateStr = GetStr(args, "date");
        var date = ParseDate(dateStr) ?? DateTime.UtcNow;
        var dayStr = date.ToString("yyyy-MM-dd");

        var entries = await _db.RepaymentEntries
            .Where(e => e.PaidDate != null && e.PaidDate.StartsWith(dayStr) && (e.Status == "paid" || e.Status == "partial"))
            .ToListAsync();

        var paidTotal = entries.Where(e => e.Status == "paid").Sum(e => e.EmiAmount);
        var partialTotal = entries.Where(e => e.Status == "partial").Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

        var plans = await _db.InstallmentPlans
            .Where(p => p.CreatedAt.Date == date.Date)
            .ToListAsync();
        var downPayments = plans.Sum(p => p.DownPayment);

        return ToJson(new
        {
            date = dayStr,
            entries_collected = entries.Count,
            paid_amount = paidTotal,
            partial_amount = partialTotal,
            total_collected = paidTotal + partialTotal,
            down_payments = downPayments,
            grand_total = paidTotal + partialTotal + downPayments
        });
    }

    private async Task<string> GetExpensesSummary(JsonElement args)
    {
        var from = ParseDate(GetStr(args, "from"));
        var to = ParseDate(GetStr(args, "to"));

        var expenses = await _db.Expenses.Include(e => e.ExpenseCategory).ToListAsync();
        if (from.HasValue) expenses = expenses.Where(e => e.Date >= from.Value).ToList();
        if (to.HasValue) expenses = expenses.Where(e => e.Date <= to.Value.AddDays(1)).ToList();

        var byCategory = expenses.GroupBy(e => e.ExpenseCategory?.Name ?? "Uncategorized")
            .Select(g => new { category = g.Key, total = g.Sum(e => e.Amount), count = g.Count() });

        return ToJson(new
        {
            total_expenses = expenses.Sum(e => e.Amount),
            count = expenses.Count,
            by_category = byCategory
        });
    }

    private async Task<string> GetTopSellingProducts(JsonElement args)
    {
        var limit = GetInt(args, "limit", 10);
        var from = ParseDate(GetStr(args, "from"));
        var to = ParseDate(GetStr(args, "to"));

        var saleItems = await _db.SaleItems.Include(si => si.Sale).ToListAsync();
        if (from.HasValue) saleItems = saleItems.Where(si => si.Sale?.SaleDate >= from.Value).ToList();
        if (to.HasValue) saleItems = saleItems.Where(si => si.Sale?.SaleDate <= to.Value.AddDays(1)).ToList();

        var top = saleItems.GroupBy(si => new { si.ProductId, si.ProductName })
            .Select(g => new { g.Key.ProductName, qty_sold = g.Sum(i => i.Quantity), revenue = g.Sum(i => i.TotalCost) })
            .OrderByDescending(x => x.qty_sold)
            .Take(limit);

        return ToJson(new { top_products = top });
    }

    private async Task<string> GetRecentPurchases(JsonElement args)
    {
        var limit = GetInt(args, "limit", 10);
        var purchases = await _db.Purchases
            .OrderByDescending(p => p.Date)
            .Take(limit)
            .Select(p => new { p.Id, p.Reference, p.SupplierName, p.Date, p.Total, p.Paid, due = p.Total - p.Paid, p.Status })
            .ToListAsync();
        return ToJson(new { count = purchases.Count, purchases });
    }

    private async Task<string> GetDashboardStats()
    {
        var today = DateTime.UtcNow.Date;
        var totalProducts = await _db.Products.CountAsync();
        var totalCustomers = await _db.Parties.CountAsync(p => p.Role == "Customer" || p.Role == "customer");
        var totalSales = await _db.Sales.CountAsync();
        var todaySales = await _db.Sales.Where(s => s.SaleDate >= today).SumAsync(s => s.GrandTotal);
        var activePlans = await _db.InstallmentPlans.CountAsync(p => p.Status == "active");
        var overdue = await _db.RepaymentEntries.CountAsync(e => e.Status == "overdue");
        var lowStock = await _db.Products.CountAsync(p => p.Quantity <= p.QuantityAlert);
        var totalRevenue = await _db.Sales.SumAsync(s => s.GrandTotal);

        return ToJson(new
        {
            total_products = totalProducts,
            total_customers = totalCustomers,
            total_sales = totalSales,
            today_sales_amount = todaySales,
            active_installment_plans = activePlans,
            overdue_installments = overdue,
            low_stock_products = lowStock,
            total_revenue = totalRevenue
        });
    }

    private async Task<string> GetCustomerLedger(JsonElement args)
    {
        var customerId = GetInt(args, "customer_id");
        var customer = await _db.Parties.FirstOrDefaultAsync(p => p.Id == customerId);
        if (customer == null) return ToJson(new { error = "Customer not found." });

        var plans = await _db.InstallmentPlans
            .Include(p => p.Product)
            .Include(p => p.Schedule)
            .Where(p => p.CustomerId == customerId)
            .ToListAsync();

        var transactions = new List<object>();
        foreach (var plan in plans)
        {
            transactions.Add(new { type = "Purchase", description = $"Plan #{plan.Id} - {plan.Product?.ProductName}", debit = plan.TotalPayable, credit = 0m });
            transactions.Add(new { type = "Down Payment", description = $"Plan #{plan.Id}", debit = 0m, credit = plan.DownPayment });
            foreach (var e in plan.Schedule.Where(s => s.Status == "paid" || s.Status == "partial").OrderBy(s => s.InstallmentNo))
            {
                var amt = e.Status == "paid" ? e.EmiAmount : (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0);
                transactions.Add(new { type = "Installment", description = $"Plan #{plan.Id} Inst #{e.InstallmentNo}", debit = 0m, credit = amt });
            }
        }

        var totalPurchases = plans.Sum(p => p.TotalPayable);
        var totalPaid = plans.Sum(p => p.DownPayment + p.Schedule.Where(e => e.Status == "paid" || e.Status == "partial")
            .Sum(e => e.Status == "paid" ? e.EmiAmount : (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0)));

        return ToJson(new
        {
            customer = customer.FullName, phone = customer.Phone,
            total_purchases = totalPurchases, total_paid = totalPaid,
            remaining = totalPurchases - totalPaid,
            transactions
        });
    }

    private async Task<string> GetDefaulters(JsonElement args)
    {
        var limit = GetInt(args, "limit", 20);
        var plans = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product)
            .Include(p => p.Schedule)
            .Where(p => p.Status == "active")
            .ToListAsync();

        var defaulters = plans
            .Where(p => p.Schedule.Any(e => e.Status == "overdue"))
            .Select(p =>
            {
                var overdue = p.Schedule.Where(e => e.Status == "overdue").ToList();
                return new
                {
                    plan_id = p.Id,
                    customer = p.Customer?.FullName ?? "Unknown",
                    phone = p.Customer?.Phone,
                    product = p.Product?.ProductName ?? "Unknown",
                    missed_installments = overdue.Count,
                    overdue_amount = overdue.Sum(e => e.EmiAmount - (e.ActualPaidAmount ?? 0) - (e.MiscAdjustedAmount ?? 0))
                };
            })
            .OrderByDescending(d => d.overdue_amount)
            .Take(limit)
            .ToList();

        return ToJson(new { count = defaulters.Count, defaulters });
    }

    private async Task<string> GetEmployeeAttendance(JsonElement args)
    {
        var dateStr = GetStr(args, "date");
        var date = ParseDate(dateStr) ?? DateTime.UtcNow;

        var records = await _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.Date.Date == date.Date)
            .Select(a => new { EmployeeName = a.Employee != null ? a.Employee.FullName : "Unknown", a.Date, a.ClockIn, a.ClockOut, a.Status, a.TotalHours })
            .ToListAsync();

        return ToJson(new { date = date.ToString("yyyy-MM-dd"), count = records.Count, attendance = records });
    }
}
