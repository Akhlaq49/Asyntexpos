using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class PlanMedia : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int PlanId { get; set; }

    /// <summary>
    /// "customer", "guarantor", or "plan"
    /// </summary>
    [Required, MaxLength(20)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// PartyId for customer/guarantor; null for plan-level media (e.g. video)
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// "image" or "video"
    /// </summary>
    [Required, MaxLength(20)]
    public string MediaType { get; set; } = "image";

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PlanId")]
    public InstallmentPlan? Plan { get; set; }
}
