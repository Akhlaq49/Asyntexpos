using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/variant-attributes")]
public class VariantAttributesController : ControllerBase
{
    private readonly IVariantAttributeService _service;
    public VariantAttributesController(IVariantAttributeService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<VariantAttributeDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<VariantAttributeDto>> Create([FromBody] CreateVariantAttributeDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), null, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VariantAttributeDto>> Update(int id, [FromBody] CreateVariantAttributeDto dto)
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
