namespace AttendanceSystemBackend.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ShiftRecord> ShiftRecords { get; set; } = new List<ShiftRecord>();
        public ICollection<WebAuthnCredential> WebAuthnCredentials { get; set; } = new List<WebAuthnCredential>();
    }
}
