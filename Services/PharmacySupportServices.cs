using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IPharmacySettingsService
{
    /// <summary>Settings for the current tenant; a default row is created on first use.</summary>
    Task<PharmacySettings> GetAsync();
    Task<PharmacySettings> SaveAsync(PharmacySettings input);
}

public class PharmacySettingsService : IPharmacySettingsService
{
    private readonly AppDbContext _db;
    public PharmacySettingsService(AppDbContext db) => _db = db;

    public async Task<PharmacySettings> GetAsync()
    {
        var s = await _db.PharmacySettings.FirstOrDefaultAsync();
        if (s != null) return s;

        var tenantName = await _db.Tenants.Where(t => t.Id == _db.CurrentTenantId).Select(t => t.Name).FirstOrDefaultAsync();
        s = new PharmacySettings { PharmacyName = tenantName ?? "Pharmacy" };
        if (_db.CurrentTenantId > 0)
        {
            _db.PharmacySettings.Add(s);
            await _db.SaveChangesAsync();
        }
        return s;
    }

    public async Task<PharmacySettings> SaveAsync(PharmacySettings input)
    {
        var s = await GetAsync();
        s.PharmacyName = input.PharmacyName?.Trim() ?? s.PharmacyName;
        s.Address = input.Address;
        s.Phone = input.Phone;
        s.DrugLicenceNo = input.DrugLicenceNo;
        s.Ntn = input.Ntn;
        s.Strn = input.Strn;
        s.ReceiptFooter = input.ReceiptFooter;
        s.DefaultTaxPercent = Math.Clamp(input.DefaultTaxPercent, 0, 100);
        s.MaxCashierDiscountPercent = Math.Clamp(input.MaxCashierDiscountPercent, 0, 100);
        s.ExpiryAlertDays1 = Math.Max(1, input.ExpiryAlertDays1);
        s.ExpiryAlertDays2 = Math.Clamp(input.ExpiryAlertDays2, 1, s.ExpiryAlertDays1);
        s.ExpiryAlertDays3 = Math.Clamp(input.ExpiryAlertDays3, 1, s.ExpiryAlertDays2);
        s.RequirePrescription = input.RequirePrescription;
        s.RefillRemindersEnabled = input.RefillRemindersEnabled;
        s.FbrEnabled = input.FbrEnabled;
        s.FbrPosId = input.FbrPosId;
        // Keep the stored token when the screen sends the masked placeholder back.
        if (!string.IsNullOrWhiteSpace(input.FbrToken) && !input.FbrToken.StartsWith("••"))
            s.FbrToken = input.FbrToken;
        s.FbrEnvironment = input.FbrEnvironment == "Production" ? "Production" : "Sandbox";
        s.ReceiptLanguage = input.ReceiptLanguage is "en+ur" or "en+ar" or "ur" or "ar" ? input.ReceiptLanguage : "en";
        s.BlockOverCreditLimit = input.BlockOverCreditLimit;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return s;
    }
}

public interface IAuditService
{
    /// <summary>Queues an audit row; it is written with the caller's next SaveChanges.</summary>
    void Log(string action, string entityType, object? entityId, object? details = null);
    string CurrentUser { get; }
    string CurrentRole { get; }
    bool IsAdmin { get; }
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public string CurrentUser => _http.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? "System";
    public string CurrentRole => _http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ?? "";
    public bool IsAdmin => CurrentRole is "Admin" or "SuperAdmin" or "Owner";

    public void Log(string action, string entityType, object? entityId, object? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserName = CurrentUser,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details == null ? null : details as string ?? JsonSerializer.Serialize(details),
            CreatedAt = DateTime.UtcNow
        });
    }
}
