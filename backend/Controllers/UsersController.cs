using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly Aquarius.Api.Services.EmailService _email;

    public UsersController(AquariusDbContext db, Aquarius.Api.Services.EmailService email)
    {
        _db = db;
        _email = email;
    }

    private bool IsAdmin() => User.FindFirst("isAdmin")?.Value == "true";

    /// <summary>列出用户（分页+搜索）</summary>
    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? q = null)
    {
        if (!IsAdmin()) return Forbid();

        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.Username.Contains(q) || u.Email.Contains(q));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.Role,
                u.IsAdmin, u.IsBanned, u.BanReason,
                u.BannedUntil, u.CreatedAt,
                BottleCount = u.Bottles.Count,
                CommentCount = u.Comments.Count
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>用户详情（瓶子+评论）</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult> Detail(int id)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var bottles = await _db.Bottles
            .Where(b => b.UserId == id)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.Id, b.Content, b.CreatedAt, b.LikeCount, CommentCount = b.Comments.Count })
            .ToListAsync();

        var comments = await _db.Comments
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Content, c.CreatedAt, c.BottleId })
            .ToListAsync();

        return Ok(new
        {
            user.Id, user.Username, user.Email, user.Role,
            user.IsAdmin, user.IsBanned, user.BanReason, user.BannedUntil, user.CreatedAt,
            bottles, comments
        });
    }

    /// <summary>删除用户</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "admin") return BadRequest(new { error = "不能删除管理员" });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>修改用户邮箱（管理员）</summary>
    [HttpPut("{id}/email")]
    public async Task<ActionResult> SetEmail(int id, [FromBody] SetEmailRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { error = "无效的邮箱地址" });

        if (await _db.Users.AnyAsync(u => u.Id != id && u.Email == req.Email))
            return BadRequest(new { error = "邮箱已被使用" });

        user.Email = req.Email.Trim();
        user.EmailVerified = req.Verified;
        user.NewEmail = null;
        user.NewEmailVerifyToken = null;
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Username, user.Email, user.EmailVerified });
    }

    /// <summary>封禁用户</summary>
    [HttpPost("{id}/ban")]
    public async Task<ActionResult> Ban(int id, [FromBody] BanRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "admin") return BadRequest(new { error = "不能封禁管理员" });

        user.IsBanned = true;
        user.BanReason = req.Reason;
        user.BannedUntil = req.Days > 0 ? DateTime.UtcNow.AddDays(req.Days) : null;
        await _db.SaveChangesAsync();

        // 发送封禁通知邮件
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings != null)
        {
            var reasonStr = string.IsNullOrWhiteSpace(req.Reason) ? "" : $"理由：{req.Reason}。";
            var untilStr = req.Days > 0 ? $"封禁至 {DateTime.UtcNow.AddDays(req.Days):yyyy-MM-dd}。" : "永久封禁。";
            _email.SendBackground(settings, user.Email, "你的 Aquarius 账号已被封禁",
                $"<p>你的账号因违反社区规则已被封禁。</p><p>{reasonStr}{untilStr}</p><p>如有疑问请联系管理员。</p>");
        }

        return Ok(new { user.Id, user.Username, user.IsBanned, user.BanReason, user.BannedUntil });
    }

    /// <summary>解封</summary>
    [HttpPost("{id}/unban")]
    public async Task<ActionResult> Unban(int id)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsBanned = false;
        user.BanReason = null;
        user.BannedUntil = null;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.IsBanned });
    }

    /// <summary>修改角色</summary>
    [HttpPost("{id}/role")]
    public async Task<ActionResult> SetRole(int id, [FromBody] RoleRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var validRoles = new[] { "user", "admin" };
        if (!validRoles.Contains(req.Role))
            return BadRequest(new { error = "无效角色" });

        user.Role = req.Role;
        user.IsAdmin = req.Role == "admin";
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Username, user.Role, user.IsAdmin });
    }

    /// <summary>搜索（用户+瓶子）</summary>
    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (!IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(q)) return Ok(new { users = Array.Empty<object>(), bottles = Array.Empty<object>() });

        var users = await _db.Users
            .Where(u => u.Username.Contains(q) || u.Email.Contains(q))
            .Take(10)
            .Select(u => new { u.Id, u.Username, u.Email, u.Role })
            .ToListAsync();

        var bottles = await _db.Bottles
            .Where(b => b.Content.Contains(q))
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new { b.Id, b.Content, b.CreatedAt, b.AuthorName })
            .ToListAsync();

        return Ok(new { users, bottles });
    }
}

public class BanRequest
{
    public string Reason { get; set; } = "";
    public int Days { get; set; }
}

public class SetEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public bool Verified { get; set; }
}

public class RoleRequest
{
    public string Role { get; set; } = "user";
}
