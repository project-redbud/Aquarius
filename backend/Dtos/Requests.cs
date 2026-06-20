using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Dtos;

public class ThrowBottleRequest
{
    [Required, MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Base64 编码的图片数据（可选）。</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>发布者留下的名字；null = 匿名发布。</summary>
    [MaxLength(50)]
    public string? AuthorName { get; set; }

    /// <summary>登录可见。</summary>
    public bool RequireLogin { get; set; }

    /// <summary>评论仅作者可见。</summary>
    public bool CommentsPrivate { get; set; }

    /// <summary>管理员标识。</summary>
    public bool IsAdminBadge { get; set; }
}

public class AddCommentRequest
{
    [Required, MaxLength(300)]
    public string Content { get; set; } = string.Empty;

    /// <summary>属于哪个顶级评论（cid）。不传=顶级评论。</summary>
    public int? CommentId { get; set; }

    /// <summary>回复的具体 rid，不传=直接回复评论。</summary>
    public int? ParentReplyId { get; set; }

    /// <summary>管理员标识。</summary>
    public bool IsAdminBadge { get; set; }
}

public class EditBottleRequest
{
    [Required, MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Base64 编码的图片数据（可选）。</summary>
    public string? ImageBase64 { get; set; }

    [MaxLength(50)]
    public string? AuthorName { get; set; }
}

public class EditCommentRequest
{
    [Required, MaxLength(300)]
    public string Content { get; set; } = string.Empty;
}

public class BottleDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? AuthorName { get; set; }
    public string Type { get; set; } = "normal";
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool LikedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public int? UserId { get; set; }
    public bool RequireLogin { get; set; }
    public bool CommentsPrivate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int ReThrowCount { get; set; }
    public DateTime? LastReThrowAt { get; set; }
    public bool IsAdminBadge { get; set; }
}

public class CommentDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public int? UserId { get; set; }

    /// <summary>仅管理员可见；普通用户返回 null。</summary>
    public string? UserToken { get; set; }

    /// <summary>楼中楼：属于哪个顶级评论。</summary>
    public int? CommentId { get; set; }

    /// <summary>楼中楼：回复的具体 rid，null=直接回评论。</summary>
    public int? ParentReplyId { get; set; }

    /// <summary>子回复总数。</summary>
    public int ReplyCount { get; set; }

    /// <summary>前 3 条子回复。</summary>
    public List<CommentDto> Replies { get; set; } = [];

    /// <summary>被回复的内容（ParentReplyId≠null 时有值，截断 30 字）。</summary>
    public string? ParentContent { get; set; }

    /// <summary>管理员标识。</summary>
    public bool IsAdminBadge { get; set; }
}

public class DailyPushDto
{
    public BottleDto? Story { get; set; }
    public BottleDto? Qa { get; set; }
}
