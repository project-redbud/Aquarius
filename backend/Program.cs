using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Aquarius.Api.Data;
using Aquarius.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────
builder.Services.AddDbContext<AquariusDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") 
        ?? "Data Source=Aquarius.db"));

// ── Controllers ───────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Force all DateTime to serialize as UTC (fixes SQLite timezone loss)
        opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });

// ── CORS (dev: allow Angular dev server from any local addr) ──
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// ── JWT Authentication ───────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? "AquariusSecretKey_ChangeInProduction_2026!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Aquarius",
            ValidAudience = "AquariusApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ── Services ──────────────────────────────────────────────
builder.Services.AddSingleton<Aquarius.Api.Services.EmailService>();

// ── Swagger / OpenAPI ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Auto-migrate & seed ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AquariusDbContext>();
    db.Database.EnsureCreated();

    // 增量迁移（列已存在时自动跳过）
    Migrate(db);
    db.SaveChanges();
}

static void Migrate(AquariusDbContext db)
{
    var sqls = new[]
    {
        // Comments
        "ALTER TABLE Comments ADD COLUMN IsBottleOwnerBadge INTEGER NOT NULL DEFAULT 0",
        // Users
        "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'user'",
        "ALTER TABLE Users ADD COLUMN IsBanned INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Users ADD COLUMN BanReason TEXT NULL",
        "ALTER TABLE Users ADD COLUMN BannedUntil TEXT NULL",
        "ALTER TABLE Users ADD COLUMN EmailVerified INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Users ADD COLUMN EmailVerifyToken TEXT NULL",
        "ALTER TABLE Users ADD COLUMN ResetPasswordToken TEXT NULL",
        "ALTER TABLE Users ADD COLUMN ResetPasswordExpires TEXT NULL",
        // Bottles
        "ALTER TABLE Bottles ADD COLUMN ReportedBottleId INTEGER NULL",
        // SiteSettings
        "ALTER TABLE SiteSettings ADD COLUMN SmtpHost TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE SiteSettings ADD COLUMN SmtpPort INTEGER NOT NULL DEFAULT 587",
        "ALTER TABLE SiteSettings ADD COLUMN SmtpUser TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE SiteSettings ADD COLUMN SmtpPassword TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE SiteSettings ADD COLUMN SmtpFrom TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE SiteSettings ADD COLUMN SmtpEnableSsl INTEGER NOT NULL DEFAULT 1",
        "ALTER TABLE SiteSettings ADD COLUMN SiteBaseUrl TEXT NOT NULL DEFAULT ''",
        // BottleLogs table + Bottle close state
        "CREATE TABLE IF NOT EXISTS BottleLogs (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, BottleId INTEGER NOT NULL, OperatorUserId INTEGER NULL, OperatorUsername TEXT NOT NULL DEFAULT '', Action TEXT NOT NULL DEFAULT '', Detail TEXT NULL, CreatedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00', FOREIGN KEY (BottleId) REFERENCES Bottles(Id) ON DELETE CASCADE, FOREIGN KEY (OperatorUserId) REFERENCES Users(Id) ON DELETE SET NULL)",
        "ALTER TABLE Bottles ADD COLUMN IsClosed INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Bottles ADD COLUMN ClosedAt TEXT NULL",
        "ALTER TABLE Bottles ADD COLUMN ClosedByUserId INTEGER NULL",
        // Notifications table + User settings
        "CREATE TABLE IF NOT EXISTS Notifications (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, Type TEXT NOT NULL DEFAULT 'system', Title TEXT NOT NULL DEFAULT '', Content TEXT NOT NULL DEFAULT '', RelatedBottleId INTEGER NULL, IsRead INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00', FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE)",
        "ALTER TABLE Users ADD COLUMN NotifyPreference TEXT NOT NULL DEFAULT 'default'",
        "ALTER TABLE Users ADD COLUMN ViewPrivateComments INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Users ADD COLUMN NewEmail TEXT NULL",
        "ALTER TABLE Users ADD COLUMN NewEmailVerifyToken TEXT NULL",
        "ALTER TABLE Users ADD COLUMN ThrowAnonymous INTEGER NOT NULL DEFAULT 1",
        "ALTER TABLE Users ADD COLUMN DefaultAuthorName TEXT NULL",
    };

    foreach (var sql in sqls)
    {
        try { db.Database.ExecuteSqlRaw(sql); }
        catch { /* 列已存在，跳过 */ }
    }

    // 回填管理员角色
    db.Database.ExecuteSqlRaw("UPDATE Users SET Role = 'admin' WHERE IsAdmin = 1 AND Role = 'user'");

    // 回填邮箱验证状态（已有用户视为已验证）
    db.Database.ExecuteSqlRaw("UPDATE Users SET EmailVerified = 1 WHERE EmailVerified = 0 AND Email IS NOT NULL AND Email != ''");

    // 确保 SiteSettings 表存在（旧库没有此表）
    db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS SiteSettings (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, SiteName TEXT NOT NULL DEFAULT 'Aquarius', Copyright TEXT NOT NULL DEFAULT '')");

    // 种子 SiteSettings
    if (!db.SiteSettings.Any())
    {
        db.SiteSettings.Add(new SiteSettings { SiteName = "Aquarius", Copyright = "" });
    }
}

// ── Middleware pipeline ───────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Auth middleware
app.UseAuthentication();
app.UseAuthorization();

// Serve Angular static files from wwwroot (production mode)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// SPA fallback — return index.html for any non-API route
app.MapFallbackToFile("index.html");

app.Run();

// ────────────────────────────────────────────────────────────
/// <summary>Ensure DateTime values always serialize with Z suffix,
/// so Angular date pipe correctly converts UTC to local time.</summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime());
    }
}
