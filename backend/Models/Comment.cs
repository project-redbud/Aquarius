using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 匿名评论。UserToken 存但不返给普通用户。
/// </summary>
public class Comment
{
    public int Id { get; set; }

    public int BottleId { get; set; }

    [MaxLength(300)]
    public string Content { get; set; } = string.Empty;

    /// <summary>评论者的匿名令牌，管理员可见。</summary>
    [MaxLength(64)]
    public string UserToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public Bottle? Bottle { get; set; }
}
