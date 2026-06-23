using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Aquarius.Api.Data;
using Aquarius.Api.Models;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AquariusDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>注册</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
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

        var user = new User
        {
            Username = req.Username.Trim(),
            Email = req.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsAdmin = isFirst,
            Role = isFirst ? "admin" : "user",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
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

        var token = GenerateToken(user);
        return Ok(new AuthResponse
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin
        });
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
            IsAdmin = user.IsAdmin
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

public class AuthResponse
{
    public string? Token { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string Role { get; set; } = "user";
}