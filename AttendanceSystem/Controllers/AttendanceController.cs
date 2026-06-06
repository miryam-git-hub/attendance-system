using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AttendanceSystemBackend.Data;
using AttendanceSystemBackend.Models;

namespace AttendanceSystemBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;

        public AttendanceController(AppDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClient = httpClientFactory.CreateClient();
        }

        // שליפת שעה מ-API חיצוני
        private async Task<DateTime> GetZurichTimeAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<TimeApiResponse>(
                "https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich");

            if (response == null)
                throw new Exception("לא ניתן לקבל שעה מה-API החיצוני");

            return response.DateTime;
        }

        // Clock In
        [HttpPost("clock-in")]
        public async Task<IActionResult> ClockIn()
        {
            var employeeId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // בדיקה שאין משמרת פתוחה
            var openShift = await _db.ShiftRecords
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId
                                       && s.ClockOut == null);

            if (openShift != null)
                return BadRequest("יש לך משמרת פתוחה — יש לבצע Clock Out קודם");

            // שליפת שעה מ-API חיצוני
            DateTime zurichTime;
            try
            {
                zurichTime = await GetZurichTimeAsync();
            }
            catch
            {
                return StatusCode(503,
                    "שירות השעה אינו זמין כרגע — לא ניתן להחתים כניסה");
            }

            // יצירת משמרת חדשה
            var shift = new ShiftRecord
            {
                EmployeeId = employeeId,
                ClockIn = zurichTime,
                ShiftDate = zurichTime.Date
            };

            _db.ShiftRecords.Add(shift);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "כניסה נרשמה בהצלחה",
                clockIn = zurichTime,
                shift.RecordId
            });
        }

        // Clock Out
        [HttpPost("clock-out")]
        public async Task<IActionResult> ClockOut()
        {
            var employeeId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // חיפוש משמרת פתוחה
            var openShift = await _db.ShiftRecords
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId
                                       && s.ClockOut == null);

            if (openShift == null)
                return BadRequest("אין משמרת פתוחה — יש לבצע Clock In קודם");

            // שליפת שעה מ-API חיצוני
            DateTime zurichTime;
            try
            {
                zurichTime = await GetZurichTimeAsync();
            }
            catch
            {
                return StatusCode(503,
                    "שירות השעה אינו זמין כרגע — לא ניתן להחתים יציאה");
            }

            // עדכון המשמרת
            openShift.ClockOut = zurichTime;
            openShift.TotalHours = (decimal)(zurichTime - openShift.ClockIn).TotalHours;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "יציאה נרשמה בהצלחה",
                clockIn = openShift.ClockIn,
                clockOut = zurichTime,
                totalHours = openShift.TotalHours
            });
        }

        // היסטוריית משמרות של העובד
        [HttpGet("my-shifts")]
        public async Task<IActionResult> GetMyShifts()
        {
            var employeeId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var shifts = await _db.ShiftRecords
                .Where(s => s.EmployeeId == employeeId)
                .OrderByDescending(s => s.ShiftDate)
                .Select(s => new
                {
                    s.RecordId,
                    s.ClockIn,
                    s.ClockOut,
                    s.TotalHours,
                    s.ShiftDate,
                    Status = s.ClockOut == null ? "פתוחה" : "סגורה"
                })
                .ToListAsync();

            return Ok(shifts);
        }
    }

    public class TimeApiResponse
    {
        public DateTime DateTime { get; set; }
    }
}