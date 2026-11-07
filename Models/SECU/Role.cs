using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

/// <summary>A staff role of the company (Admin, Manager, Salesman ...). Users get one; menu access is set per role.</summary>
public class Role : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Built-in role (Admin): full access, can't be renamed or deleted</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
