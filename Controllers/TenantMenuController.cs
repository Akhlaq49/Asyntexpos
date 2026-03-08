using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Services;
using System.Security.Claims;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantMenuController : ControllerBase
{
    private readonly ITenantMenuService _service;
    public TenantMenuController(ITenantMenuService service) => _service = service;

    /// <summary>
    /// Get hidden menu keys for the current tenant.
    /// </summary>
    [HttpGet("hidden")]
    public async Task<ActionResult<List<string>>> GetHiddenMenuKeys()
        => Ok(await _service.GetHiddenMenuKeysAsync());

    /// <summary>
    /// Replace the set of hidden menu keys for the current tenant.
    /// Only SuperAdmin/Admin can call this.
    /// </summary>
    [HttpPut("hidden")]
    public async Task<IActionResult> UpdateHiddenMenuKeys([FromBody] List<string> hiddenKeys)
    {
        var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        try
        {
            var (success, count) = await _service.UpdateHiddenMenuKeysAsync(callerRole, hiddenKeys);
            return Ok(new { success, hiddenCount = count });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Get the default dashboard path for the current tenant.
    /// </summary>
    [HttpGet("default-dashboard")]
    public async Task<ActionResult<string>> GetDefaultDashboard()
        => Ok(await _service.GetDefaultDashboardAsync());

    /// <summary>
    /// Set the default dashboard path for the current tenant.
    /// Only SuperAdmin/Admin can call this.
    /// </summary>
    [HttpPut("default-dashboard")]
    public async Task<IActionResult> SetDefaultDashboard([FromBody] DefaultDashboardDto dto)
    {
        var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        try
        {
            var success = await _service.SetDefaultDashboardAsync(callerRole, dto.DashboardPath);
            return Ok(new { success });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}

public class DefaultDashboardDto
{
    public string DashboardPath { get; set; } = string.Empty;
}
