using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;
using Aquarius.Api.Models;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public NotificationsController(AquariusDbContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>分页获取通知（可按类型筛选）</summary>
    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null)
    {
        var userId = GetUserId();

        var query = _db.Notifications
            .Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
            query = query.Where(n => n.Type == type);

        var total = await query.CountAsync();
        var unreadTotal = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Content = n.Content,
                RelatedBottleId = n.RelatedBottleId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, unreadTotal, page, pageSize });
    }

    /// <summary>获取未读通知数</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult> UnreadCount()
    {
        var userId = GetUserId();
        var count = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { count });
    }

    /// <summary>标记单条已读</summary>
    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkRead(int id)
    {
        var userId = GetUserId();
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n == null) return NotFound();
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>全部已读</summary>
    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        var userId = GetUserId();
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }
}
