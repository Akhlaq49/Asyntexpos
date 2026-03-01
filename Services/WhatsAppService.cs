using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReactPosApi.Services;

/// <summary>
/// WhatsApp Cloud API service implementation using Meta Graph API v21.0.
/// Docs: https://developers.facebook.com/docs/whatsapp/cloud-api
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly IConfiguration _configuration;

    private string? _accessToken;
    private string? _phoneNumberId;
    private string? _businessAccountId;

    private const string GraphApiBaseUrl = "https://graph.facebook.com/v22.0";

    public WhatsAppService(HttpClient httpClient, ILogger<WhatsAppService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Load from configuration
        _accessToken = _configuration["WhatsApp:AccessToken"];
        _phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
        _businessAccountId = _configuration["WhatsApp:BusinessAccountId"];
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_accessToken) &&
        !string.IsNullOrWhiteSpace(_phoneNumberId);

    public WhatsAppConfigStatus GetConfigStatus() => new()
    {
        IsConfigured = IsConfigured,
        PhoneNumberId = _phoneNumberId,
        HasAccessToken = !string.IsNullOrWhiteSpace(_accessToken),
        BusinessAccountId = _businessAccountId
    };

    public void UpdateConfig(WhatsAppConfigUpdate update)
    {
        if (!string.IsNullOrWhiteSpace(update.AccessToken))
            _accessToken = update.AccessToken;
        if (!string.IsNullOrWhiteSpace(update.PhoneNumberId))
            _phoneNumberId = update.PhoneNumberId;
        if (!string.IsNullOrWhiteSpace(update.BusinessAccountId))
            _businessAccountId = update.BusinessAccountId;

        _logger.LogInformation("WhatsApp Cloud API configuration updated.");
    }

    /// <summary>
    /// Send a text message via WhatsApp Cloud API.
    /// </summary>
    public async Task<WhatsAppSendResult> SendTextMessageAsync(string to, string message)
    {
        if (!IsConfigured)
            return new WhatsAppSendResult { Success = false, Error = "WhatsApp Cloud API is not configured." };

        try
        {
            var phone = NormalizePhone(to);
            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new
                {
                    preview_url = false,
                    body = message
                }
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"https://graph.facebook.com/v22.0/{_phoneNumberId}/messages",
                content);

            var result = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? new WhatsAppSendResult { Success = true, MessageId = JsonDocument.Parse(result).RootElement.GetProperty("messages")[0].GetProperty("id").GetString() }
                : new WhatsAppSendResult { Success = false, Error = $"WhatsApp API error: {response.StatusCode} - {result}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp text message to {To}", to);
            return new WhatsAppSendResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Send a document (PDF, etc.) via WhatsApp Cloud API.
    /// First uploads the media, then sends a document message.
    /// </summary>
    public async Task<WhatsAppSendResult> SendDocumentAsync(string to, byte[] fileBytes, string filename, string? caption = null)
    {
        if (!IsConfigured)
            return new WhatsAppSendResult { Success = false, Error = "WhatsApp Cloud API is not configured." };

        try
        {
            var phone = NormalizePhone(to);

            // Step 1: Upload the media
            var mediaId = await UploadMediaAsync(fileBytes, filename, "application/pdf");
            if (string.IsNullOrEmpty(mediaId))
                return new WhatsAppSendResult { Success = false, Error = "Failed to upload media to WhatsApp." };

            // Step 2: Send the document message
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phone,
                type = "document",
                document = new
                {
                    id = mediaId,
                    caption = caption ?? "",
                    filename = filename
                }
            };

            var result = await SendApiRequestAsync($"{GraphApiBaseUrl}/{_phoneNumberId}/messages", payload);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp document to {To}", to);
            return new WhatsAppSendResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Send a pre-approved template message.
    /// </summary>
    public async Task<WhatsAppSendResult> SendTemplateMessageAsync(string to, string templateName, string languageCode, List<string>? bodyParameters = null)
    {
        if (!IsConfigured)
            return new WhatsAppSendResult { Success = false, Error = "WhatsApp Cloud API is not configured." };

        try
        {
            var phone = NormalizePhone(to);

            var components = new List<object>();
            if (bodyParameters != null && bodyParameters.Count > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = bodyParameters.Select(p => new { type = "text", text = p }).ToArray()
                });
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = languageCode },
                    components = components
                }
            };

            var result = await SendApiRequestAsync($"{GraphApiBaseUrl}/{_phoneNumberId}/messages", payload);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp template message to {To}", to);
            return new WhatsAppSendResult { Success = false, Error = ex.Message };
        }
    }

    #region Private Helpers

    /// <summary>
    /// Upload media to WhatsApp Cloud API and return the media ID.
    /// </summary>
    private async Task<string?> UploadMediaAsync(byte[] fileBytes, string filename, string mimeType)
    {
        var url = $"{GraphApiBaseUrl}/{_phoneNumberId}/media";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent(mimeType), "type");

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(fileContent, "file", filename);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = form;

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WhatsApp media upload failed: {Status} {Response}", response.StatusCode, responseJson);
            return null;
        }

        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("id").GetString();
    }

    /// <summary>
    /// Send a request to the WhatsApp Cloud API.
    /// </summary>
    private async Task<WhatsAppSendResult> SendApiRequestAsync(string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WhatsApp API error: {Status} {Response}", response.StatusCode, responseJson);

            // Parse error message
            string errorMsg = $"WhatsApp API returned {response.StatusCode}";
            try
            {
                var errorDoc = JsonDocument.Parse(responseJson);
                if (errorDoc.RootElement.TryGetProperty("error", out var errorObj))
                {
                    errorMsg = errorObj.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString() ?? errorMsg
                        : errorMsg;
                }
            }
            catch { /* ignore parse errors */ }

            return new WhatsAppSendResult { Success = false, Error = errorMsg };
        }

        // Extract message ID
        string? messageId = null;
        try
        {
            var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.GetArrayLength() > 0)
            {
                messageId = messages[0].GetProperty("id").GetString();
            }
        }
        catch { /* ignore */ }

        _logger.LogInformation("WhatsApp message sent successfully. MessageId: {MessageId}", messageId);
        return new WhatsAppSendResult { Success = true, MessageId = messageId };
    }

    /// <summary>
    /// Normalize phone number to international format (digits only, with country code).
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        // Remove all non-digit characters
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // If starts with 0, assume Pakistan (+92) and replace leading 0
        if (digits.StartsWith("0"))
            digits = "92" + digits[1..];

        // If doesn't start with country code, prepend 92 (Pakistan)
        if (digits.Length == 10)
            digits = "92" + digits;

        return digits;
    }

    #endregion
}
