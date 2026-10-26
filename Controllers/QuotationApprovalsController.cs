using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Staff approvals: staff send a quotation to the company's Admin / SuperAdmin (one or more);
/// a recipient approves it (then the staff member can convert it to a sale) or discards it with a reason.
/// </summary>
[Authorize]
[ApiController]
public class QuotationApprovalsController : ControllerBase
{
    private readonly AppDbContext _db;
    public QuotationApprovalsController(AppDbContext db) => _db = db;

    public record ApproverDto(int Id, string Name, string Role);
    public record SendDto(List<int> RecipientIds, string? Message);
    public record DecideDto(string? Note);
    public record LineDto(string ProductName, int Quantity, decimal TotalCost);
    public record ApprovalDto(int Id, int QuotationId, string QuotationReference, string CustomerName, decimal GrandTotal, string? QuotationDate,
        string RequestedByName, DateTime RequestedAt, string? Message, string RecipientNames, bool SentToMe, string Status,
        string? DecidedByName, DateTime? DecidedAt, string? DecisionNote, bool QuotationExists, bool QuotationChanged, string? SaleReference, List<LineDto> Items);

    private int MyId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string MyName => User.FindFirstValue(ClaimTypes.Name) ?? "";
    private bool IAmAdmin => User.FindFirstValue(ClaimTypes.Role) is RoleCatalog.SuperAdmin or RoleCatalog.Admin;

    /// <summary>Admins / SuperAdmins of the company that a quotation can be sent to</summary>
    [HttpGet("api/quotation-approvals/approvers")]
    public async Task<IActionResult> Approvers()
    {
        var list = await _db.Parties.Where(p => (p.Role == RoleCatalog.SuperAdmin || p.Role == RoleCatalog.Admin) && p.IsActive && p.PasswordHash != null)
            .OrderBy(p => p.Role == RoleCatalog.SuperAdmin ? 0 : 1).ThenBy(p => p.FullName)
            .Select(p => new ApproverDto(p.Id, p.FullName, p.Role == RoleCatalog.SuperAdmin ? "Super Admin" : "Admin")).ToListAsync();
        return Ok(list);
    }

