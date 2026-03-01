using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models
{
    public class Attendance
    {
        [Key]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Party? Employee { get; set; }

        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Present"; // Present, Absent, Holiday

        public TimeSpan? ClockIn { get; set; }

        public TimeSpan? ClockOut { get; set; }

        [MaxLength(20)]
        public string? Production { get; set; }

        [MaxLength(20)]
        public string? BreakTime { get; set; }

        [MaxLength(20)]
        public string? Overtime { get; set; }

        [MaxLength(20)]
        public string? TotalHours { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
