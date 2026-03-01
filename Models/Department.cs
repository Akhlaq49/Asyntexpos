using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class Department
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Head of Department – FK to Parties table</summary>
    public int? HODId { get; set; }

    [ForeignKey(nameof(HODId))]
    public Party? HOD { get; set; }

    public string? Description { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Designation> Designations { get; set; } = new List<Designation>();
}
