namespace ReactPosApi.Services;

/// <summary>
/// WhatsApp Cloud API integration service.
/// Uses Meta's Graph API to send messages via WhatsApp Business Platform.
/// </summary>
public interface IWhatsAppService
{
    /// <summary>Send a plain text message to a phone number.</summary>
    Task<WhatsAppSendResult> SendTextMessageAsync(string to, string message);

    /// <summary>Send a document (e.g. PDF) with an optional caption.</summary>
    Task<WhatsAppSendResult> SendDocumentAsync(string to, byte[] fileBytes, string filename, string? caption = null);

    /// <summary>Send a pre-approved template message.</summary>
    Task<WhatsAppSendResult> SendTemplateMessageAsync(string to, string templateName, string languageCode, List<string>? bodyParameters = null);

    /// <summary>Check if WhatsApp Cloud API is configured and ready.</summary>
    bool IsConfigured { get; }

    /// <summary>Get current configuration status (without exposing secrets).</summary>
    WhatsAppConfigStatus GetConfigStatus();

    /// <summary>Update configuration at runtime.</summary>
    void UpdateConfig(WhatsAppConfigUpdate update);
}

public class WhatsAppSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? Error { get; set; }
}

public class WhatsAppConfigStatus
{
    public bool IsConfigured { get; set; }
    public string? PhoneNumberId { get; set; }
    public bool HasAccessToken { get; set; }
    public string? BusinessAccountId { get; set; }
}

public class WhatsAppConfigUpdate
{
    public string? AccessToken { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
}
