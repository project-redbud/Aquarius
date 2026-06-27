using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public HomeController(AquariusDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> Index()
    {
        // ── 最新推送日期（有推送的最后一天）──
        var latestPushDate = await _db.DailyPushes
            .OrderByDescending(d => d.Date)
            .Select(d => d.Date)
            .FirstOrDefaultAsync();
        var targetDate = latestPushDate != default ? latestPushDate.Date : DateTime.Today;

        // ── 当日推送 ──────────────────────────────────
        var pushes = await _db.DailyPushes
            .Where(d => d.Date == targetDate)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        BottleDto? ToDto(Models.DailyPush? p)
        {
            if (p == null) return null;
            return new BottleDto
            {
                Id = p.BottleId ?? 0,
                Content = p.Content,
                ImagePath = p.ImagePath,
                AuthorName = p.Type switch { "story" => "📖 每日故事", "qa" => "❓ 每日问答", "news" => "📰 每日新闻", _ => "📖 每日故事" },
                Type = p.Type,
                CreatedAt = p.Date.Date + p.CreatedAt.TimeOfDay,
                ExpiresAt = p.Date.Date.AddDays(7)
            };
        }

        var news = ToDto(pushes.FirstOrDefault(p => p.Type == "news"));
        var story = ToDto(pushes.FirstOrDefault(p => p.Type == "story"));
        var qa = ToDto(pushes.FirstOrDefault(p => p.Type == "qa"));

        // ── 最新 TOP10 ─────────────────────────────────
        var latestBottles = await _db.Bottles
            .Where(b => b.Type == "normal" && b.ExpiresAt > DateTime.UtcNow && !b.IsClosed)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        var latest = latestBottles.Select(b => new
        {
            b.Id, b.Content, b.AuthorName, b.LikeCount,
            CommentCount = b.Comments.Count, b.CreatedAt
        }).ToList();

        // ── 最热 TOP10 ─────────────────────────────────
        var candidates = await _db.Bottles
            .Where(b => b.Type == "normal" && b.ExpiresAt > DateTime.UtcNow && !b.IsClosed)
            .ToListAsync();

        var hot = candidates
            .Select(b => new
            {
                b.Id, b.Content, b.AuthorName, b.LikeCount,
                CommentCount = b.Comments.Count, b.CreatedAt,
                Heat = Math.Max(0, b.LikeCount * 2 + b.Comments.Count
                    - (int)(DateTime.UtcNow - b.CreatedAt).TotalDays)
            })
            .OrderByDescending(b => b.Heat)
            .Take(10)
            .ToList();

        return Ok(new { pushes = new { news, story, qa }, latest, hot });
    }
}
