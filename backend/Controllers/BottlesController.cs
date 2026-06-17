using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;
using Aquarius.Api.Models;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BottlesController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly IWebHostEnvironment _env;

    public BottlesController(AquariusDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>投瓶</summary>
    [HttpPost]
    public async Task<ActionResult<BottleDto>> Throw([FromBody] ThrowBottleRequest req,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);

        string? imagePath = null;
        if (!string.IsNullOrWhiteSpace(req.ImageBase64))
            imagePath = await SaveImage(req.ImageBase64);

        var bottle = new Bottle
        {
            Content = req.Content,
            ImagePath = imagePath,
            AuthorName = string.IsNullOrWhiteSpace(req.AuthorName) ? null : req.AuthorName.Trim(),
            UserToken = token,
            Type = "normal",
            CreatedAt = DateTime.UtcNow
        };

        _db.Bottles.Add(bottle);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOne), new { id = bottle.Id }, await ToDto(bottle, token));
    }

    /// <summary>随机捞一个普通瓶</summary>
    [HttpGet("random")]
    public async Task<ActionResult<BottleDto?>> Random(
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);

        var count = await _db.Bottles.CountAsync(b => b.Type == "normal");
        if (count == 0) return Ok(null as BottleDto);

        var skip = System.Random.Shared.Next(count);
        var bottle = await _db.Bottles
            .Include(b => b.Comments)
            .Where(b => b.Type == "normal")
            .Skip(skip)
            .FirstAsync();

        return Ok(await ToDto(bottle, token));
    }

    /// <summary>查看单个瓶</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BottleDto>> GetOne(int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var bottle = await _db.Bottles
            .Include(b => b.Comments)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bottle == null) return NotFound();

        return Ok(await ToDto(bottle, token));
    }

    /// <summary>我投的瓶子</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<BottleDto>>> Mine(
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var bottles = await _db.Bottles
            .Include(b => b.Comments)
            .Where(b => b.UserToken == token)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var result = new List<BottleDto>();
        foreach (var b in bottles)
            result.Add(await ToDto(b, token));

        return Ok(result);
    }

    /// <summary>点赞/取消赞（toggle）</summary>
    [HttpPost("{id}/like")]
    public async Task<ActionResult> ToggleLike(int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();

        var existing = await _db.Likes
            .FirstOrDefaultAsync(l => l.BottleId == id && l.UserToken == token);

        if (existing != null)
        {
            _db.Likes.Remove(existing);
            bottle.LikeCount = Math.Max(0, bottle.LikeCount - 1);
        }
        else
        {
            _db.Likes.Add(new Like { BottleId = id, UserToken = token });
            bottle.LikeCount++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { liked = existing == null, bottle.LikeCount });
    }

    // ── helpers ────────────────────────────────────────────

    private string ResolveToken(string? headerToken)
    {
        return !string.IsNullOrWhiteSpace(headerToken) ? headerToken.Trim()
            : Guid.NewGuid().ToString("N");
    }

    private async Task<BottleDto> ToDto(Bottle b, string token)
    {
        var liked = await _db.Likes.AnyAsync(l => l.BottleId == b.Id && l.UserToken == token);
        return new BottleDto
        {
            Id = b.Id,
            Content = b.Content,
            ImagePath = b.ImagePath,
            AuthorName = b.AuthorName,
            Type = b.Type,
            LikeCount = b.LikeCount,
            CommentCount = b.Comments.Count,
            LikedByMe = liked,
            CreatedAt = b.CreatedAt
        };
    }

    private async Task<string?> SaveImage(string base64)
    {
        try
        {
            var comma = base64.IndexOf(',');
            var data = comma >= 0 ? base64[(comma + 1)..] : base64;
            var bytes = Convert.FromBase64String(data);

            var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
            Directory.CreateDirectory(uploads);

            var filename = $"{Guid.NewGuid():N}.jpg";
            var filePath = Path.Combine(uploads, filename);
            await System.IO.File.WriteAllBytesAsync(filePath, bytes);

            return $"uploads/{filename}";
        }
        catch
        {
            return null;
        }
    }
}
