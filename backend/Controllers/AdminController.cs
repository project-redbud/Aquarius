using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public AdminController(AquariusDbContext db) => _db = db;

    /// <summary>查看某个瓶的评论（含 UserToken）</summary>
    [HttpGet("bottles/{bottleId}/comments")]
    public async Task<ActionResult<List<CommentDto>>> GetComments(int bottleId,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        if (adminKey != "aquarius-admin-2025") return Unauthorized();

        var comments = await _db.Comments
            .Where(c => c.BottleId == bottleId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                UserToken = c.UserToken,  // ← admin sees this
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(comments);
    }

    /// <summary>创建每日推送瓶</summary>
    [HttpPost("daily")]
    public async Task<ActionResult> CreateDaily([FromBody] CreateDailyRequest req,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        if (adminKey != "aquarius-admin-2025") return Unauthorized();

        var push = new Models.DailyPush
        {
            Type = req.Type,
            Content = req.Content,
            ImagePath = req.ImagePath,
            Date = req.Date.Date,
            CreatedAt = DateTime.UtcNow
        };

        _db.DailyPushes.Add(push);
        await _db.SaveChangesAsync();

        return Created($"/api/admin/daily/{push.Id}", push);
    }

    /// <summary>列出所有瓶子（管理）</summary>
    [HttpGet("bottles")]
    public async Task<ActionResult> ListBottles(
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        if (adminKey != "aquarius-admin-2025") return Unauthorized();

        var bottles = await _db.Bottles
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Content, b.ImagePath, b.AuthorName,
                b.UserToken, b.Type, b.LikeCount,
                CommentCount = b.Comments.Count,
                b.CreatedAt
            })
            .ToListAsync();

        return Ok(bottles);
    }

    /// <summary>删除瓶子（管理）</summary>
    [HttpDelete("bottles/{id}")]
    public async Task<ActionResult> DeleteBottle(int id,
        [FromHeader(Name = "X-Admin-Key")] string? adminKey)
    {
        if (adminKey != "aquarius-admin-2025") return Unauthorized();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();

        _db.Bottles.Remove(bottle);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CreateDailyRequest
{
    public string Type { get; set; } = "story"; // "story" or "qa"
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
