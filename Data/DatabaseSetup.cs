using Microsoft.EntityFrameworkCore;

namespace ReactPosApi.Data;

/// <summary>
/// Runs on start: a new database is created from the model (EnsureCreated), then the module scripts add what EF
/// can't describe - procedures, composite foreign keys, generated columns and views. The scripts are idempotent.
/// A database that still has the table names from before the naming convention is refused: it must be migrated
/// first with Database/Migrations (run by a person, never by the app).
/// </summary>
public static class DatabaseSetup
{
    /// <summary>Embedded scripts, in the order they run</summary>
    public static readonly string[] Scripts = { "SYST/Procedures.sql", "INVT/Schema.sql", "CATL/Schema.sql", "ACCT/Schema.sql" };

    public static void Apply(AppDbContext db)
    {
        var legacy = LegacyTables(db);
        if (legacy.Count > 0)
            throw new InvalidOperationException(
                $"The database still has tables with their old names ({string.Join(", ", legacy.Take(5))}{(legacy.Count > 5 ? " ..." : "")}). " +
                "Run Database/Migrations/2026-11-07-01-drop-unused-tables.sql and 2026-11-07-02-rename-tables.sql first.");
        db.Database.EnsureCreated();
        var connection = db.Database.GetConnectionString()!;
        foreach (var script in Scripts) SqlScript.RunResource(connection, script);
    }

    /// <summary>Tables of this database that still have a name from before the naming convention</summary>
    public static List<string> LegacyTables(AppDbContext db)
    {
        if (!db.Database.CanConnect()) return new List<string>();
        var names = db.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME AS `Value` FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'").ToList();
        return FindLegacy(names);
    }

    /// <summary>The names in the list that are table names from before the naming convention (any letter case:
    /// lower_case_table_names = 1 stores them in lower case)</summary>
    public static List<string> FindLegacy(IEnumerable<string> tableNames)
    {
        var legacy = DbNames.Legacy.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tableNames.Where(legacy.Contains).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
