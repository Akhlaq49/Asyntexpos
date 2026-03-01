using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Shift
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? StartTime { get; set; }

    [MaxLength(20)]
    public string? EndTime { get; set; }

    [MaxLength(200)]
    public string? WeekOff { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
