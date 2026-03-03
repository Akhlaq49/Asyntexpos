using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class SubCategory : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(200)]
    public string SubCategoryName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    [MaxLength(100)]
    public string? CategoryCode { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Image { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("CategoryId")]
    public Category? Category { get; set; }
}
