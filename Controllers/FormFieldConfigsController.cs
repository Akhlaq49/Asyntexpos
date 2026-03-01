using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FormFieldConfigsController : ControllerBase
{
    private readonly IFormFieldConfigService _service;

    public FormFieldConfigsController(IFormFieldConfigService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get all field configs, optionally filtered by form name.
    /// GET /api/formfieldconfigs?formName=Customer
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FormFieldConfig>>> GetAll([FromQuery] string? formName)
    {
        if (!string.IsNullOrWhiteSpace(formName))
        {
            var byForm = await _service.GetByFormAsync(formName);
            return Ok(byForm);
        }

        var all = await _service.GetAllAsync();
        return Ok(all);
    }

    /// <summary>
    /// Get a single field config by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FormFieldConfig>> GetById(int id)
    {
        var config = await _service.GetByIdAsync(id);
        if (config == null) return NotFound();
        return Ok(config);
    }

    /// <summary>
    /// Upsert a single field config.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FormFieldConfig>> Upsert([FromBody] FormFieldConfigDto dto)
    {
        var result = await _service.UpsertAsync(dto.FormName, dto.FieldName, dto.FieldLabel, dto.IsVisible);
        return Ok(result);
    }

    /// <summary>
    /// Bulk upsert field configs (save all at once from settings UI).
    /// </summary>
    [HttpPut("bulk")]
    public async Task<ActionResult<List<FormFieldConfig>>> BulkUpsert([FromBody] List<FormFieldConfigDto> dtos)
    {
        var configs = dtos.Select(d => new FormFieldConfig
        {
            FormName = d.FormName,
            FieldName = d.FieldName,
            FieldLabel = d.FieldLabel,
            IsVisible = d.IsVisible
        }).ToList();

        var result = await _service.BulkUpsertAsync(configs);
        return Ok(result);
    }

    /// <summary>
    /// Delete a field config by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Seed default field configurations for all known forms.
    /// POST /api/formfieldconfigs/seed
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await _service.SeedDefaultsAsync();
        var all = await _service.GetAllAsync();
        return Ok(all);
    }
}

public class FormFieldConfigDto
{
    public string FormName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}
