using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Services;
using System.Security.Claims;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolePermissionsController : ControllerBase
{
    private readonly IRolePermissionService _service;
    public RolePermissionsController(IRolePermissionService service) => _service = service;

    [HttpGet("my-permissions")]
    public async Task<ActionResult<List<string>>> GetMyPermissions()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        return Ok(await _service.GetPermissionsByRoleAsync(role));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<string>>> GetConfiguredRoles()
        => Ok(await _service.GetConfiguredRolesAsync());

    [HttpGet("by-role/{role}")]
    public async Task<ActionResult<List<string>>> GetByRole(string role)
    {
        try
        {
            return Ok(await _service.GetPermissionsByRoleAsync(role));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load permissions", error = ex.Message });
        }
    }

    [HttpPut("by-role/{role}")]
    public async Task<IActionResult> UpdateRole(string role, [FromBody] List<string> menuKeys)
    {
        var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        try
        {
            var result = await _service.UpdateRoleAsync(role, callerRole, menuKeys);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
