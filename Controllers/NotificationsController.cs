using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public NotificationsController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ════════════════════════════════════════════════════════════════
    //  POS APP ENDPOINTS (JWT-authenticated, tenant-scoped)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Queue a single SMS for delivery via the Android gateway.
    /// POST /api/notifications/sms/send
    /// </summary>
    [Authorize]
    [HttpPost("sms/send")]
    public async Task<IActionResult> QueueSms([FromBody] QueueSmsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Both phone and message are required." });

        var phoneStr = request.Phone.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(phoneStr, @"^\+?[0-9]{7,15}$"))
            return BadRequest(new { error = "Invalid phone number format." });

        if (request.Message.Length > 1600)
            return BadRequest(new { error = "Message too long. Maximum 1600 characters." });

        var msg = new SmsMessage
        {
            TenantId = _db.CurrentTenantId,
            To = phoneStr,
            Message = request.Message,
            Channel = request.Channel ?? "sms",
            Reference = request.Reference,
            Status = "pending",
            MediaUrl = request.MediaUrl
        };

        _db.SmsMessages.Add(msg);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, id = msg.Id, message = "SMS queued for delivery." });
    }

    /// <summary>
    /// Queue bulk SMS (max 100 recipients).
    /// POST /api/notifications/sms/send-bulk
    /// </summary>
    [Authorize]
    [HttpPost("sms/send-bulk")]
    public async Task<IActionResult> QueueBulkSms([FromBody] QueueBulkSmsRequest request)
    {
        if (request.Phones == null || request.Phones.Count == 0)
            return BadRequest(new { error = "phones must be a non-empty array." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        if (request.Phones.Count > 100)
            return BadRequest(new { error = "Maximum 100 recipients per bulk request." });

        var messages = new List<SmsMessage>();
        foreach (var phone in request.Phones)
        {
            var p = phone?.Trim() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(p, @"^\+?[0-9]{7,15}$"))
                continue;

            messages.Add(new SmsMessage
            {
                TenantId = _db.CurrentTenantId,
                To = p,
                Message = request.Message,
                Channel = request.Channel ?? "sms",
                Reference = request.Reference,
                Status = "pending"
            });
        }

        _db.SmsMessages.AddRange(messages);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, queued = messages.Count, message = $"{messages.Count} SMS queued for delivery." });
    }

    /// <summary>
    /// View SMS queue for current tenant (with optional status filter).
    /// GET /api/notifications/sms/queue?status=pending&page=1&pageSize=50
    /// </summary>
    [Authorize]
    [HttpGet("sms/queue")]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _db.SmsMessages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        var total = await query.CountAsync();
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.To,
                m.Message,
                m.Channel,
                m.Status,
                m.Error,
                m.Reference,
                m.MediaUrl,
                m.CreatedAt,
                m.ProcessedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, messages });
    }

    // ════════════════════════════════════════════════════════════════
    //  ANDROID GATEWAY POLL ENDPOINTS (API-key auth, no tenant filter)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Android SMS Gateway polls this to fetch pending messages.
    /// GET /api/notifications/sms/pending?api_key=YOUR_KEY
    /// Returns pending messages and marks them as "processing".
    /// </summary>
    [AllowAnonymous]
    [HttpGet("sms/pending")]
    public async Task<IActionResult> GetPending([FromQuery] string api_key)
    {
        if (!ValidateGatewayApiKey(api_key))
            return Unauthorized(new { error = "Invalid API key." });

        // Bypass tenant filter to fetch ALL pending SMS across tenants
        var pending = await _db.SmsMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == "pending")
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync();

        // Mark them as processing so they aren't returned again
        foreach (var msg in pending)
            msg.Status = "processing";

        await _db.SaveChangesAsync();

        var result = pending.Select(m => new
        {
            id = m.Id.ToString(),
            phone = m.To,
            message = m.Message,
            channel = m.Channel,
            media_url = m.MediaUrl
        });

        return Ok(new { messages = result });
    }

    /// <summary>
    /// Android SMS Gateway reports delivery status.
    /// POST /api/notifications/sms/status?api_key=YOUR_KEY
    /// Body: { "id": "123", "status": "sent" } or { "id": "123", "status": "failed", "error": "No signal" }
    /// </summary>
    [AllowAnonymous]
    [HttpPost("sms/status")]
    public async Task<IActionResult> UpdateStatus([FromQuery] string api_key, [FromBody] StatusUpdateRequest request)
    {
        if (!ValidateGatewayApiKey(api_key))
            return Unauthorized(new { error = "Invalid API key." });

        if (!int.TryParse(request.Id, out var msgId))
            return BadRequest(new { error = "Invalid message ID." });

        var msg = await _db.SmsMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == msgId);

        if (msg == null)
            return NotFound(new { error = "Message not found." });

        msg.Status = request.Status ?? "sent";
        msg.Error = request.Error;
        msg.ProcessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    /// <summary>
    /// Health check for the SMS poll endpoint.
    /// GET /api/notifications/sms/health?api_key=YOUR_KEY
    /// </summary>
    [AllowAnonymous]
    [HttpGet("sms/health")]
    public IActionResult GatewayHealth([FromQuery] string api_key)
    {
        if (!ValidateGatewayApiKey(api_key))
            return Unauthorized(new { error = "Invalid API key." });

        return Ok(new { success = true, message = "SMS Gateway Poll API is running." });
    }

    // ════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════

    private bool ValidateGatewayApiKey(string? key)
    {
        var expected = _config["SmsGateway:ApiKey"];
        return !string.IsNullOrEmpty(key)
            && !string.IsNullOrEmpty(expected)
            && string.Equals(key, expected, StringComparison.Ordinal);
    }
}

// ── Request DTOs ──

public class QueueSmsRequest
{
    public string Phone { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Channel { get; set; }
    public string? Reference { get; set; }
    public string? MediaUrl { get; set; }
}

public class QueueBulkSmsRequest
{
    public List<string> Phones { get; set; } = new();
    public string Message { get; set; } = "";
    public string? Channel { get; set; }
    public string? Reference { get; set; }
}

public class StatusUpdateRequest
{
    public string Id { get; set; } = "";
    public string? Status { get; set; }
    public string? Error { get; set; }
}
