using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

public class User
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    /// <summary>角色：user / moderator / admin</summary>
    [MaxLength(20)]
    public string Role { get; set; } = "user";

    public bool IsBanned { get; set; }

    [MaxLength(500)]
    public string? BanReason { get; set; }

    public DateTime? BannedUntil { get; set; }

    /// <summary>邮箱是否已验证。未验证不可投瓶/评论。</summary>
    public bool EmailVerified { get; set; }

    /// <summary>邮箱验证令牌（注册时生成，激活后清空）。</summary>
    [MaxLength(128)]
    public string? EmailVerifyToken { get; set; }

    /// <summary>密码重置令牌。</summary>
    [MaxLength(128)]
    public string? ResetPasswordToken { get; set; }

    /// <summary>密码重置令牌过期时间。</summary>
    public DateTime? ResetPasswordExpires { get; set; }

    /// <summary>通知偏好：default / notify_only / none</summary>
    [MaxLength(20)]
    public string NotifyPreference { get; set; } = "default";

    /// <summary>管理员是否查看仅作者可见的评论（默认关闭）</summary>
    public bool ViewPrivateComments { get; set; }

    /// <summary>待验证的新邮箱。</summary>
    [MaxLength(200)]
    public string? NewEmail { get; set; }

    /// <summary>新邮箱验证令牌。</summary>
    [MaxLength(128)]
    public string? NewEmailVerifyToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public List<Bottle> Bottles { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
}