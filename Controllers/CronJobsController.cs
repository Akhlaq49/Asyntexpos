using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/cron")]
public class CronJobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<CronJobsController> _logger;
    private readonly IConfiguration _configuration;

    public CronJobsController(
        AppDbContext db,
        IWhatsAppService whatsApp,
        ILogger<CronJobsController> logger,
        IConfiguration configuration)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Cron endpoint: Sends WhatsApp reminders for installments due within 5 days.
    /// Processes all tenants. Sends message to each customer and a summary to tenant owner.
    /// Secured via X-Cron-Key header.
    /// </summary>
    [HttpPost("installment-reminders")]
    public async Task<IActionResult> SendInstallmentReminders()
    {
        // Validate cron API key
        var expectedKey = _configuration["CronJob:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return StatusCode(500, new { error = "CronJob:ApiKey is not configured." });

        if (!Request.Headers.TryGetValue("X-Cron-Key", out var cronKey)
            || !string.Equals(cronKey.FirstOrDefault(), expectedKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "Invalid or missing cron key." });
        }

        if (!_whatsApp.IsConfigured)
            return StatusCode(503, new { error = "WhatsApp is not configured." });

        var today = DateTime.UtcNow.Date;
        var reminderDate = today.AddDays(5).ToString("yyyy-MM-dd");
        var todayStr = today.ToString("yyyy-MM-dd");

        // Fetch all active tenants
        var tenants = await _db.Tenants
            .Where(t => t.IsActive)
            .ToListAsync();

        var totalCustomerMessages = 0;
        var totalOwnerMessages = 0;
        var failures = new List<string>();

        foreach (var tenant in tenants)
        {
            try
            {
                // Get repayment entries due within 5 days for this tenant (bypass global filter)
                var upcomingEntries = await _db.RepaymentEntries
                    .IgnoreQueryFilters()
                    .Include(r => r.Plan!)
                        .ThenInclude(p => p.Customer)
                    .Include(r => r.Plan!)
                        .ThenInclude(p => p.Product)
                    .Where(r => r.TenantId == tenant.Id
                        && r.Plan!.Status == "active"
                        && (r.Status == "upcoming" || r.Status == "due")
                        && string.Compare(r.DueDate, todayStr) >= 0
                        && string.Compare(r.DueDate, reminderDate) <= 0)
                    .OrderBy(r => r.DueDate)
                    .ToListAsync();

                if (upcomingEntries.Count == 0)
                    continue;

                // Group by customer
                var customerGroups = upcomingEntries
                    .Where(e => e.Plan?.Customer != null)
                    .GroupBy(e => e.Plan!.CustomerId);

                var tenantSummaryLines = new List<string>();

                foreach (var group in customerGroups)
                {
                    var customer = group.First().Plan!.Customer!;
                    if (string.IsNullOrWhiteSpace(customer.Phone))
                        continue;

                    // Build customer message
                    var lines = new List<string>
                    {
                        $"Assalam o Alaikum {customer.FullName},",
                        "",
                        "This is a reminder that you have upcoming installment(s):",
                        ""
                    };

                    foreach (var entry in group)
                    {
                        var productName = entry.Plan?.Product?.ProductName ?? "N/A";
                        lines.Add($"- *{productName}* | Installment #{entry.InstallmentNo} | Amount: Rs {entry.EmiAmount:N0} | Due: {entry.DueDate}");
                    }

                    lines.Add("");
                    lines.Add("Please ensure timely payment to avoid any late charges. JazakAllah!");

                    var customerMessage = string.Join("\n", lines);

                    var result = await _whatsApp.SendTextMessageAsync(customer.Phone, customerMessage);
                    if (result.Success)
                    {
                        totalCustomerMessages++;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send WhatsApp to {Phone} (Tenant {TenantId}): {Error}",
                            customer.Phone, tenant.Id, result.Error);
                        failures.Add($"Customer {customer.FullName} ({customer.Phone}): {result.Error}");
                    }

                    // Collect summary for tenant owner
                    foreach (var entry in group)
                    {
                        var productName = entry.Plan?.Product?.ProductName ?? "N/A";
                        tenantSummaryLines.Add(
                            $"- {customer.FullName} ({customer.Phone}) | {productName} | #{entry.InstallmentNo} | Rs {entry.EmiAmount:N0} | Due: {entry.DueDate}");
                    }
                }

                // Send summary to tenant owner (Admin)
                if (tenantSummaryLines.Count > 0)
                {
                    var owner = await _db.Parties
                        .IgnoreQueryFilters()
                        .Where(p => p.TenantId == tenant.Id
                            && p.Role == "Admin"
                            && p.IsActive
                            && !string.IsNullOrEmpty(p.Phone))
                        .FirstOrDefaultAsync();

                    if (owner != null)
                    {
                        var ownerLines = new List<string>
                        {
                            $"*Installment Reminder Summary — {tenant.Name}*",
                            $"Date: {today:dd MMM yyyy}",
                            "",
                            $"The following {tenantSummaryLines.Count} installment(s) are due within the next 5 days:",
                            ""
                        };
                        ownerLines.AddRange(tenantSummaryLines);
                        ownerLines.Add("");
                        ownerLines.Add("Customers have been notified via WhatsApp.");

                        var ownerMessage = string.Join("\n", ownerLines);

                        var ownerResult = await _whatsApp.SendTextMessageAsync(owner.Phone!, ownerMessage);
                        if (ownerResult.Success)
                        {
                            totalOwnerMessages++;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send summary to tenant owner {Phone} (Tenant {TenantId}): {Error}",
                                owner.Phone, tenant.Id, ownerResult.Error);
                            failures.Add($"Tenant Owner {owner.FullName} ({owner.Phone}): {ownerResult.Error}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No active Admin with phone found for Tenant {TenantId} ({TenantName})",
                            tenant.Id, tenant.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing installment reminders for Tenant {TenantId}", tenant.Id);
                failures.Add($"Tenant {tenant.Name} (ID: {tenant.Id}): {ex.Message}");
            }
        }

        return Ok(new
        {
            message = "Installment reminder job completed.",
            tenantsProcessed = tenants.Count,
            customerMessagesSent = totalCustomerMessages,
            ownerMessagesSent = totalOwnerMessages,
            failures = failures.Count > 0 ? failures : null
        });
    }

    /// <summary>
    /// Cron endpoint: Queues SMS reminders for installments due within 5 days.
    /// Sends to customers AND their guarantors via the SMS poll queue.
    /// Secured via X-Cron-Key header.
    /// POST /api/cron/sms-installment-reminders
    /// </summary>
    [HttpPost("sms-installment-reminders")]
    public async Task<IActionResult> SendSmsInstallmentReminders()
    {
        var expectedKey = _configuration["CronJob:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return StatusCode(500, new { error = "CronJob:ApiKey is not configured." });

        if (!Request.Headers.TryGetValue("X-Cron-Key", out var cronKey)
            || !string.Equals(cronKey.FirstOrDefault(), expectedKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "Invalid or missing cron key." });
        }

        var today = DateTime.UtcNow.Date;
        var reminderDate = today.AddDays(5).ToString("yyyy-MM-dd");
        var todayStr = today.ToString("yyyy-MM-dd");

        var tenants = await _db.Tenants
            .Where(t => t.IsActive)
            .ToListAsync();

        var totalCustomerSms = 0;
        var totalGuarantorSms = 0;
        var totalOwnerSms = 0;
        var failures = new List<string>();

        foreach (var tenant in tenants)
        {
            try
            {
                var upcomingEntries = await _db.RepaymentEntries
                    .IgnoreQueryFilters()
                    .Include(r => r.Plan!)
                        .ThenInclude(p => p.Customer)
                    .Include(r => r.Plan!)
                        .ThenInclude(p => p.Product)
                    .Include(r => r.Plan!)
                        .ThenInclude(p => p.PlanGuarantors)
                            .ThenInclude(g => g.Party)
                    .Where(r => r.TenantId == tenant.Id
                        && r.Plan!.Status == "active"
                        && (r.Status == "upcoming" || r.Status == "due")
                        && string.Compare(r.DueDate, todayStr) >= 0
                        && string.Compare(r.DueDate, reminderDate) <= 0)
                    .OrderBy(r => r.DueDate)
                    .ToListAsync();

                if (upcomingEntries.Count == 0)
                    continue;

                var customerGroups = upcomingEntries
                    .Where(e => e.Plan?.Customer != null)
                    .GroupBy(e => e.Plan!.CustomerId);

                var tenantSummaryLines = new List<string>();

                foreach (var group in customerGroups)
                {
                    var customer = group.First().Plan!.Customer!;

                    // Build installment details
                    var detailLines = new List<string>();
                    foreach (var entry in group)
                    {
                        var productName = entry.Plan?.Product?.ProductName ?? "N/A";
                        detailLines.Add($"- {productName} | #{entry.InstallmentNo} | Rs {entry.EmiAmount:N0} | Due: {entry.DueDate}");
                    }
                    var details = string.Join("\n", detailLines);

                    // SMS to customer
                    if (!string.IsNullOrWhiteSpace(customer.Phone))
                    {
                        var customerMsg = $"Assalam o Alaikum {customer.FullName},\n"
                            + "Reminder: You have upcoming installment(s):\n"
                            + details + "\n"
                            + "Please ensure timely payment. JazakAllah!";

                        _db.SmsMessages.Add(new SmsMessage
                        {
                            TenantId = tenant.Id,
                            To = customer.Phone,
                            Message = customerMsg,
                            Channel = "sms",
                            Reference = $"REMINDER-{todayStr}",
                            Status = "pending"
                        });
                        totalCustomerSms++;
                    }

                    // SMS to guarantors
                    var guarantors = group
                        .SelectMany(e => e.Plan!.PlanGuarantors)
                        .DistinctBy(g => g.PartyId)
                        .Where(g => !string.IsNullOrWhiteSpace(g.Party?.Phone));

                    foreach (var g in guarantors)
                    {
                        var gMsg = $"Assalam o Alaikum {g.Party!.FullName},\n"
                            + $"Reminder as guarantor for {customer.FullName}:\n"
                            + details + "\n"
                            + "Please ensure the customer pays on time. JazakAllah!";

                        _db.SmsMessages.Add(new SmsMessage
                        {
                            TenantId = tenant.Id,
                            To = g.Party.Phone!,
                            Message = gMsg,
                            Channel = "sms",
                            Reference = $"REMINDER-GUARANTOR-{todayStr}",
                            Status = "pending"
                        });
                        totalGuarantorSms++;
                    }

                    // Collect summary for tenant owner
                    foreach (var entry in group)
                    {
                        var productName = entry.Plan?.Product?.ProductName ?? "N/A";
                        tenantSummaryLines.Add(
                            $"- {customer.FullName} ({customer.Phone}) | {productName} | #{entry.InstallmentNo} | Rs {entry.EmiAmount:N0} | Due: {entry.DueDate}");
                    }
                }

                // Send summary SMS to tenant owner (Admin)
                if (tenantSummaryLines.Count > 0)
                {
                    var owner = await _db.Parties
                        .IgnoreQueryFilters()
                        .Where(p => p.TenantId == tenant.Id
                            && p.Role == "Admin"
                            && p.IsActive
                            && !string.IsNullOrEmpty(p.Phone))
                        .FirstOrDefaultAsync();

                    if (owner != null)
                    {
                        var ownerMsg = $"Installment Reminder Summary - {tenant.Name}\n"
                            + $"Date: {today:dd MMM yyyy}\n"
                            + $"{tenantSummaryLines.Count} installment(s) due within 5 days:\n"
                            + string.Join("\n", tenantSummaryLines);

                        _db.SmsMessages.Add(new SmsMessage
                        {
                            TenantId = tenant.Id,
                            To = owner.Phone!,
                            Message = ownerMsg,
                            Channel = "sms",
                            Reference = $"REMINDER-OWNER-{todayStr}",
                            Status = "pending"
                        });
                        totalOwnerSms++;
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SMS reminders for Tenant {TenantId}", tenant.Id);
                failures.Add($"Tenant {tenant.Name} (ID: {tenant.Id}): {ex.Message}");
            }
        }

        return Ok(new
        {
            message = "SMS installment reminder job completed.",
            tenantsProcessed = tenants.Count,
            customerSmsQueued = totalCustomerSms,
            guarantorSmsQueued = totalGuarantorSms,
            ownerSmsQueued = totalOwnerSms,
            failures = failures.Count > 0 ? failures : null
        });
    }
}
