using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

/// <summary>
/// 点赞记录。一个用户只能对同一个瓶点一次赞。
/// </summary>
public class Like
{
    public int Id { get; set; }

    public int BottleId { get; set; }

    [MaxLength(64)]
    public string UserToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public Bottle? Bottle { get; set; }
}
