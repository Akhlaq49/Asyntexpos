using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class SmsMessage : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>Recipient phone number (e.g. 923001234567)</summary>
    [Required, MaxLength(20)]
    public string To { get; set; } = string.Empty;

    /// <summary>SMS body text</summary>
    [Required, MaxLength(1600)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Channel: sms or whatsapp</summary>
    [Required, MaxLength(20)]
    public string Channel { get; set; } = "sms";

    /// <summary>pending → processing → sent / failed</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending";

    /// <summary>Error message if delivery failed</summary>
    [MaxLength(500)]
    public string? Error { get; set; }

    /// <summary>Optional reference (e.g. sale ID, invoice ID)</summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    /// <summary>Optional media URL (e.g. PDF invoice link)</summary>
    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
