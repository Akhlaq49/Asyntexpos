using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReactPosApi.Services;

public class AgentService : IAgentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IAgentToolExecutor _toolExecutor;
    private const int MaxToolRounds = 5;

    public AgentService(HttpClient httpClient, IConfiguration config, IAgentToolExecutor toolExecutor)
    {
        _httpClient = httpClient;
        _config = config;
        _toolExecutor = toolExecutor;
    }

    public async Task<AgentChatResult> ChatAsync(string userMessage, int tenantId, List<AgentChatMessage>? history = null)
    {
        var settings = _config.GetSection("LlmSettings");
        var apiKey = settings["ApiKey"];
        var model = settings["Model"] ?? "grok-4-latest";
        var baseUrl = (settings["BaseUrl"] ?? "https://api.x.ai/v1").TrimEnd('/');
        if (!baseUrl.EndsWith("/chat/completions"))
            baseUrl += "/chat/completions";
        var temperature = double.TryParse(settings["Temperature"], out var t) ? t : 0;
        var systemPrompt = settings["SystemPrompt"]
            ?? "You are a helpful POS assistant. Use the available tools to look up real data before answering.";

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("LLM API key is not configured.");

        // Build initial messages
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt + "\n\nIMPORTANT INSTRUCTIONS:\n1. Always use tools to fetch real data - NEVER make up data.\n2. For complex queries involving JOINs or multiple tables, FIRST call get_database_schema to learn table/column names, then use execute_sql_query with proper JOINs.\n3. ALWAYS include WHERE TenantId = @TenantId in every table reference in your SQL queries.\n4. Use the predefined tools (get_sales_summary, get_customer_info, etc.) for simple lookups.\n5. Use execute_sql_query for complex queries that need JOINs across multiple tables.\n6. Format results in a clear, readable way for the user." }
        };

        if (history != null)
        {
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });
        }

        messages.Add(new { role = "user", content = userMessage });

        var tools = AgentTools.GetToolDefinitions();
        var toolsUsed = new List<string>();
        // tenantId is passed through to tool executor for tenant-isolated queries

        // Agent loop: send to LLM, if it requests tool calls, execute them and loop back
        for (int round = 0; round < MaxToolRounds; round++)
        {
            var responseJson = await CallLlmAsync(baseUrl, apiKey, model, temperature, messages, tools);

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Handle error responses from the LLM API
            if (root.TryGetProperty("error", out var errorEl))
            {
                var errMsg = errorEl.TryGetProperty("message", out var em) ? em.GetString() : errorEl.GetRawText();
                return new AgentChatResult { Reply = $"LLM API error: {errMsg}", ToolsUsed = toolsUsed };
            }

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return new AgentChatResult { Reply = "No response from the AI model. Please try again.", ToolsUsed = toolsUsed };

            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var message))
                return new AgentChatResult { Reply = "Unexpected response format from the AI model.", ToolsUsed = toolsUsed };

            var finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : "";

            // Check if LLM wants to call tools
            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
            {
                // Build a clean assistant message with tool_calls for re-serialization
                var tcList = new List<object>();
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    tcList.Add(new Dictionary<string, object>
                    {
                        ["id"] = tc.GetProperty("id").GetString()!,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, string>
                        {
                            ["name"] = tc.GetProperty("function").GetProperty("name").GetString()!,
                            ["arguments"] = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
                        }
                    });
                }
                var assistantMsg = new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = message.TryGetProperty("content", out var ac) && ac.ValueKind == JsonValueKind.String ? ac.GetString()! : ""
                };
                assistantMsg["tool_calls"] = tcList;
                messages.Add(assistantMsg);

                // Execute each tool call
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var toolCallId = tc.GetProperty("id").GetString()!;
                    var funcName = tc.GetProperty("function").GetProperty("name").GetString()!;
                    var funcArgs = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";

                    toolsUsed.Add(funcName);

                    JsonElement argsElement;
                    try { argsElement = JsonDocument.Parse(funcArgs).RootElement; }
                    catch { argsElement = JsonDocument.Parse("{}").RootElement; }

                    var toolResult = await _toolExecutor.ExecuteAsync(funcName, argsElement, tenantId);

                    // Add tool response to conversation
                    messages.Add(new
                    {
                        role = "tool",
                        tool_call_id = toolCallId,
                        content = toolResult
                    });
                }
                // Continue loop so LLM can process tool results
                continue;
            }

            // No tool calls — return the final answer
            var content = message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            return new AgentChatResult { Reply = content, ToolsUsed = toolsUsed };
        }

        // Fallback if max rounds exceeded
        return new AgentChatResult
        {
            Reply = "I gathered data but couldn't finalize an answer. Please try rephrasing your question.",
            ToolsUsed = toolsUsed
        };
    }

    private async Task<string> CallLlmAsync(string url, string apiKey, string model, double temperature,
        List<object> messages, List<object> tools)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["messages"] = messages,
            ["model"] = model,
            ["stream"] = false,
            ["temperature"] = temperature,
            ["tools"] = tools,
            ["tool_choice"] = "auto"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Return the body even on error — the caller handles error JSON gracefully
        return responseBody;
    }
}
