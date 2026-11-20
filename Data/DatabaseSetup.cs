using Microsoft.EntityFrameworkCore;

namespace ReactPosApi.Data;

/// <summary>
/// Runs on start: a new database is created from the model (EnsureCreated), then the module scripts add what EF
/// can't describe - procedures, composite keys, check constraints and views. The scripts are idempotent (T-SQL,
/// Microsoft SQL Server), so they run on every start.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>Embedded scripts, in the order they run</summary>
    public static readonly string[] Scripts = { "SYST/Procedures.sql", "INVT/Schema.sql", "ACCT/Schema.sql" };

    public static void Apply(AppDbContext db)
    {
        db.Database.EnsureCreated();
        var connection = db.Database.GetConnectionString()!;
        foreach (var script in Scripts) SqlScript.RunResource(connection, script);
    }
}
