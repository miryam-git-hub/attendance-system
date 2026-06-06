using System.ComponentModel.DataAnnotations;

namespace AttendanceSystemBackend.Models
{
    // אישור (passkey) רשום עבור עובד — מאחסן את המפתח הציבורי ומונה החתימות
    public class WebAuthnCredential
    {
        [Key]
        public int Id { get; set; }
        public int EmployeeId { get; set; }

        public byte[] CredentialId { get; set; } = Array.Empty<byte>();
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public byte[] UserHandle { get; set; } = Array.Empty<byte>();
        public byte[] AaGuid { get; set; } = Array.Empty<byte>();

        // Fido2 משתמש ב-uint; נשמר כ-long והומר בגבולות
        public long SignatureCounter { get; set; }

        public string CredType { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }

        public Employee Employee { get; set; } = null!;
    }
}
