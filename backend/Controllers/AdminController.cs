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

        // 记日志
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);
        _db.BottleLogs.Add(new Models.BottleLog
        {
            BottleId = bottle.Id,
            OperatorUserId = userId,
            OperatorUsername = user?.Username ?? "未知",
            Action = "republish_daily",
            Detail = $"重新推送至 {newDate:yyyy-MM-dd}",
            CreatedAt = DateTime.UtcNow
        });

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

    /// <summary>管理员删除评论/回复（记日志）</summary>
    [HttpDelete("comments/{id}")]
    public async Task<ActionResult> DeleteComment(int id)
    {
        if (!IsAdmin()) return Forbid();

        var comment = await _db.Comments.Include(c => c.Replies).FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null) return NotFound();

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);

        var detail = comment.Content.Length > 50 ? comment.Content[..50] + "..." : comment.Content;

        _db.Comments.RemoveRange(comment.Replies);
        _db.Comments.Remove(comment);

        _db.BottleLogs.Add(new Models.BottleLog
        {
            BottleId = comment.BottleId,
            OperatorUserId = userId,
            OperatorUsername = user?.Username ?? "未知",
            Action = "delete_reply",
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>关闭瓶子（不再捞取/评论，但仍可查看）</summary>
    [HttpPost("bottles/{id}/close")]
    public async Task<ActionResult> CloseBottle(int id)
    {
        if (!IsAdmin()) return Forbid();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();
        if (bottle.IsClosed) return BadRequest(new { error = "瓶子已关闭" });

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);

        bottle.IsClosed = true;
        bottle.ClosedAt = DateTime.UtcNow;
        bottle.ClosedByUserId = userId;

        _db.BottleLogs.Add(new Models.BottleLog
        {
            BottleId = id,
            OperatorUserId = userId,
            OperatorUsername = user?.Username ?? "未知",
            Action = "close",
            Detail = null,
            CreatedAt = DateTime.UtcNow
        });

        // 意见瓶关闭 → 通知瓶主
        if (bottle.Type == "suggestion" && bottle.UserId != null)
        {
            _db.Notifications.Add(new Models.Notification
            {
                UserId = bottle.UserId.Value,
                Type = "bottle_processed",
                Title = "你的意见已被处理",
                Content = bottle.Content.Length > 50 ? bottle.Content[..50] + "..." : bottle.Content,
                RelatedBottleId = id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "瓶子已关闭" });
    }

    /// <summary>打开瓶子（恢复捞取/评论）</summary>
    [HttpPost("bottles/{id}/open")]
    public async Task<ActionResult> OpenBottle(int id)
    {
        if (!IsAdmin()) return Forbid();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();
        if (!bottle.IsClosed) return BadRequest(new { error = "瓶子未关闭" });

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);

        bottle.IsClosed = false;
        bottle.ClosedAt = null;
        bottle.ClosedByUserId = null;

        _db.BottleLogs.Add(new Models.BottleLog
        {
            BottleId = id,
            OperatorUserId = userId,
            OperatorUsername = user?.Username ?? "未知",
            Action = "open",
            Detail = null,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "瓶子已打开" });
    }

    /// <summary>发送系统通知（创建通知瓶 + 通知记录）</summary>
    [HttpPost("notifications/send")]
    public async Task<ActionResult> SendNotification([FromBody] SendNotificationRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var adminUser = await _db.Users.FindAsync(userId);
        var adminName = adminUser?.Username ?? "管理员";

        // 创建通知瓶（一切皆瓶）
        var bottle = new Models.Bottle
        {
            Content = $"{req.Title}\n\n{req.Content}",
            AuthorName = adminName,
            UserToken = "system",
            UserId = userId,
            Type = "notification",
            IsAdminBadge = true,
            CommentsPrivate = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };
        _db.Bottles.Add(bottle);
        await _db.SaveChangesAsync();

        // 确定目标用户列表
        List<Models.User> targets;
        if (!string.IsNullOrWhiteSpace(req.TargetUsers))
        {
            // 支持半角全角逗号分隔，匹配 UID 或用户名
            var parts = req.TargetUsers
                .Replace("，", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            targets = new List<Models.User>();
            foreach (var part in parts)
            {
                Models.User? u = null;
                if (int.TryParse(part, out var uid))
                    u = await _db.Users.FindAsync(uid);
                else
                    u = await _db.Users.FirstOrDefaultAsync(x => x.Username == part);

                if (u != null && !targets.Contains(u))
                    targets.Add(u);
            }
        }
        else
        {
            targets = await _db.Users.Where(u => u.NotifyPreference != "none").ToListAsync();
        }

        // 为每个目标用户创建通知记录
        foreach (var t in targets)
        {
            if (t.NotifyPreference == "none") continue;
            _db.Notifications.Add(new Models.Notification
            {
                UserId = t.Id,
                Type = "system",
                Title = req.Title,
                Content = req.Content,
                RelatedBottleId = bottle.Id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { bottleId = bottle.Id, targetCount = targets.Count });
    }

    // ── Site settings ────────────────────────────────────

    [HttpGet("settings")]
    public async Task<ActionResult> GetSettings()
    {
        if (!IsAdmin()) return Forbid();
        var s = await _db.SiteSettings.FirstOrDefaultAsync();
        if (s == null) return NotFound();
        return Ok(new
        {
            s.SiteName, s.Copyright,
            s.SmtpHost, s.SmtpPort, s.SmtpUser,
            s.SmtpFrom, s.SmtpEnableSsl, s.SiteBaseUrl,
            SmtpPassword = string.IsNullOrEmpty(s.SmtpPassword) ? "" : "••••••"
        });
    }

    // ── Suggestions ──────────────────────────────────────

    [HttpGet("suggestions")]
    public async Task<ActionResult> ListSuggestions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!IsAdmin()) return Forbid();

        var baseQuery = _db.Bottles.Where(b => b.Type == "suggestion");
        var pendingTotal = await baseQuery.CountAsync(b => !b.IsClosed);
        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new
            {
                b.Id, b.Content, b.AuthorName, b.CreatedAt,
                b.ReportedBottleId,
                b.IsClosed,
                CommentCount = b.Comments.Count
            })
            .ToListAsync();

        return Ok(new { items, total, pendingTotal, page, pageSize });
    }

    [HttpPut("settings")]
    public async Task<ActionResult> UpdateSettings([FromBody] UpdateSettingsRequest req)
    {
        if (!IsAdmin()) return Forbid();
        var s = await _db.SiteSettings.FirstOrDefaultAsync();
        if (s == null) return NotFound();
        s.SiteName = req.SiteName ?? s.SiteName;
        s.Copyright = req.Copyright ?? s.Copyright;
        if (req.SmtpHost != null) s.SmtpHost = req.SmtpHost;
        if (req.SmtpPort.HasValue) s.SmtpPort = req.SmtpPort.Value;
        if (req.SmtpUser != null) s.SmtpUser = req.SmtpUser;
        if (req.SmtpPassword != null) s.SmtpPassword = req.SmtpPassword;
        if (req.SmtpFrom != null) s.SmtpFrom = req.SmtpFrom;
        if (req.SmtpEnableSsl.HasValue) s.SmtpEnableSsl = req.SmtpEnableSsl.Value;
        if (req.SiteBaseUrl != null) s.SiteBaseUrl = req.SiteBaseUrl;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            s.SiteName, s.Copyright,
            s.SmtpHost, s.SmtpPort, s.SmtpUser,
            s.SmtpFrom, s.SmtpEnableSsl, s.SiteBaseUrl,
            SmtpPassword = string.IsNullOrEmpty(s.SmtpPassword) ? "" : "••••••"
        });
    }
}

public class UpdateSettingsRequest
{
    public string? SiteName { get; set; }
    public string? Copyright { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFrom { get; set; }
    public bool? SmtpEnableSsl { get; set; }
    public string? SiteBaseUrl { get; set; }
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