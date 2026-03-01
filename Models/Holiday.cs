using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Holiday
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public int Days { get; set; }

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
