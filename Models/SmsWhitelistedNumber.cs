using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class SmsWhitelistedNumber
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
