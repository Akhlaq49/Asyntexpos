using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/installments")]
public class InstallmentsController : ControllerBase
{
    private readonly IInstallmentService _service;

    public InstallmentsController(IInstallmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search, [FromQuery] string? status)
    {
        if (page.HasValue)
        {
            var query = new PaginationQuery { Page = page.Value, PageSize = pageSize ?? 10, Search = search, Status = status };
            return Ok(await _service.GetAllPagedAsync(query));
        }
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var plan = await _service.GetByIdAsync(id);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallmentDto dto)
    {
        try
        {
            var plan = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = plan.Id }, plan);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PreviewInstallmentDto dto)
    {
        var preview = _service.PreviewPlan(dto);
        return Ok(preview);
    }

    [HttpPut("{planId}/pay/{installmentNo}")]
    public async Task<IActionResult> MarkPaid(int planId, int installmentNo, [FromBody] PayInstallmentDto paymentDto)
    {
        try
        {
            var result = await _service.PayInstallmentAsync(planId, installmentNo, paymentDto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var success = await _service.CancelAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("parties/search")]
    public async Task<IActionResult> SearchParties([FromQuery] string? q)
    {
        var parties = await _service.SearchPartiesAsync(q);
        return Ok(parties);
    }

    [HttpPost("{planId}/guarantors")]
    public async Task<IActionResult> AddGuarantor(int planId, [FromForm] string name, [FromForm] string? so,
        [FromForm] string? phone, [FromForm] string? cnic, [FromForm] string? address,
        [FromForm] string? relationship, [FromForm] int? partyId, IFormFile? picture)
    {
        try
        {
            var result = await _service.AddGuarantorAsync(planId, name, so, phone, cnic, address, relationship, picture, partyId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("guarantors/{guarantorId}")]
    public async Task<IActionResult> UpdateGuarantor(int guarantorId, [FromForm] string name, [FromForm] string? so,
        [FromForm] string? phone, [FromForm] string? cnic, [FromForm] string? address,
        [FromForm] string? relationship, IFormFile? picture)
    {
        var result = await _service.UpdateGuarantorAsync(guarantorId, name, so, phone, cnic, address, relationship, picture);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("guarantors/{guarantorId}")]
    public async Task<IActionResult> DeleteGuarantor(int guarantorId)
    {
        var success = await _service.DeleteGuarantorAsync(guarantorId);
        if (!success) return NotFound();
        return NoContent();
    }
}
