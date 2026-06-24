using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;
using Aquarius.Api.Models;
using Aquarius.Api.Services;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly EmailService _email;

    public UserSettingsController(AquariusDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task<User?> GetUser() =>
        await _db.Users.FindAsync(GetUserId());

    private static string GenerateToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

    /// <summary>修改密码</summary>
    [HttpPut("password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
            return BadRequest(new { error = "原密码错误" });
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return BadRequest(new { error = "新密码至少 6 个字符" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "密码已修改" });
    }

    /// <summary>请求修改邮箱（发验证邮件到新邮箱，旧邮箱保留）</summary>
    [HttpPost("change-email")]
    public async Task<ActionResult> ChangeEmail([FromBody] ChangeEmailRequest req)
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.NewEmail) || !req.NewEmail.Contains('@'))
            return BadRequest(new { error = "请输入有效的电子邮件地址" });

        if (req.NewEmail == user.Email)
            return BadRequest(new { error = "新邮箱与当前邮箱相同" });

        if (await _db.Users.AnyAsync(u => u.Email == req.NewEmail || u.NewEmail == req.NewEmail))
            return BadRequest(new { error = "该邮箱已被使用" });

        user.NewEmail = req.NewEmail.Trim();
        user.NewEmailVerifyToken = GenerateToken();
        await _db.SaveChangesAsync();

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            await _email.SendVerificationAsync(settings, user.NewEmail, user.NewEmailVerifyToken);

        return Ok(new { message = "验证邮件已发送到新邮箱，请查收" });
    }

    /// <summary>确认新邮箱</summary>
    [HttpPost("verify-new-email")]
    public async Task<ActionResult> VerifyNewEmail([FromBody] VerifyNewEmailRequest req)
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(user.NewEmailVerifyToken) || user.NewEmailVerifyToken != req.Token)
            return BadRequest(new { error = "验证链接无效或已过期" });

        var oldEmail = user.Email;
        user.Email = user.NewEmail!;
        user.EmailVerified = true;
        user.NewEmail = null;
        user.NewEmailVerifyToken = null;
        await _db.SaveChangesAsync();

        // 通知旧邮箱
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            await _email.SendAsync(settings, oldEmail,
                "你的 Aquarius 邮箱已变更",
                $"<p>你的 Aquarius 账号邮箱已从 {oldEmail} 变更为 {user.Email}。</p><p>如果不是你本人操作，请立即联系管理员。</p>");
        }

        return Ok(new { message = "邮箱已更新" });
    }

    /// <summary>重发邮箱验证邮件</summary>
    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerification()
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        if (user.EmailVerified)
            return Ok(new { message = "邮箱已验证" });

        // 如果有待验证的新邮箱，重发新邮箱验证
        var targetEmail = user.NewEmail ?? user.Email;
        var token = user.NewEmailVerifyToken ?? user.EmailVerifyToken;
        if (string.IsNullOrEmpty(token))
        {
            user.EmailVerifyToken = GenerateToken();
            token = user.EmailVerifyToken;
            await _db.SaveChangesAsync();
        }

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
            await _email.SendVerificationAsync(settings, targetEmail, token);

        return Ok(new { message = "验证邮件已重新发送" });
    }

    /// <summary>获取当前用户设置</summary>
    [HttpGet("preferences")]
    public async Task<ActionResult> GetPreferences()
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        return Ok(new
        {
            user.NotifyPreference,
            user.ViewPrivateComments,
            user.Email,
            emailVerified = user.EmailVerified,
            newEmail = user.NewEmail,
            isAdmin = user.IsAdmin
        });
    }

    /// <summary>更新通知偏好和私评开关</summary>
    [HttpPut("preferences")]
    public async Task<ActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest req)
    {
        var user = await GetUser();
        if (user == null) return NotFound();

        if (req.NotifyPreference != null)
        {
            var valid = new[] { "default", "notify_only", "none" };
            if (!valid.Contains(req.NotifyPreference))
                return BadRequest(new { error = "无效的通知偏好" });
            user.NotifyPreference = req.NotifyPreference;
        }

        if (req.ViewPrivateComments.HasValue && user.IsAdmin)
            user.ViewPrivateComments = req.ViewPrivateComments.Value;

        await _db.SaveChangesAsync();
        return Ok(new { user.NotifyPreference, user.ViewPrivateComments });
    }
}
