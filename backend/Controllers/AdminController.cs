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
            CreatedAt = DateTime.UtcNow
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

    /// <summary>列出所有瓶子（管理）</summary>
    [HttpGet("bottles")]
    public async Task<ActionResult> ListBottles()
    {
        if (!IsAdmin()) return Forbid();

        var bottles = await _db.Bottles
            .OrderByDescending(b => b.CreatedAt)
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

        return Ok(bottles);
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
}

public class CreateDailyRequest
{
    public string Type { get; set; } = "story";
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
}