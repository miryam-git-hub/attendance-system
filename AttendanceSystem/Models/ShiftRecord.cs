using System.ComponentModel.DataAnnotations;

namespace AttendanceSystemBackend.Models
{
    public class ShiftRecord
    {
        [Key]
        public int RecordId { get; set; }
        public int EmployeeId { get; set; }
        public DateTime ClockIn { get; set; }
        public DateTime? ClockOut { get; set; }
        public decimal? TotalHours { get; set; }
        public DateTime ShiftDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Employee Employee { get; set; } = null!;
    }
}