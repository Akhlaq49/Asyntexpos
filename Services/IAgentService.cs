namespace ReactPosApi.Services;

public interface IAgentService
{
    Task<AgentChatResult> ChatAsync(string userMessage, int tenantId, List<AgentChatMessage>? history = null);
}

public class AgentChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
}

public class AgentChatResult
{
    public string Reply { get; set; } = "";
    public List<string> ToolsUsed { get; set; } = new();
}
