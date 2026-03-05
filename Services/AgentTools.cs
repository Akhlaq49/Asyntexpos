using System.Text.Json.Serialization;

namespace ReactPosApi.Services;

/// <summary>
/// Defines a tool the LLM can call, following OpenAI function-calling schema.
/// </summary>
public static class AgentTools
{
    public static List<object> GetToolDefinitions() => new()
    {
        MakeTool("get_database_schema", "Get the full database schema with all table definitions, columns, types, foreign keys, and relationships. Call this FIRST before writing any SQL query so you know the exact table and column names.",
            new { type = "object", properties = new {} }),

        MakeTool("execute_sql_query", "Execute a read-only SQL SELECT query against the MySQL database. ALWAYS use WHERE TenantId = @TenantId for tenant isolation. Use proper JOINs for related data. Use LIMIT to prevent huge results. Only SELECT is allowed. IMPORTANT: For RepaymentEntries paid amounts, if Status='paid' use EmiAmount, if Status='partial' use COALESCE(ActualPaidAmount,0)+COALESCE(MiscAdjustedAmount,0).",
            new { type = "object", properties = new {
                query = new { type = "string", description = "The SQL SELECT query to execute. MUST include WHERE TenantId = @TenantId in every table reference. Use JOINs for related data. For RepaymentEntries paid amount use: CASE WHEN re.Status='paid' THEN re.EmiAmount WHEN re.Status='partial' THEN COALESCE(re.ActualPaidAmount,0)+COALESCE(re.MiscAdjustedAmount,0) ELSE 0 END" }
            }, required = new[] { "query" } }),

        MakeTool("get_sales_summary", "Get sales summary for a date range. Returns total sales, paid, unpaid, overdue amounts.",
            new { type = "object", properties = new {
                from = new { type = "string", description = "Start date (yyyy-MM-dd). Optional." },
                to = new { type = "string", description = "End date (yyyy-MM-dd). Optional." }
            }}),

        MakeTool("get_today_sales", "Get today's sales count and total amount.", new { type = "object", properties = new {} }),

        MakeTool("get_low_stock_products", "Get products that are at or below their stock alert quantity.",
            new { type = "object", properties = new {
                limit = new { type = "integer", description = "Max number of products to return. Default 20." }
            }}),

        MakeTool("search_products", "Search products by name, SKU, category, or brand.",
            new { type = "object", properties = new {
                query = new { type = "string", description = "Search term for product name, SKU, category, or brand." }
            }, required = new[] { "query" } }),

        MakeTool("get_product_stock", "Get current stock/quantity for a specific product by name or SKU.",
            new { type = "object", properties = new {
                query = new { type = "string", description = "Product name or SKU to look up." }
            }, required = new[] { "query" } }),

        MakeTool("get_customer_info", "Get customer details and their installment plans by name, phone, or ID.",
            new { type = "object", properties = new {
                query = new { type = "string", description = "Customer name, phone number, or ID." }
            }, required = new[] { "query" } }),

        MakeTool("get_overdue_installments", "Get all overdue installment entries with customer details.",
            new { type = "object", properties = new {
                limit = new { type = "integer", description = "Max results. Default 20." }
            }}),

        MakeTool("get_installment_plan_details", "Get full details of a specific installment plan by plan ID.",
            new { type = "object", properties = new {
                plan_id = new { type = "integer", description = "The installment plan ID." }
            }, required = new[] { "plan_id" } }),

        MakeTool("get_daily_collection", "Get today's or a specific date's installment collection summary.",
            new { type = "object", properties = new {
                date = new { type = "string", description = "Date (yyyy-MM-dd). Defaults to today." }
            }}),

        MakeTool("get_expenses_summary", "Get expense summary for a date range.",
            new { type = "object", properties = new {
                from = new { type = "string", description = "Start date (yyyy-MM-dd). Optional." },
                to = new { type = "string", description = "End date (yyyy-MM-dd). Optional." }
            }}),

        MakeTool("get_top_selling_products", "Get top selling products by quantity or revenue.",
            new { type = "object", properties = new {
                limit = new { type = "integer", description = "Number of top products. Default 10." },
                from = new { type = "string", description = "Start date. Optional." },
                to = new { type = "string", description = "End date. Optional." }
            }}),

        MakeTool("get_recent_purchases", "Get recent purchase orders.",
            new { type = "object", properties = new {
                limit = new { type = "integer", description = "Max results. Default 10." }
            }}),

        MakeTool("get_dashboard_stats", "Get overall dashboard statistics: total sales, purchases, customers, products, active plans, etc.",
            new { type = "object", properties = new {} }),

        MakeTool("get_customer_ledger", "Get full ledger (all transactions) for a customer by customer ID.",
            new { type = "object", properties = new {
                customer_id = new { type = "integer", description = "Customer ID." }
            }, required = new[] { "customer_id" } }),

        MakeTool("get_defaulters", "Get list of customers with overdue installments (defaulters).",
            new { type = "object", properties = new {
                limit = new { type = "integer", description = "Max results. Default 20." }
            }}),

        MakeTool("get_employee_attendance", "Get attendance records for today or a date.",
            new { type = "object", properties = new {
                date = new { type = "string", description = "Date (yyyy-MM-dd). Defaults to today." }
            }}),
    };

    private static object MakeTool(string name, string description, object parameters) => new
    {
        type = "function",
        function = new { name, description, parameters }
    };
}
