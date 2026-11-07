using System.Text;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;

namespace ReactPosApi.Services;

/// <summary>
/// The database schema the AI agent writes its SELECT queries against: built from the EF model, so the table names
/// (Data/DbNames.cs) and columns are always the real ones.
/// </summary>
public static class AgentDbSchema
{
    public static string GetSchemaDescription(DbContext db)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DATABASE SCHEMA (MySQL) ===");
        sb.AppendLine("Table names: MODULE_ENTITY, 4 letters each (SYST system, SECU security, PRTY parties, INVT inventory, SALE sales,");
        sb.AppendLine("PRCH purchases, FINC finance, ACCT accounting, CATL product catalogue). Most tables have TenantId and IsDeleted.");
        sb.AppendLine();
        foreach (var entity in db.Model.GetEntityTypes().Where(e => !e.IsOwned()).OrderBy(e => e.GetTableName()))
        {
            var columns = entity.GetProperties().Select(p =>
            {
                var fk = p.GetContainingForeignKeys().FirstOrDefault();
                var target = fk?.PrincipalEntityType.GetTableName();
                return $"{p.GetColumnName()} {p.ClrType.Name.Replace("Nullable`1", "").Replace("String", "text")}"
                    + (p.IsPrimaryKey() ? " PK" : "") + (target != null ? $" -> {target}" : "");
            });
            sb.AppendLine($"TABLE: {entity.GetTableName()} ({entity.ClrType.Name})");
            sb.AppendLine("  " + string.Join(", ", columns));
        }
        sb.AppendLine();
        sb.AppendLine("=== RULES ===");
        sb.AppendLine("1. ALWAYS include WHERE TenantId = @TenantId (and IsDeleted = 0 where the table has it) in every query");
        sb.AppendLine("2. Use LEFT JOIN when data might not exist");
        sb.AppendLine("3. For today's date use CURDATE() (MySQL)");
        sb.AppendLine("4. Only generate SELECT queries - never INSERT/UPDATE/DELETE");
        sb.AppendLine("5. Limit results to prevent huge responses (use LIMIT)");
        return sb.ToString();
    }
}
