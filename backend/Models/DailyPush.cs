using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 每日推送瓶。每天由服务器推送一则故事瓶 + 一则问答瓶。
/// </summary>
public class DailyPush
{
    public int Id { get; set; }

    /// <summary>"story" or "qa"</summary>
    [MaxLength(20)]
    public string Type { get; set; } = "story";

    [MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImagePath { get; set; }

    /// <summary>推送日期，同一天只返回一组。</summary>
    public DateTime Date { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联的瓶子 ID，可为 null。创建时自动生成 Bottle 实体。</summary>
    public int? BottleId { get; set; }
    public Bottle? Bottle { get; set; }
}
