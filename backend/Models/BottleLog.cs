using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 瓶子管理操作日志。仅记录管理操作（关闭/打开/删回复/重新推送等），
/// 不记录分享、点赞、举报、重投、编辑等用户操作。
/// </summary>
public class BottleLog
{
    public int Id { get; set; }

    public int BottleId { get; set; }

    /// <summary>操作人用户 ID（可空，系统操作为 null）。</summary>
    public int? OperatorUserId { get; set; }

    /// <summary>操作人用户名（不匿名）。</summary>
    [MaxLength(50)]
    public string OperatorUsername { get; set; } = string.Empty;

    /// <summary>操作类型：close / open / delete_reply / republish_daily 等。</summary>
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>操作详情（可选，如被删回复内容摘要）。</summary>
    [MaxLength(300)]
    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public Bottle? Bottle { get; set; }
    public User? OperatorUser { get; set; }
}
