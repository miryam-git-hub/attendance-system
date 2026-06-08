using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AttendanceSystemBackend.Data;
using AttendanceSystemBackend.Models;

namespace AttendanceSystemBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EmployeesController(AppDbContext db)
        {
            _db = db;
        }

        // יצירת עובד חדש — מנהלים בלבד (ללא הנפקת טוקנים — לא משנה את ההתחברות הנוכחית)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
        {
            // ולידציה בסיסית
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.IdNumber)||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("שם מלא, אימייל וסיסמה הם שדות חובה");

            if (request.Password.Length < 6)
                return BadRequest("הסיסמה חייבת להכיל לפחות 6 תווים");

            // תפקיד חוקי בלבד
            var role = string.IsNullOrWhiteSpace(request.Role) ? "Employee" : request.Role.Trim();
            if (role != "Employee" && role != "Admin")
                return BadRequest("תפקיד לא חוקי — יש לבחור Employee או Admin");

            var email = request.Email.Trim().ToLower();

            // בדיקה שהאימייל אינו תפוס
            var exists = await _db.Employees.AnyAsync(e => e.Email == email);
            if (exists)
                return Conflict("כתובת האימייל כבר רשומה במערכת");

            // יצירת העובד
            var employee = new Employee
            {    
               IdNumber = request.IdNumber.Trim(),
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                IsActive = true
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            // החזרת פרטי העובד שנוצר — ללא טוקנים, ללא סיסמה
            return CreatedAtAction(nameof(Create), new
            {
                employee.EmployeeId,
                employee.IdNumber,
                employee.FullName,
                employee.Email,
                employee.Role,
                employee.IsActive,
                employee.CreatedAt
            });
        }
    }

    public class CreateEmployeeRequest
    {
        public string IdNumber { get; set; }=string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";
    }
}
