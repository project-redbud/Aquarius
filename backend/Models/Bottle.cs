using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 漂流瓶。Type: "normal" = 用户投的, "story" = 每日故事, "qa" = 每日问答。
/// </summary>
public class Bottle
{
    public int Id { get; set; }

    [MaxLength(2000)]
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

    /// <summary>登录可见：未登录用户无法捞到此瓶。</summary>
    public bool RequireLogin { get; set; }

    /// <summary>评论仅作者可见：非作者/管理员看不到评论。</summary>
    public bool CommentsPrivate { get; set; }

    /// <summary>管理员标识：显示管理员徽章。</summary>
    public bool IsAdminBadge { get; set; }

    public int LikeCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>编辑时间，null 表示未编辑过。</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>到期时间（创建时 = CreatedAt + 7天，重投时更新）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>重投次数。</summary>
    public int ReThrowCount { get; set; }

    /// <summary>最后一次重投时间，null 表示从未重投。</summary>
    public DateTime? LastReThrowAt { get; set; }

    /// <summary>举报目标瓶子 ID（意见瓶专用）。</summary>
    public int? ReportedBottleId { get; set; }

    /// <summary>瓶子是否已关闭。关闭后不可捞取、不可评论，但可查看。</summary>
    public bool IsClosed { get; set; }

    /// <summary>关闭时间。</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>关闭操作人 ID。</summary>
    public int? ClosedByUserId { get; set; }

    /// <summary>精华瓶：管理员标记，显示【精华】标识并提供 5 个虚空点赞。</summary>
    public bool IsEssence { get; set; }

    // ── FK to User ─────────────────────────────────────────
    public int? UserId { get; set; }

    // ── Navigation ────────────────────────────────────────
    public User? User { get; set; }
    public List<Comment> Comments { get; set; } = [];
}
