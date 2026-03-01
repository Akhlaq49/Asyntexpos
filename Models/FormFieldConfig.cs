using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class FormFieldConfig
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FormName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FieldLabel { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
