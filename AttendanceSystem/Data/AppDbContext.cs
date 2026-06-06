using Microsoft.EntityFrameworkCore;
using AttendanceSystemBackend.Models;

namespace AttendanceSystemBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<ShiftRecord> ShiftRecords { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // הגדרת הקשר בין עובד למשמרות
            modelBuilder.Entity<ShiftRecord>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.ShiftRecords)
                .HasForeignKey(s => s.EmployeeId);

            // דיוק בשדה TotalHours
            modelBuilder.Entity<ShiftRecord>()
                .Property(s => s.TotalHours)
                .HasColumnType("decimal(5,2)");

            // הגדרת אישורי WebAuthn (passkeys)
            modelBuilder.Entity<WebAuthnCredential>(b =>
            {
                b.HasOne(c => c.Employee)
                    .WithMany(e => e.WebAuthnCredentials)
                    .HasForeignKey(c => c.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // מוגבל ל-900 בתים כדי שניתן יהיה ליצור אינדקס ייחודי (varbinary(max) אינו חוקי לאינדקס)
                b.Property(c => c.CredentialId).HasColumnType("varbinary(900)");
                b.HasIndex(c => c.CredentialId).IsUnique();
            });
        }
    }
}