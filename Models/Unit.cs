using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Unit
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Value { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
