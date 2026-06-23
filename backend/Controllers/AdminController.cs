using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public AdminController(AquariusDbContext db) => _db = db;

    private bool IsAdmin()
    {
        var claim = User.FindFirst("isAdmin")?.Value;
        return claim == "true";
    }

    private IActionResult RequireAdmin()
    {
        if (!IsAdmin()) return Forbid();
        return null!; // will be returned by caller
    }

    /// <summary>查看某个瓶的评论（含 UserToken + 楼中楼）</summary>
    [HttpGet("bottles/{bottleId}/comments")]
    public async Task<ActionResult<List<CommentDto>>> GetComments(int bottleId)
    {
        if (!IsAdmin()) return Forbid();

        var comments = await _db.Comments
            .Include(c => c.Replies).ThenInclude(r => r.Replies)
            .Where(c => c.BottleId == bottleId && c.CommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = comments.Select(c => new CommentDto
        {
            Id = c.Id,
            Content = c.Content,
            UserToken = c.UserToken,
            CommentId = c.CommentId,
            ParentReplyId = c.ParentReplyId,
            CreatedAt = c.CreatedAt,
            EditedAt = c.EditedAt,
            UserId = c.UserId,
            ReplyCount = c.Replies.Count,
            Replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => new CommentDto
            {
                Id = r.Id,
                Content = r.Content,
                UserToken = r.UserToken,
                CommentId = r.CommentId,
                ParentReplyId = r.ParentReplyId,
                CreatedAt = r.CreatedAt,
                EditedAt = r.EditedAt,
                UserId = r.UserId
            }).ToList()
        }).ToList();

        return Ok(result);
    }

    /// <summary>检查某日某类型推送是否已存在</summary>
    [HttpGet("daily/check")]
    public async Task<ActionResult> CheckDaily(
        [FromQuery] string type,
        [FromQuery] string date)
    {
        if (!IsAdmin()) return Forbid();

        var day = DateTime.Parse(date);

        var existing = await _db.DailyPushes
            .Where(d => d.Date == day && d.Type == type)
            .Select(d => new { d.Id, d.Content, d.Date, d.BottleId })
            .FirstOrDefaultAsync();

        if (existing == null) return NotFound();
        return Ok(existing);
    }

    /// <summary>创建/更新每日推送瓶（同类型+日期已存在则更新）</summary>
    [HttpPost("daily")]
    public async Task<ActionResult> CreateDaily([FromBody] CreateDailyRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var date = DateTime.Parse(req.Date);
        var existing = await _db.DailyPushes
            .Include(d => d.Bottle)
            .FirstOrDefaultAsync(d => d.Date == date && d.Type == req.Type);

        if (existing != null)
        {
            existing.Content = req.Content;
            existing.ImagePath = req.ImagePath;
            if (existing.Bottle != null)
            {
                existing.Bottle.Content = req.Content;
                existing.Bottle.ImagePath = req.ImagePath;
            }
            await _db.SaveChangesAsync();
            return Ok(new { existing.Id, existing.Type, existing.Content, existing.ImagePath, existing.Date, existing.BottleId, updated = true });
        }

        var bottle = new Models.Bottle
        {
            Content = req.Content,
            ImagePath = req.ImagePath,
            AuthorName = req.Type == "story" ? "📖 每日故事" : "❓ 每日问答",
            UserToken = "system",
            Type = req.Type,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _db.Bottles.Add(bottle);
        await _db.SaveChangesAsync();

        var push = new Models.DailyPush
        {
            Type = req.Type,
            Content = req.Content,
            ImagePath = req.ImagePath,
            Date = date,
            BottleId = bottle.Id,
            CreatedAt = DateTime.UtcNow
        };

        _db.DailyPushes.Add(push);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/daily/{push.Id}", new { push.Id, push.Type, push.Content, push.ImagePath, push.Date, push.BottleId });
    }

    // ── Daily push management ──────────────────────────

    /// <summary>列出所有每日推送（分页，每页 10 条）</summary>
    [HttpGet("daily")]
    public async Task<ActionResult> ListDaily(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!IsAdmin()) return Forbid();

        var query = _db.DailyPushes
            .OrderByDescending(d => d.Date)
            .ThenBy(d => d.Type);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id, d.Type, d.Content, d.ImagePath,
                d.Date, d.BottleId, d.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>编辑每日推送内容</summary>
    [HttpPut("daily/{id}")]
    public async Task<ActionResult> EditDaily(int id, [FromBody] EditDailyRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var push = await _db.DailyPushes.Include(d => d.Bottle).FirstOrDefaultAsync(d => d.Id == id);
        if (push == null) return NotFound();

        push.Content = req.Content;
        push.ImagePath = req.ImagePath;
        if (push.Bottle != null)
        {
            push.Bottle.Content = req.Content;
            push.Bottle.ImagePath = req.ImagePath;
        }
        await _db.SaveChangesAsync();

        return Ok(new { push.Id, push.Type, push.Content, push.ImagePath, push.Date, push.BottleId });
    }

    /// <summary>删除每日推送（级联删除关联瓶子）</summary>
    [HttpDelete("daily/{id}")]
    public async Task<ActionResult> DeleteDaily(int id)
    {
        if (!IsAdmin()) return Forbid();

        var push = await _db.DailyPushes.Include(d => d.Bottle).FirstOrDefaultAsync(d => d.Id == id);
        if (push == null) return NotFound();

        if (push.Bottle != null) _db.Bottles.Remove(push.Bottle);
        _db.DailyPushes.Remove(push);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>重新推送到新日期（克隆内容 + 瓶子）</summary>
    [HttpPost("daily/{id}/republish")]
    public async Task<ActionResult> RepublishDaily(int id, [FromBody] RepublishDailyRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var source = await _db.DailyPushes.FindAsync(id);
        if (source == null) return NotFound();

        var newDate = DateTime.Parse(req.Date);

        // 检查目标日期+类型是否已有推送
        var existing = await _db.DailyPushes
            .Include(d => d.Bottle)
            .FirstOrDefaultAsync(d => d.Date == newDate && d.Type == source.Type);
        if (existing != null)
        {
            if (!req.Force)
                return Conflict($"该日期已有「{source.Type}」推送");
            // 强制覆盖：删除旧推送
            if (existing.Bottle != null) _db.Bottles.Remove(existing.Bottle);
            _db.DailyPushes.Remove(existing);
            await _db.SaveChangesAsync();
        }

        var bottle = new Models.Bottle
        {
            Content = source.Content,
            ImagePath = source.ImagePath,
            AuthorName = source.Type == "story" ? "📖 每日故事" : "❓ 每日问答",
            UserToken = "system",
            Type = source.Type,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _db.Bottles.Add(bottle);
        await _db.SaveChangesAsync();

        var push = new Models.DailyPush
        {
            Type = source.Type,
            Content = source.Content,
            ImagePath = source.ImagePath,
            Date = newDate,
            BottleId = bottle.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.DailyPushes.Add(push);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/daily/{push.Id}", new { push.Id, push.Type, push.Content, push.ImagePath, push.Date, push.BottleId });
    }

    /// <summary>列出所有瓶子（管理，分页每页 10 条）</summary>
    [HttpGet("bottles")]
    public async Task<ActionResult> ListBottles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!IsAdmin()) return Forbid();

        var query = _db.Bottles
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id, b.Content, b.ImagePath, b.AuthorName,
                b.UserToken, b.Type, b.LikeCount,
                CommentCount = b.Comments.Count,
                b.CreatedAt,
                b.EditedAt,
                b.UserId
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>删除瓶子（管理）</summary>
    [HttpDelete("bottles/{id}")]
    public async Task<ActionResult> DeleteBottle(int id)
    {
        if (!IsAdmin()) return Forbid();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();

        _db.Bottles.Remove(bottle);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Site settings ────────────────────────────────────

    [HttpGet("settings")]
    public async Task<ActionResult> GetSettings()
    {
        if (!IsAdmin()) return Forbid();
        var s = await _db.SiteSettings.FirstOrDefaultAsync();
        if (s == null) return NotFound();
        return Ok(new { s.SiteName, s.Copyright });
    }

    [HttpPut("settings")]
    public async Task<ActionResult> UpdateSettings([FromBody] UpdateSettingsRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var s = await _db.SiteSettings.FirstOrDefaultAsync();
        if (s == null) return NotFound();
        s.SiteName = req.SiteName ?? s.SiteName;
        s.Copyright = req.Copyright ?? s.Copyright;
        await _db.SaveChangesAsync();
        return Ok(new { s.SiteName, s.Copyright });
    }
}

public class UpdateSettingsRequest
{
    public string? SiteName { get; set; }
    public string? Copyright { get; set; }
}

public class CreateDailyRequest
{
    public string Type { get; set; } = "story";
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
}

public class EditDailyRequest
{
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
}

public class RepublishDailyRequest
{
    public string Date { get; set; } = string.Empty;
    public bool Force { get; set; }
}