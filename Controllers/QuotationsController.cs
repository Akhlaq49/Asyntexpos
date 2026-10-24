using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _service;
    public QuotationsController(IQuotationService service) => _service = service;

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
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotationDto dto)
        => Ok(await _service.CreateAsync(dto));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateQuotationDto dto)
    {
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
    public async Task<IActionResult> ConvertToSale(int id, [FromBody] ConvertQuotationToSaleDto dto, [FromServices] ISaleService sales)
    {
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
    public async Task<IActionResult> Revisions(int id) => Ok(await _service.GetRevisionsAsync(id));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
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
