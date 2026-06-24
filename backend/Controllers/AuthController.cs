using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Aquarius.Api.Data;
using Aquarius.Api.Models;
using Aquarius.Api.Services;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly IConfiguration _config;
    private readonly EmailService _email;

    public AuthController(AquariusDbContext db, IConfiguration config, EmailService email)
    {
        _db = db;
        _config = config;
        _email = email;
    }

    /// <summary>注册 — 发送验证邮件，不直接登录</summary>
    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest req)
    {
        // 验证
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 2)
            return BadRequest(new { error = "用户名至少 2 个字符" });
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { error = "请输入有效的电子邮件地址" });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "密码至少 6 个字符" });
        if (req.Password != req.ConfirmPassword)
            return BadRequest(new { error = "两次输入的密码不一致" });

        // 唯一性检查
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { error = "用户名已被注册" });
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { error = "电子邮件已被注册" });

        // 首个注册用户自动成为管理员
        var isFirst = !await _db.Users.AnyAsync();

        var verifyToken = GenerateCryptoToken();

        var user = new User
        {
            Username = req.Username.Trim(),
            Email = req.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsAdmin = isFirst,
            Role = isFirst ? "admin" : "user",
            EmailVerified = false,
            EmailVerifyToken = verifyToken,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 发送验证邮件
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            _email.SendVerificationBackground(settings, user.Email, verifyToken);

        return Ok(new { message = "注册成功！请查收验证邮件并激活账号。" });
    }

    /// <summary>登录（用户名或邮箱 + 密码）</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "请输入登录名和密码" });

        // 按用户名或邮箱查找
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == req.Login || u.Email == req.Login);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "用户名/邮箱或密码错误" });

        if (!user.EmailVerified)
            return Unauthorized(new { error = "邮箱尚未验证，请先查收激活邮件" });

        if (user.IsBanned)
        {
            var banMsg = "账号已被封禁";
            if (!string.IsNullOrWhiteSpace(user.BanReason))
                banMsg += $"：{user.BanReason}";
            if (user.BannedUntil.HasValue && user.BannedUntil > DateTime.UtcNow)
                banMsg += $"（至 {user.BannedUntil:yyyy-MM-dd HH:mm}）";
            return Unauthorized(new { error = banMsg });
        }

        var token = GenerateToken(user);
        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            Role = user.Role
        });
    }

    /// <summary>邮箱验证 — 点击邮件中的链接激活账号</summary>
    [HttpPost("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = "验证令牌无效" });

        // 先查找邮箱变更验证令牌
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NewEmailVerifyToken == req.Token);
        if (user != null)
        {
            var oldEmail = user.Email;
            user.Email = user.NewEmail!;
            user.EmailVerified = true;
            user.NewEmail = null;
            user.NewEmailVerifyToken = null;
            await _db.SaveChangesAsync();

            // 后台通知旧邮箱
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings != null)
                _email.SendBackground(settings, oldEmail, "你的 Aquarius 邮箱已变更",
                    $"<p>你的 Aquarius 账号邮箱已从 {oldEmail} 变更为 {user.Email}。</p><p>如果不是你本人操作，请立即联系管理员。</p>");

            var jwt = GenerateToken(user);
            return Ok(new AuthResponse { Token = jwt, Username = user.Username, IsAdmin = user.IsAdmin, Role = user.Role });
        }

        // 再查找初始注册验证令牌
        user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerifyToken == req.Token);
        if (user == null)
            return BadRequest(new { error = "验证链接无效或已过期" });

        user.EmailVerified = true;
        user.EmailVerifyToken = null;
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            Role = user.Role
        });
    }

    /// <summary>重新发送验证邮件</summary>
    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerification([FromBody] ResendVerificationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { error = "请输入电子邮件地址" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null)
            // 不暴露用户是否存在
            return Ok(new { message = "验证邮件已重新发送" });

        if (user.EmailVerified)
            return Ok(new { message = "邮箱已验证，可直接登录" });

        user.EmailVerifyToken = GenerateCryptoToken();
        await _db.SaveChangesAsync();

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            _email.SendVerificationBackground(settings, user.Email, user.EmailVerifyToken);

        return Ok(new { message = "验证邮件已重新发送，请查收" });
    }

    /// <summary>忘记密码 — 发送重置邮件</summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { error = "请输入电子邮件地址" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null)
            // 不暴露用户是否存在
            return Ok(new { message = "重置链接已发送" });

        user.ResetPasswordToken = GenerateCryptoToken();
        user.ResetPasswordExpires = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            _email.SendPasswordResetBackground(settings, user.Email, user.ResetPasswordToken);

        return Ok(new { message = "重置密码链接已发送到注册邮箱" });
    }

    /// <summary>重置密码</summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = "重置令牌无效" });
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return BadRequest(new { error = "新密码至少 6 个字符" });

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.ResetPasswordToken == req.Token &&
            u.ResetPasswordExpires > DateTime.UtcNow);

        if (user == null)
            return BadRequest(new { error = "重置链接无效或已过期（1小时内有效）" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.ResetPasswordToken = null;
        user.ResetPasswordExpires = null;
        await _db.SaveChangesAsync();

        // 后台发送密码重置通知邮件
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            _email.SendBackground(settings, user.Email, "你的 Aquarius 密码已重置", "<p>你的密码刚刚通过找回密码功能被重置。如果不是你本人操作，请立即联系管理员。</p>");

        return Ok(new { message = "密码已重置，请登录" });
    }

    /// <summary>获取当前登录用户信息</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new AuthResponse
        {
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            Role = user.Role
        });
    }

    // ── helpers ────────────────────────────────────────────

    private string GenerateToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? "AquariusSecretKey_ChangeInProduction_2026!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "Aquarius",
            audience: "AquariusApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateCryptoToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}

// ── DTOs ──────────────────────────────────────────────────

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginRequest
{
    /// <summary>用户名或电子邮件</summary>
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string? Token { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string Role { get; set; } = "user";
}
