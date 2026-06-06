using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AttendanceSystemBackend.Data;
using AttendanceSystemBackend.Models;

var builder = WebApplication.CreateBuilder(args);

// ����� ���� �������
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ����� JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

// CORS � ����� �-React ����� �� ����
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// אחסון אתגרי WebAuthn בין begin ל-complete
builder.Services.AddMemoryCache();

// FIDO2 / WebAuthn (passkeys)
builder.Services.AddFido2(options =>
{
    options.ServerDomain = "localhost";                 // RP ID
    options.ServerName = "Attendance System";
    options.Origins = new HashSet<string> { "http://localhost:3000" };
    options.TimestampDriftTolerance = 300000;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed — יצירת אדמין ראשוני אם אין אף עובד במערכת
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // יצירת הסכימה אם אינה קיימת (אין migrations בפרויקט)
    db.Database.EnsureCreated();

    // EnsureCreated אינו מוסיף טבלאות למסד נתונים קיים — יוצרים את טבלת ה-passkeys ידנית אם חסרה
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[WebAuthnCredentials]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WebAuthnCredentials] (
        [Id]               INT             IDENTITY(1,1) NOT NULL,
        [EmployeeId]       INT             NOT NULL,
        [CredentialId]     VARBINARY(900)  NOT NULL,
        [PublicKey]        VARBINARY(MAX)  NOT NULL,
        [UserHandle]       VARBINARY(MAX)  NOT NULL,
        [AaGuid]           VARBINARY(MAX)  NOT NULL,
        [SignatureCounter] BIGINT          NOT NULL,
        [CredType]         NVARCHAR(MAX)   NOT NULL,
        [DeviceName]       NVARCHAR(MAX)   NULL,
        [CreatedAt]        DATETIME2       NOT NULL,
        [LastUsedAt]       DATETIME2       NULL,
        CONSTRAINT [PK_WebAuthnCredentials] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_WebAuthnCredentials_Employees_EmployeeId]
            FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([EmployeeId]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_WebAuthnCredentials_CredentialId]
        ON [dbo].[WebAuthnCredentials]([CredentialId]);
    CREATE INDEX [IX_WebAuthnCredentials_EmployeeId]
        ON [dbo].[WebAuthnCredentials]([EmployeeId]);
END
");

    if (!db.Employees.Any())
    {
        db.Employees.Add(new Employee
        {
            FullName = "System Admin",
            Email = "admin@company.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123"),
            Role = "Admin",
            IsActive = true
        });
        db.SaveChanges();

        app.Logger.LogInformation(
            "Seeded initial admin: admin@company.com / Admin123 (change the password after first login)");
    }
}



app.Run();