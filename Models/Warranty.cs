using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Warranty
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Duration { get; set; }

    [MaxLength(20)]
    public string Period { get; set; } = "Month";  // "Month" or "Year"

    [MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
