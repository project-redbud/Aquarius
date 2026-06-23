using System.ComponentModel.DataAnnotations;

namespace Aquarius.Api.Models;

public class User
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    /// <summary>角色：user / moderator / admin</summary>
    [MaxLength(20)]
    public string Role { get; set; } = "user";

    public bool IsBanned { get; set; }

    [MaxLength(500)]
    public string? BanReason { get; set; }

    public DateTime? BannedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────
    public List<Bottle> Bottles { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
}