using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 用户通知。系统通过队列轮询推送，前端定期拉取。
/// Type: system / like / comment / bottle_processed
/// </summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>接收通知的用户 ID。</summary>
    public int UserId { get; set; }

    /// <summary>通知类型。</summary>
    [MaxLength(30)]
    public string Type { get; set; } = "system";

    /// <summary>标题。</summary>
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>内容摘要。</summary>
    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>关联的瓶子 ID（可选）。</summary>
    public int? RelatedBottleId { get; set; }

    /// <summary>是否已读。</summary>
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public User? User { get; set; }
}
