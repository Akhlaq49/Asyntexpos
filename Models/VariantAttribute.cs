using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class VariantAttribute
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Comma-separated variant values (e.g. "XS, S, M, L, XL")</summary>
    [Required]
    public string Values { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
