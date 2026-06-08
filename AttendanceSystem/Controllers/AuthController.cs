using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AttendanceSystemBackend.Data;
using AttendanceSystemBackend.Models;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace AttendanceSystemBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IFido2 _fido2;
        private readonly IMemoryCache _cache;

        public AuthController(AppDbContext db, IConfiguration config, IFido2 fido2, IMemoryCache cache)
        {
            _db = db;
            _config = config;
            _fido2 = fido2;
            _cache = cache;
        }

        // התחברות
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.IdNumber == request.IdNumber && e.IsActive);

            if (employee == null)
                return Unauthorized("עובד לא נמצא עם ת.ז: " + request.IdNumber);
            Console.WriteLine("INPUT PASSWORD: " + request.Password);
            Console.WriteLine("HASH FROM DB: " + employee.PasswordHash);

            if (!BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
                return Unauthorized("סיסמה שגויה");

            var accessToken = GenerateAccessToken(employee);
            var refreshToken = await GenerateRefreshTokenAsync(employee.EmployeeId);

            return Ok(new
            {
                accessToken,
                refreshToken = refreshToken.Token,
                employee.EmployeeId,
                employee.FullName,
                employee.Email,
                employee.Role
            });
        }
        // חידוש טוקן
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var refreshToken = await _db.RefreshTokens
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Token == request.RefreshToken
                                       && !r.IsRevoked
                                       && r.ExpiryDate > DateTime.UtcNow);

            if (refreshToken == null)
                return Unauthorized("Refresh Token לא תקין או פג תוקף");

            // ביטול הטוקן הישן
            refreshToken.IsRevoked = true;

            // יצירת טוקנים חדשים
            var newAccessToken = GenerateAccessToken(refreshToken.Employee);
            var newRefreshToken = await GenerateRefreshTokenAsync(refreshToken.EmployeeId);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken.Token
            });
        }

        // התנתקות
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
        {
            var refreshToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

            if (refreshToken != null)
            {
                refreshToken.IsRevoked = true;
                await _db.SaveChangesAsync();
            }

            return Ok("התנתקות בוצעה בהצלחה");
        }

        // ===================== WebAuthn / Passkeys =====================

        // התחלת רישום passkey עבור העובד המחובר
        [Authorize]
        [HttpPost("passkey/register/begin")]
        public async Task<IActionResult> PasskeyRegisterBegin()
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null)
                return Unauthorized();

            var existingIds = await _db.WebAuthnCredentials
                .Where(c => c.EmployeeId == employeeId)
                .Select(c => c.CredentialId)
                .ToListAsync();
            var excludeCredentials = existingIds
                .Select(id => new PublicKeyCredentialDescriptor(id))
                .ToList();

            var user = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(employeeId.ToString()),
                Name = employee.Email,
                DisplayName = employee.FullName
            };

            var authenticatorSelection = new AuthenticatorSelection
            {
                RequireResidentKey = true,   // discoverable credential — לוגין ללא אימייל
                UserVerification = UserVerificationRequirement.Required
            };

            var options = _fido2.RequestNewCredential(
                user, excludeCredentials, authenticatorSelection,
                AttestationConveyancePreference.None);

            _cache.Set("fido2.reg." + employeeId, options.ToJson(), TimeSpan.FromMinutes(5));

            return Content(options.ToJson(), "application/json");
        }

        // סיום רישום passkey — אימות ושמירת האישור
        [Authorize]
        [HttpPost("passkey/register/complete")]
        public async Task<IActionResult> PasskeyRegisterComplete([FromBody] PasskeyRegisterCompleteRequest request)
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (!_cache.TryGetValue("fido2.reg." + employeeId, out string? optionsJson) || optionsJson == null)
                return BadRequest("פג תוקף בקשת הרישום — נסה שוב");

            var options = CredentialCreateOptions.FromJson(optionsJson);

            IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (args, ct) =>
                !await _db.WebAuthnCredentials.AnyAsync(c => c.CredentialId == args.CredentialId, ct);

            Fido2.CredentialMakeResult result;
            try
            {
                result = await _fido2.MakeNewCredentialAsync(request.AttestationResponse, options, isUnique);
            }
            catch (Fido2VerificationException ex)
            {
                return BadRequest("רישום ה-passkey נכשל: " + ex.Message);
            }

            if (result.Result == null)
                return BadRequest("רישום ה-passkey נכשל");

            var cred = new WebAuthnCredential
            {
                EmployeeId = employeeId,
                CredentialId = result.Result.CredentialId,
                PublicKey = result.Result.PublicKey,
                UserHandle = result.Result.User.Id,
                AaGuid = result.Result.Aaguid.ToByteArray(),
                SignatureCounter = result.Result.Counter,
                CredType = result.Result.CredType,
                DeviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                    ? "Passkey" : request.DeviceName.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.WebAuthnCredentials.Add(cred);
            await _db.SaveChangesAsync();
            _cache.Remove("fido2.reg." + employeeId);

            return Ok(new { cred.Id, cred.DeviceName, cred.CreatedAt });
        }

        // התחלת התחברות passkey — usernameless (ללא רשימת אישורים)
        [AllowAnonymous]
        [HttpPost("passkey/login/begin")]
        public IActionResult PasskeyLoginBegin()
        {
            var options = _fido2.GetAssertionOptions(
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required);

            var flowId = Guid.NewGuid().ToString("N");
            _cache.Set("fido2.login." + flowId, options.ToJson(), TimeSpan.FromMinutes(5));

            var body = "{\"flowId\":\"" + flowId + "\",\"options\":" + options.ToJson() + "}";
            return Content(body, "application/json");
        }

        // סיום התחברות passkey — אימות וקבלת טוקנים (אותו פורמט כמו התחברות רגילה)
        [AllowAnonymous]
        [HttpPost("passkey/login/complete")]
        public async Task<IActionResult> PasskeyLoginComplete([FromBody] PasskeyLoginCompleteRequest request)
        {
            if (!_cache.TryGetValue("fido2.login." + request.FlowId, out string? optionsJson) || optionsJson == null)
                return Unauthorized("פג תוקף בקשת ההתחברות — נסה שוב");

            var options = AssertionOptions.FromJson(optionsJson);

            var credId = request.AssertionResponse.Id;
            var cred = await _db.WebAuthnCredentials.FirstOrDefaultAsync(c => c.CredentialId == credId);
            if (cred == null)
                return Unauthorized("האישור אינו מוכר");

            IsUserHandleOwnerOfCredentialIdAsync isOwner = async (args, ct) =>
                await _db.WebAuthnCredentials.AnyAsync(
                    c => c.CredentialId == args.CredentialId && c.UserHandle == args.UserHandle, ct);

            AssertionVerificationResult result;
            try
            {
                result = await _fido2.MakeAssertionAsync(
                    request.AssertionResponse, options, cred.PublicKey,
                    (uint)cred.SignatureCounter, isOwner);
            }
            catch (Fido2VerificationException ex)
            {
                return Unauthorized("אימות ה-passkey נכשל: " + ex.Message);
            }

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == cred.EmployeeId && e.IsActive);
            if (employee == null)
                return Unauthorized("המשתמש אינו פעיל");

            cred.SignatureCounter = result.Counter;
            cred.LastUsedAt = DateTime.UtcNow;

            var accessToken = GenerateAccessToken(employee);
            var refreshToken = await GenerateRefreshTokenAsync(employee.EmployeeId);
            await _db.SaveChangesAsync();
            _cache.Remove("fido2.login." + request.FlowId);

            return Ok(new
            {
                accessToken,
                refreshToken = refreshToken.Token,
                employee.EmployeeId,
                employee.FullName,
                employee.Email,
                employee.Role
            });
        }

        // רשימת ה-passkeys של העובד המחובר
        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> ListPasskeys()
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var list = await _db.WebAuthnCredentials
                .Where(c => c.EmployeeId == employeeId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new { c.Id, c.DeviceName, c.CreatedAt, c.LastUsedAt })
                .ToListAsync();
            return Ok(list);
        }

        // מחיקת passkey (בעלות בלבד)
        [Authorize]
        [HttpDelete("passkeys/{id}")]
        public async Task<IActionResult> DeletePasskey(int id)
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var cred = await _db.WebAuthnCredentials
                .FirstOrDefaultAsync(c => c.Id == id && c.EmployeeId == employeeId);
            if (cred == null)
                return NotFound();

            _db.WebAuthnCredentials.Remove(cred);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private string GenerateAccessToken(Employee employee)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, employee.EmployeeId.ToString()),
                new Claim(ClaimTypes.Email, employee.Email),
                new Claim(ClaimTypes.Role, employee.Role),
                new Claim(ClaimTypes.Name, employee.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(
                    key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<RefreshToken> GenerateRefreshTokenAsync(int employeeId)
        {
            var refreshToken = new RefreshToken
            {
                EmployeeId = employeeId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiryDate = DateTime.UtcNow.AddDays(7)
            };

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            return refreshToken;
        }
    }

    public class LoginRequest
    {
        
            public string IdNumber { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }

    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class PasskeyRegisterCompleteRequest
    {
        public AuthenticatorAttestationRawResponse AttestationResponse { get; set; } = null!;
        public string? DeviceName { get; set; }
    }

    public class PasskeyLoginCompleteRequest
    {
        public string FlowId { get; set; } = string.Empty;
        public AuthenticatorAssertionRawResponse AssertionResponse { get; set; } = null!;
    }