    /// <summary>Send the quotation for approval (a pending request is replaced, e.g. to change who it goes to)</summary>
    [HttpPost("api/quotations/{id:int}/approval-request")]
    public async Task<IActionResult> Send(int id, [FromBody] SendDto dto)
    {
        var q = await _db.Quotations.FirstOrDefaultAsync(x => x.Id == id && (IAmAdmin || x.CreatedById == MyId));
        if (q == null) return NotFound(new { message = "This quotation no longer exists." });
        if (q.SaleId != null) return BadRequest(new { message = $"{q.Reference} was already converted to sale {q.SaleReference}." });
        if (q.SupersededById != null) return BadRequest(new { message = $"{q.Reference} is an older version - send the latest version." });
        if (q.ApprovalStatus == QuotationApprovalStatus.Approved) return BadRequest(new { message = $"{q.Reference} is already approved - it can be converted to a sale." });
        var ids = (dto.RecipientIds ?? new()).Distinct().ToList();
        if (ids.Count == 0) return BadRequest(new { message = "Choose who to send it to." });
        var recipients = await _db.Parties.Where(p => ids.Contains(p.Id) && (p.Role == RoleCatalog.SuperAdmin || p.Role == RoleCatalog.Admin) && p.IsActive).ToListAsync();
        if (recipients.Count != ids.Count) return BadRequest(new { message = "It can only be sent to an active Admin or Super Admin of the company." });

        using var transaction = await _db.Database.BeginTransactionAsync();
        foreach (var old in await _db.QuotationApprovals.Where(a => a.QuotationId == q.Id && a.Status == QuotationApprovalStatus.Pending).ToListAsync())
        { old.Status = QuotationApprovalStatus.Withdrawn; old.DecidedAt = DateTime.UtcNow; old.DecisionNote = "Sent again"; }
        var names = string.Join(", ", recipients.Select(r => r.FullName));
        var request = new QuotationApproval
        {
            QuotationId = q.Id, QuotationReference = q.Reference, CustomerName = q.CustomerName, GrandTotal = q.GrandTotal,
            RequestedById = MyId, RequestedByName = MyName, RequestedAt = DateTime.UtcNow,
            Message = string.IsNullOrWhiteSpace(dto.Message) ? null : dto.Message.Trim()[..Math.Min(500, dto.Message.Trim().Length)],
            RecipientIds = "," + string.Join(",", recipients.Select(r => r.Id)) + ",", RecipientNames = names.Length > 500 ? names[..500] : names,
        };
        _db.QuotationApprovals.Add(request);
        q.ApprovalStatus = QuotationApprovalStatus.Pending; q.ApprovalSentTo = request.RecipientNames;
        q.ApprovalBy = MyName; q.ApprovalAt = request.RequestedAt; q.ApprovalNote = request.Message;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { q.Id, q.Reference, q.ApprovalStatus, q.ApprovalSentTo });
    }

    /// <summary>The staff member takes the request back</summary>
    [HttpPost("api/quotations/{id:int}/approval-request/withdraw")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var q = await _db.Quotations.FirstOrDefaultAsync(x => x.Id == id && (IAmAdmin || x.CreatedById == MyId));
        if (q == null) return NotFound(new { message = "This quotation no longer exists." });
        if (q.ApprovalStatus != QuotationApprovalStatus.Pending) return BadRequest(new { message = $"{q.Reference} isn't waiting for approval." });
        foreach (var a in await _db.QuotationApprovals.Where(a => a.QuotationId == q.Id && a.Status == QuotationApprovalStatus.Pending).ToListAsync())
        { a.Status = QuotationApprovalStatus.Withdrawn; a.DecidedAt = DateTime.UtcNow; a.DecidedByName = MyName; }
        q.ApprovalStatus = null; q.ApprovalSentTo = null; q.ApprovalBy = null; q.ApprovalAt = null; q.ApprovalNote = null;
        await _db.SaveChangesAsync();
        return Ok(new { q.Id });
    }

    /// <summary>Requests for the admins: sent to me (default) or all; filter by status</summary>
    [HttpGet("api/quotation-approvals")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] bool all = false)
    {
        if (!IAmAdmin) return StatusCode(403, new { message = "Only an Admin or Super Admin can see staff approvals." });
        var me = $",{MyId},";
        var q = _db.QuotationApprovals.AsQueryable();
        if (!all) q = q.Where(a => a.RecipientIds.Contains(me));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        var list = await q.OrderByDescending(a => a.Status == QuotationApprovalStatus.Pending).ThenByDescending(a => a.RequestedAt).Take(500).ToListAsync();

        var qids = list.Select(a => a.QuotationId).Distinct().ToList();
        var quotes = await _db.Quotations.Include(x => x.Items).Where(x => qids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return Ok(list.Select(a =>
        {
            quotes.TryGetValue(a.QuotationId, out var qt);
            return new ApprovalDto(a.Id, a.QuotationId, a.QuotationReference, a.CustomerName, qt?.GrandTotal ?? a.GrandTotal, qt?.QuotationDate.ToString("dd MMM yyyy"),
                a.RequestedByName, a.RequestedAt, a.Message, a.RecipientNames, a.RecipientIds.Contains(me), a.Status,
                a.DecidedByName, a.DecidedAt, a.DecisionNote, qt != null, qt != null && qt.GrandTotal != a.GrandTotal, qt?.SaleReference,
                qt?.Items.OrderBy(i => i.Id).Select(i => new LineDto(i.ProductName, i.Quantity, i.TotalCost)).ToList() ?? new());
        }));
    }

    /// <summary>How many requests are waiting for me (menu / page badge)</summary>
    [HttpGet("api/quotation-approvals/pending-count")]
    public async Task<IActionResult> PendingCount()
    {
        if (!IAmAdmin) return Ok(new { count = 0 });
        var me = $",{MyId},";
        return Ok(new { count = await _db.QuotationApprovals.CountAsync(a => a.Status == QuotationApprovalStatus.Pending && a.RecipientIds.Contains(me)) });
    }

    [HttpPost("api/quotation-approvals/{id:int}/approve")]
    public Task<IActionResult> Approve(int id, [FromBody] DecideDto dto) => Decide(id, QuotationApprovalStatus.Approved, dto.Note);

    [HttpPost("api/quotation-approvals/{id:int}/discard")]
    public Task<IActionResult> Discard(int id, [FromBody] DecideDto dto) => Decide(id, QuotationApprovalStatus.Discarded, dto.Note);

    private async Task<IActionResult> Decide(int id, string decision, string? note)
    {
        if (!IAmAdmin) return StatusCode(403, new { message = "Only an Admin or Super Admin can approve or discard." });
        var a = await _db.QuotationApprovals.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound(new { message = "This request no longer exists." });
        if (a.Status != QuotationApprovalStatus.Pending)
            return BadRequest(new { message = a.Status switch
            {
                QuotationApprovalStatus.Approved or QuotationApprovalStatus.Discarded => $"{a.QuotationReference} was already {a.Status.ToLower()} by {a.DecidedByName}.",
                QuotationApprovalStatus.Withdrawn => $"{a.RequestedByName} took this request back.",
                _ => $"This request no longer applies ({a.DecisionNote ?? a.Status}).",
            } });
        if (!a.RecipientIds.Contains($",{MyId},")) return StatusCode(403, new { message = $"This request was sent to {a.RecipientNames}." });
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(500, note.Trim().Length)];
        if (decision == QuotationApprovalStatus.Discarded && text == null) return BadRequest(new { message = "Write the reason for discarding it." });
        var q = await _db.Quotations.FirstOrDefaultAsync(x => x.Id == a.QuotationId);
        if (q == null) return BadRequest(new { message = "The quotation was deleted." });

        a.Status = decision; a.DecidedById = MyId; a.DecidedByName = MyName; a.DecidedAt = DateTime.UtcNow; a.DecisionNote = text;
        q.ApprovalStatus = decision; q.ApprovalBy = MyName; q.ApprovalAt = a.DecidedAt; q.ApprovalNote = text;
        await _db.SaveChangesAsync();
        return Ok(new { a.Id, a.Status, q.Reference });
    }
}
