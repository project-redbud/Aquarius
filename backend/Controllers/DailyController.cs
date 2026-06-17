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

    /// <summary>获取今日推送（故事瓶 + 问答瓶）</summary>
    [HttpGet]
    public async Task<ActionResult<DailyPushDto>> Today(
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var today = DateTime.UtcNow.Date;
        var token = userToken?.Trim() ?? "";

        var story = await _db.DailyPushes
            .Where(d => d.Date == today && d.Type == "story")
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        var qa = await _db.DailyPushes
            .Where(d => d.Date == today && d.Type == "qa")
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
                AuthorName = push.Type == "story" ? "📖 每日故事" : "❓ 每日问答",
                Type = push.Type,
                CreatedAt = push.CreatedAt
            };
        }

        return Ok(new DailyPushDto
        {
            Story = ToPushBottle(story),
            Qa = ToPushBottle(qa)
        });
    }
}
