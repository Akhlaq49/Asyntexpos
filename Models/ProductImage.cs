using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class ProductImage
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required, MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("ProductId")]
    public Product? Product { get; set; }
}
