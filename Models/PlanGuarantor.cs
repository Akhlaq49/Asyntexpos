using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Join table linking a Party (as guarantor) to an InstallmentPlan.
/// A party can guarantee multiple plans; a plan can have multiple guarantors.
/// </summary>
public class PlanGuarantor
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    public int PartyId { get; set; }

    [MaxLength(100)]
    public string? Relationship { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("PlanId")]
    public InstallmentPlan? Plan { get; set; }

    [ForeignKey("PartyId")]
    public Party? Party { get; set; }
}
