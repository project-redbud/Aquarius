using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 评论 / 回复。CommentId=null 表示顶级评论，否则表示属于哪个顶级评论。
/// ParentReplyId=null 表示直接回复评论，非null 表示回复某条具体回复（楼中楼）。
/// </summary>
public class Comment
{
    public int Id { get; set; }

    public int BottleId { get; set; }

    [MaxLength(300)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(64)]
    public string UserToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>属于哪个顶级评论（cid）。null = 自己是顶级评论。</summary>
    public int? CommentId { get; set; }

    /// <summary>回复的是哪条具体回复（rid）。null = 直接回复评论。</summary>
    public int? ParentReplyId { get; set; }

    // ── Navigation ────────────────────────────────────────
    public Bottle? Bottle { get; set; }
    public Comment? ParentComment { get; set; }
    public Comment? ParentReply { get; set; }
    public List<Comment> Replies { get; set; } = [];
}
