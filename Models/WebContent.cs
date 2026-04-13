using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class WebContent : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>
    /// Section type: "header", "slider", "banner", "near_slider", "footer"
    /// </summary>
    [Required, MaxLength(50)]
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Display title for the content block.
    /// </summary>
    [MaxLength(500)]
    public string? Title { get; set; }

    /// <summary>
    /// Subtitle or short description.
    /// </summary>
    [MaxLength(1000)]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Rich HTML or plain-text body content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Image URL or path (stored via FileService).
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Link/URL the content should navigate to when clicked.
    /// </summary>
    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Call-to-action button text (e.g. "Shop Now").
    /// </summary>
    [MaxLength(200)]
    public string? ButtonText { get; set; }

    /// <summary>
    /// Display order within the section (lower = first).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// active / inactive
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
