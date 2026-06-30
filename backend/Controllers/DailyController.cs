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

    /// <summary>
    /// 获取每日推送。
    /// 无 ?date= 参数时返回最近 7 天窗口的全部推送 + minDate/maxDate 边界（供前端控件使用）。
    /// 有 ?date= 参数时返回指定日期的推送（精确查询，用于外部链接跳转）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Today(
        [FromQuery] string? date,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
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

        // ── 指定日期：保持原有精确查询（外部链接跳转用）──
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateTime.TryParse(date, out var targetDay))
                return BadRequest("日期格式无效，请使用 yyyy-MM-dd");

            var dayStart = targetDay.Date;
            var dayEnd = dayStart.AddDays(1);

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

            return Ok(new DailyPushDto
            {
                Story = ToPushBottle(story),
                Qa = ToPushBottle(qa),
                News = ToPushBottle(news)
            });
        }

        // ── 无参：返回 7 天窗口全部推送 + 边界（前端据此渲染日期选择器）──
        var recentCutoff = DateTime.Today.AddDays(-6);
        var latestPushDate = await _db.DailyPushes
            .Where(d => d.Date >= recentCutoff && d.Date <= DateTime.Today)
            .OrderByDescending(d => d.Date)
            .Select(d => d.Date)
            .FirstOrDefaultAsync();

        var maxDate = latestPushDate != default
            ? latestPushDate.Date
            : DateTime.Today.AddDays(-1);
        var minDate = maxDate.AddDays(-6);

        var windowStart = minDate;
        var windowEnd = maxDate.AddDays(1);

        var pushes = await _db.DailyPushes
            .Where(d => d.Date >= windowStart && d.Date < windowEnd)
            .OrderBy(d => d.Date)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

        var days = new List<DailyDayItem>();
        for (var d = windowStart; d <= maxDate; d = d.AddDays(1))
        {
            var dayPushes = pushes.Where(p => p.Date.Date == d).ToList();
            days.Add(new DailyDayItem
            {
                Date = d.ToString("yyyy-MM-dd"),
                Story = ToPushBottle(dayPushes.FirstOrDefault(p => p.Type == "story")),
                Qa = ToPushBottle(dayPushes.FirstOrDefault(p => p.Type == "qa")),
                News = ToPushBottle(dayPushes.FirstOrDefault(p => p.Type == "news"))
            });
        }

        return Ok(new DailyListResponse
        {
            MinDate = minDate.ToString("yyyy-MM-dd"),
            MaxDate = maxDate.ToString("yyyy-MM-dd"),
            Days = days
        });
    }
}
