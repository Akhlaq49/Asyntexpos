using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _service;
    private readonly ReactPosApi.Data.AppDbContext _db;
    public QuotationsController(IQuotationService service, ReactPosApi.Data.AppDbContext db) { _service = service; _db = db; }

    /* Staff (not Admin / SuperAdmin) only see and work on the quotations they made */
    private bool IsAdmin => User.FindFirstValue(ClaimTypes.Role) is RoleCatalog.SuperAdmin or RoleCatalog.Admin;
    private int MyId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<bool> CanAccessAsync(int id) => IsAdmin || await _db.Quotations.AnyAsync(q => q.Id == id && q.CreatedById == MyId);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? product,
        [FromQuery] string? customer,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search)
    {
        if (!IsAdmin)
        {
            var mine = (await _service.GetAllAsync(product, customer, status, sort)).Where(q => q.CreatedById == MyId).ToList();
            if (!page.HasValue) return Ok(mine);
            if (!string.IsNullOrWhiteSpace(search))
                mine = mine.Where(q => (q.Reference + " " + q.CustomerName).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            var size = pageSize ?? 10;
            return Ok(new PagedResult<QuotationDto> { Items = mine.Skip((page.Value - 1) * size).Take(size).ToList(), TotalCount = mine.Count, Page = page.Value, PageSize = size });
        }
        if (page.HasValue)
        {
            var query = new PaginationQuery { Page = page.Value, PageSize = pageSize ?? 10, Search = search, Status = status };
            return Ok(await _service.GetAllPagedAsync(query));
        }
        return Ok(await _service.GetAllAsync(product, customer, status, sort));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await CanAccessAsync(id)) return NotFound();
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotationDto dto)
    {
        dto.CreatedById = MyId; dto.CreatedByName = User.FindFirstValue(ClaimTypes.Name);
        try { return Ok(await _service.CreateAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateQuotationDto dto)
    {
        if (!await CanAccessAsync(id)) return NotFound();
        try
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Quotation -> sale: deducts stock and records the payment received now</summary>
    [HttpPost("{id}/convert-to-sale")]
    public async Task<IActionResult> ConvertToSale(int id, [FromBody] ConvertQuotationToSaleDto dto, [FromServices] ISaleService sales, [FromServices] ReactPosApi.Data.AppDbContext db)
    {
        if (!await CanAccessAsync(id)) return NotFound();
        // staff (not Admin / SuperAdmin) can only convert a quotation an admin has approved
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (callerRole != RoleCatalog.SuperAdmin && callerRole != RoleCatalog.Admin)
        {
            var approval = await db.Quotations.Where(q => q.Id == id).Select(q => new { q.ApprovalStatus, q.Reference }).FirstOrDefaultAsync();
            if (approval != null && approval.ApprovalStatus != "Approved")
                return BadRequest(new { message = approval.ApprovalStatus == "Pending"
                    ? $"{approval.Reference} is waiting for approval - it can be converted once an admin approves it."
                    : $"{approval.Reference} needs an admin's approval before it can be converted to a sale. Use \"Send for approval\"." });
        }
        try
        {
            var sale = await sales.ConvertQuotationAsync(id, dto, User.FindFirstValue(ClaimTypes.Name) ?? "");
            return sale == null ? NotFound() : Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>New version of the quotation (…_R1, _R2 …); the current version is kept as history</summary>
    [HttpPost("{id}/revise")]
    public async Task<IActionResult> Revise(int id, [FromBody] CreateQuotationDto dto)
    {
        if (!await CanAccessAsync(id)) return NotFound();
        dto.CreatedById = MyId; dto.CreatedByName = User.FindFirstValue(ClaimTypes.Name);
        try
        {
            var revision = await _service.ReviseAsync(id, dto);
            return revision == null ? NotFound() : Ok(revision);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>All versions of the quotation, original first</summary>
    [HttpGet("{id}/revisions")]
    public async Task<IActionResult> Revisions(int id) => await CanAccessAsync(id) ? Ok(await _service.GetRevisionsAsync(id)) : NotFound();

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAccessAsync(id)) return NotFound();
        try
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
