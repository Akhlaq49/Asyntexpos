namespace ReactPosApi.DTOs;

public class AgentChatRequestDto
{
    public string Message { get; set; } = "";
    public List<AgentChatMessageDto>? History { get; set; }
}

public class AgentChatMessageDto
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class AgentChatResponseDto
{
    public string Reply { get; set; } = "";
    public List<string> ToolsUsed { get; set; } = new();
}
