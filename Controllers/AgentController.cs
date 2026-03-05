using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AgentChatRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Message is required." });

        var tenantClaim = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !int.TryParse(tenantClaim, out var tenantId))
            return Unauthorized(new { message = "Tenant ID not found in token." });

        var history = dto.History?.Select(h => new AgentChatMessage
        {
            Role = h.Role,
            Content = h.Content
        }).ToList();

        var result = await _agentService.ChatAsync(dto.Message, tenantId, history);
        return Ok(new AgentChatResponseDto { Reply = result.Reply, ToolsUsed = result.ToolsUsed });
    }
}
