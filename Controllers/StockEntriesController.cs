using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/stock-entries")]
public class StockEntriesController : ControllerBase
{
    private readonly IStockEntryService _service;
    public StockEntriesController(IStockEntryService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<StockEntryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<StockEntryDto>> Create([FromBody] CreateStockEntryDto dto)
    {
        var result = await _service.CreateAsync(dto);
        if (result == null) return BadRequest(new { message = "Product not found" });
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StockEntryDto>> Update(int id, [FromBody] CreateStockEntryDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
