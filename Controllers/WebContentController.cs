using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/webcontent")]
public class WebContentController : ControllerBase
{
    private readonly IWebContentService _service;

    public WebContentController(IWebContentService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<WebContentDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("section/{section}")]
    public async Task<ActionResult<List<WebContentDto>>> GetBySection(string section)
        => Ok(await _service.GetBySectionAsync(section));

    [HttpGet("{id}")]
    public async Task<ActionResult<WebContentDto>> GetById(int id)
    {
        var dto = await _service.GetByIdAsync(id);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WebContentDto>> Create(
        [FromForm] CreateWebContentDto dto,
        IFormFile? image)
    {
        var result = await _service.CreateAsync(dto, image);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WebContentDto>> Update(
        int id,
        [FromForm] CreateWebContentDto dto,
        IFormFile? image)
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
