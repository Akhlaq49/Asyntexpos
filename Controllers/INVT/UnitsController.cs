using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly IUnitService _service;
    public UnitsController(IUnitService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<DropdownOptionDto>>> GetAll()
        => Ok(await _service.GetAllDropdownAsync());

    [HttpGet("list")]
    public async Task<ActionResult<List<UnitDto>>> GetList()
        => Ok(await _service.GetListAsync());

    [HttpPost]
    public async Task<ActionResult<UnitDto>> Create([FromBody] CreateUnitDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetList), null, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UnitDto>> Update(int id, [FromBody] CreateUnitDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _service.DeleteAsync(id);
        if (error != null) return Conflict(new { message = error });
        if (!success) return NotFound();
        return NoContent();
    }
}
