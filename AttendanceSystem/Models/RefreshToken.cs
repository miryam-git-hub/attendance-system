using System.ComponentModel.DataAnnotations;

namespace AttendanceSystemBackend.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Employee Employee { get; set; } = null!;
    }
}