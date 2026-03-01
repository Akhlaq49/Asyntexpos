using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class LeaveType
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int Quota { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Leave> Leaves { get; set; } = new List<Leave>();
}
