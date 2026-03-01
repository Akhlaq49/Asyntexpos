using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Category
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    // Navigation
    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
