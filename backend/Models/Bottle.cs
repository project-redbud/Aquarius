using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 漂流瓶。Type: "normal" = 用户投的, "story" = 每日故事, "qa" = 每日问答。
/// </summary>
public class Bottle
{
    public int Id { get; set; }

    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>图片相对路径，如 "uploads/abc123.jpg"；null 表示纯文字。</summary>
    [MaxLength(500)]
    public string? ImagePath { get; set; }

    /// <summary>发布者留下的名字；null 表示匿名发布。</summary>
    [MaxLength(50)]
    public string? AuthorName { get; set; }

    /// <summary>发布者的匿名令牌 (GUID)，用于管理员追溯。</summary>
    [MaxLength(64)]
    public string UserToken { get; set; } = string.Empty;

    /// <summary>normal / story / qa</summary>
    [MaxLength(20)]
    public string Type { get; set; } = "normal";

    public int LikeCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>编辑时间，null 表示未编辑过。</summary>
    public DateTime? EditedAt { get; set; }

    // ── FK to User ─────────────────────────────────────────
    public int? UserId { get; set; }

    // ── Navigation ────────────────────────────────────────
    public User? User { get; set; }
    public List<Comment> Comments { get; set; } = [];
}
