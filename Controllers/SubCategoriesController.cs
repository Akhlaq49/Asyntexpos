using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/sub-categories")]
public class SubCategoriesController : ControllerBase
{
    private readonly ISubCategoryService _service;
    public SubCategoriesController(ISubCategoryService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<SubCategoryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<SubCategoryDto>> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<SubCategoryDto>> Create([FromForm] CreateSubCategoryDto dto, IFormFile? image)
    {
        var result = await _service.CreateAsync(dto, image);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SubCategoryDto>> Update(int id, [FromForm] CreateSubCategoryDto dto, IFormFile? image)
    {
        var result = await _service.UpdateAsync(id, dto, image);
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
