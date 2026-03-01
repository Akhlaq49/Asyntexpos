using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class Leave
{
    [Key]
    public int Id { get; set; }

    /// <summary>Employee who applied – FK to Parties</summary>
    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Party Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public LeaveType LeaveType { get; set; } = null!;

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public decimal Days { get; set; }

    [MaxLength(20)]
    public string DayType { get; set; } = "Full Day";  // Full Day / Half Day

    public string? Reason { get; set; }

    /// <summary>New, Approved, Rejected</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "New";

    /// <summary>Approved/Rejected by – FK to Parties</summary>
    public int? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public Party? ApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
