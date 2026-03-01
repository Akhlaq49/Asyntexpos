using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class Warehouse
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Value { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
