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
}

public class AddCommentRequest
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
}

public class CommentDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>仅管理员可见；普通用户返回 null。</summary>
    public string? UserToken { get; set; }
}

public class DailyPushDto
{
    public BottleDto? Story { get; set; }
    public BottleDto? Qa { get; set; }
}
