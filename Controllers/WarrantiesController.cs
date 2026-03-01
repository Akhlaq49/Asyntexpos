using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WarrantiesController : ControllerBase
{
    private readonly IWarrantyService _service;
    public WarrantiesController(IWarrantyService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<WarrantyDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<WarrantyDto>> Create([FromBody] CreateWarrantyDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), null, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WarrantyDto>> Update(int id, [FromBody] CreateWarrantyDto dto)
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
