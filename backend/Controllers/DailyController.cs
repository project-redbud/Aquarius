using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DailyController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DailyController(AquariusDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>获取指定日期的推送（默认今日），仅允许查看最近 7 天。</summary>
    [HttpGet]
    public async Task<ActionResult<DailyPushDto>> Today(
        [FromQuery] string? date,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        DateTime targetDay;

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateTime.TryParse(date, out targetDay))
                return BadRequest("日期格式无效，请使用 yyyy-MM-dd");

            // 只允许查看最近 7 天（今日 - 前 6 天）
            var minDate = DateTime.Today.AddDays(-6);
            if (targetDay < minDate || targetDay > DateTime.Today)
                return BadRequest($"只能查看从 {minDate:yyyy-MM-dd} 到 {DateTime.Today:yyyy-MM-dd} 的推送");
        }
        else
        {
            targetDay = DateTime.Today;
        }

        var dayStart = targetDay.Date;
        var dayEnd = dayStart.AddDays(1);
        var token = userToken?.Trim() ?? "";

        var story = await _db.DailyPushes
            .Where(d => d.Date >= dayStart && d.Date < dayEnd && d.Type == "story")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        var qa = await _db.DailyPushes
            .Where(d => d.Date >= dayStart && d.Date < dayEnd && d.Type == "qa")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        var news = await _db.DailyPushes
            .Where(d => d.Date >= dayStart && d.Date < dayEnd && d.Type == "news")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        BottleDto? ToPushBottle(Models.DailyPush? push)
        {
            if (push == null) return null;
            return new BottleDto
            {
                Id = push.BottleId ?? 0,
                Content = push.Content,
                ImagePath = push.ImagePath,
                AuthorName = push.Type switch { "story" => "📖 每日故事", "qa" => "❓ 每日问答", "news" => "📰 每日新闻", _ => "📖 每日故事" },
                Type = push.Type,
                CreatedAt = push.Date.Date + push.CreatedAt.TimeOfDay,
                ExpiresAt = push.Date.Date.AddDays(7)
            };
        }

        return Ok(new DailyPushDto
        {
            Story = ToPushBottle(story),
            Qa = ToPushBottle(qa),
            News = ToPushBottle(news)
        });
    }
}
