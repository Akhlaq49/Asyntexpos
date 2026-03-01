using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly IWebHostEnvironment _env;

    public WhatsAppController(IWhatsAppService whatsAppService, IWebHostEnvironment env)
    {
        _whatsAppService = whatsAppService;
        _env = env;
    }

    /// <summary>
    /// Get WhatsApp Cloud API configuration status.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var status = _whatsAppService.GetConfigStatus();
        return Ok(status);
    }

    /// <summary>
    /// Update WhatsApp Cloud API configuration.
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateConfig([FromBody] WhatsAppConfigUpdate update)
    {
        _whatsAppService.UpdateConfig(update);
        return Ok(new { message = "WhatsApp configuration updated successfully.", status = _whatsAppService.GetConfigStatus() });
    }

    /// <summary>
    /// Send a text message via WhatsApp Cloud API.
    /// </summary>
    [HttpPost("send/text")]
    public async Task<IActionResult> SendText([FromBody] SendTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Phone number and message are required." });

        var result = await _whatsAppService.SendTextMessageAsync(request.To, request.Message);

        if (!result.Success)
            return StatusCode(502, new { error = result.Error });

        return Ok(new { messageId = result.MessageId, message = "Message sent successfully." });
    }

    /// <summary>
    /// Send a document (PDF) via WhatsApp Cloud API.
    /// Accepts multipart form data with the file and metadata.
    /// </summary>
    [HttpPost("send/document")]
    public async Task<IActionResult> SendDocument([FromForm] SendDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To) || request.File == null || request.File.Length == 0)
            return BadRequest(new { error = "Phone number and file are required." });

        using var ms = new MemoryStream();
        await request.File.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        var filename = request.Filename ?? request.File.FileName;
        var result = await _whatsAppService.SendDocumentAsync(request.To, fileBytes, filename, request.Caption);

        if (!result.Success)
            return StatusCode(502, new { error = result.Error });

        return Ok(new { messageId = result.MessageId, message = "Document sent successfully." });
    }

    /// <summary>
    /// Send a text message + document (PDF) in sequence via WhatsApp Cloud API.
    /// Accepts multipart form data.
    /// </summary>
    [HttpPost("send/text-and-document")]
    public async Task<IActionResult> SendTextAndDocument([FromForm] SendTextAndDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest(new { error = "Phone number is required." });

        var results = new List<object>();

        // Send text message first (if provided)
        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            var textResult = await _whatsAppService.SendTextMessageAsync(request.To, request.Message);
            results.Add(new { type = "text", success = textResult.Success, messageId = textResult.MessageId, error = textResult.Error });
        }

        // Send document (if provided)
        if (request.File != null && request.File.Length > 0)
        {
            using var ms = new MemoryStream();
            await request.File.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var filename = request.Filename ?? request.File.FileName;

            var docResult = await _whatsAppService.SendDocumentAsync(request.To, fileBytes, filename, request.Caption);
            results.Add(new { type = "document", success = docResult.Success, messageId = docResult.MessageId, error = docResult.Error });
        }

        var allSuccess = results.All(r => ((dynamic)r).success);
        return Ok(new { success = allSuccess, results });
    }

    /// <summary>
    /// Send a template message via WhatsApp Cloud API.
    /// </summary>
    [HttpPost("send/template")]
    public async Task<IActionResult> SendTemplate([FromBody] SendTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.TemplateName))
            return BadRequest(new { error = "Phone number and template name are required." });

        var result = await _whatsAppService.SendTemplateMessageAsync(
            request.To,
            request.TemplateName,
            request.LanguageCode ?? "en",
            request.BodyParameters
        );

        if (!result.Success)
            return StatusCode(502, new { error = result.Error });

        return Ok(new { messageId = result.MessageId, message = "Template message sent successfully." });
    }

    /// <summary>
    /// Upload a PDF to the server and return a public download URL.
    /// Used for sharing PDFs via WhatsApp wa.me links (no API required).
    /// Files are stored in wwwroot/shares/ and auto-cleaned after 24 hours.
    /// </summary>
    [HttpPost("upload-pdf")]
    public async Task<IActionResult> UploadPdf([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > 25 * 1024 * 1024) // 25MB limit
            return BadRequest(new { error = "File too large. Maximum 25MB." });

        var sharesDir = Path.Combine(_env.WebRootPath, "shares");
        if (!Directory.Exists(sharesDir))
            Directory.CreateDirectory(sharesDir);

        // Clean up old files (older than 24h)
        try
        {
            foreach (var oldFile in System.IO.Directory.GetFiles(sharesDir))
            {
                if (System.IO.File.GetCreationTimeUtc(oldFile) < DateTime.UtcNow.AddHours(-24))
                    System.IO.File.Delete(oldFile);
            }
        }
        catch { /* ignore cleanup errors */ }

        // Save with unique name
        var uniqueName = $"{Guid.NewGuid():N}.pdf";
        var filePath = Path.Combine(sharesDir, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Build the public URL
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var publicUrl = $"{baseUrl}/shares/{uniqueName}";

        return Ok(new { url = publicUrl, filename = uniqueName });
    }
}

// Request DTOs
public class SendTextRequest
{
    public string To { get; set; } = "";
    public string Message { get; set; } = "";
}

public class SendDocumentRequest
{
    public string To { get; set; } = "";
    public IFormFile? File { get; set; }
    public string? Filename { get; set; }
    public string? Caption { get; set; }
}

public class SendTextAndDocumentRequest
{
    public string To { get; set; } = "";
    public string? Message { get; set; }
    public IFormFile? File { get; set; }
    public string? Filename { get; set; }
    public string? Caption { get; set; }
}

public class SendTemplateRequest
{
    public string To { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string? LanguageCode { get; set; } = "en";
    public List<string>? BodyParameters { get; set; }
}
