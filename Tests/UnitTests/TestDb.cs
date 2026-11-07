using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.UnitTests;

/// <summary>A company's database for one test: SQLite in memory, created from the model (same tables as MySQL)</summary>
public sealed class TestDb : IDisposable
{
    private sealed class FixedTenant : ITenantProvider { public int TenantId { get; init; } }

    private readonly SqliteConnection _connection;
    public int TenantId { get; }
    public AppDbContext Db { get; private set; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        // MySQL functions the raw queries use
        _connection.CreateFunction("CONCAT", (string? a, string? b, string? c, string? d, string? e) => string.Concat(a, b, c, d, e));
        using (var setup = Create(0))
        {
            setup.Database.EnsureCreated();
            setup.Tenants.Add(new Tenant { Name = "Test company", Email = "test@local", IsActive = true, CreatedAt = DateTime.UtcNow });
            setup.SaveChanges();
            TenantId = setup.Tenants.IgnoreQueryFilters().Single().Id;
            // the view the package-items import reads (MySQL: Database/CATL/Schema.sql); empty here
            setup.Database.ExecuteSqlRaw($"CREATE VIEW {DbNames.CatlViewTrcf} AS SELECT 0 AS TenantId, '' AS PackageName, '' AS TierName, '' AS ProductName WHERE 0");
        }
        Db = Create(TenantId);
    }

    public AppDbContext Create(int tenantId) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new FixedTenant { TenantId = tenantId });

    /// <summary>A fresh context (nothing tracked), like the next HTTP request</summary>
    public AppDbContext Fresh()
    {
        Db.Dispose();
        Db = Create(TenantId);
        return Db;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
