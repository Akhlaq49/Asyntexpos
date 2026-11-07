using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Drawing to Solution: upload an electrical drawing (DXF / vector PDF), review what was read, then get the
/// suggested Standard or Premium solution from the company's Solution Packages data.
/// The drawing is read by the Drawing2Quote service (Python); see Services/DrawingService.cs.
/// </summary>
[Authorize]
[ApiController]
[Route("api/drawings")]
public class DrawingsController : ControllerBase
{
    private readonly DrawingService _service;
    public DrawingsController(DrawingService service) => _service = service;

    public record SuggestDto(JsonElement Extraction, string? Plan, Dictionary<string, string>? Choices, int? SizeId, OtherBrandDto? OtherBrand);
    /// <summary>the client already uses another company's automation in these areas ("Floor / Area") -> Matter gateways</summary>
    public record OtherBrandDto(string? Company, List<string>? Areas);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(new { running = await _service.IsRunningAsync(ct) });

    [HttpPost("extract")]
    [RequestSizeLimit(DrawingService.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = DrawingService.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Extract(IFormFile? file, CancellationToken ct)
    {
        if (file == null) return BadRequest(new { message = "Choose the drawing file." });
        try { return Ok(await _service.ExtractAsync(file, ct)); }
        catch (DrawingServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    /// <summary>progress of a drawing being read; "done" carries the result</summary>
    [HttpGet("extract/{jobId}")]
    public async Task<IActionResult> ExtractStatus(string jobId, CancellationToken ct)
    {
        try { return Ok(await _service.JobAsync(jobId, ct)); }
        catch (DrawingServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("suggest")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Suggest([FromBody] SuggestDto dto, CancellationToken ct)
    {
        var plan = dto.Plan?.Trim().ToLowerInvariant();
        if (plan is not ("standard" or "premium")) return BadRequest(new { message = "Choose Standard or Premium." });
        if (dto.Extraction.ValueKind != JsonValueKind.Object) return BadRequest(new { message = "Upload the drawing first." });
        var other = dto.OtherBrand;
        if (other?.Areas is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(other.Company)) return BadRequest(new { message = "Enter the other company's name." });
            if (other.Areas.Count > 1000) return BadRequest(new { message = "Too many areas." });
        }
        else other = null;
        try { return Ok(await _service.SuggestAsync(dto.Extraction, plan, dto.Choices, dto.SizeId, other, ct)); }
        catch (DrawingServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
    }
}
