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
public class BottlesController : ControllerBase
{
    private readonly AquariusDbContext _db;
    private readonly IWebHostEnvironment _env;

    public BottlesController(AquariusDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>投瓶</summary>
    [HttpPost]
    public async Task<ActionResult<BottleDto>> Throw([FromBody] ThrowBottleRequest req,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var userId = GetUserId();

        string? imagePath = null;
        if (!string.IsNullOrWhiteSpace(req.ImageBase64))
            imagePath = await SaveImage(req.ImageBase64);

        var bottle = new Bottle
        {
            Content = req.Content,
            ImagePath = imagePath,
            AuthorName = string.IsNullOrWhiteSpace(req.AuthorName) ? null : req.AuthorName.Trim(),
            UserToken = token,
            UserId = userId,
            Type = "normal",
            RequireLogin = req.RequireLogin,
            CommentsPrivate = req.CommentsPrivate,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsAdminBadge = req.IsAdminBadge && User.FindFirst("isAdmin")?.Value == "true"
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
        var userId = GetUserId();

        var query = _db.Bottles.Where(b => b.Type == "normal" && b.ExpiresAt > DateTime.UtcNow);

        // 登录可见：未登录用户不捞到标记了 RequireLogin 的瓶子
        if (userId == null)
            query = query.Where(b => !b.RequireLogin);

        var count = await query.CountAsync();
        if (count == 0) return Ok(null as BottleDto);

        var skip = System.Random.Shared.Next(count);
        var bottle = await query
            .Include(b => b.Comments)
            .Skip(skip)
            .FirstAsync();

        return Ok(await ToDto(bottle, token));
    }

    /// <summary>查看单个瓶</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BottleDto>> GetOne(int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var bottle = await _db.Bottles
            .Include(b => b.Comments)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bottle == null) return NotFound();

        // 登录可见保护：未登录用户不能直接查看
        if (bottle.RequireLogin && GetUserId() == null)
            return NotFound();

        // 过期保护：已过期的瓶子返回 404
        if (bottle.ExpiresAt <= DateTime.UtcNow)
            return NotFound();

        return Ok(await ToDto(bottle, token));
    }

    /// <summary>我点赞的瓶子（分页，每页 15 条）</summary>
    [HttpGet("liked")]
    public async Task<ActionResult> Liked(
        [FromHeader(Name = "X-User-Token")] string? userToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        var token = ResolveToken(userToken);

        var likeQuery = _db.Likes
            .Where(l => l.UserToken == token)
            .OrderByDescending(l => l.CreatedAt);

        var total = await likeQuery.CountAsync();

        var likedBottleIds = await likeQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => l.BottleId)
            .ToListAsync();

        var bottles = await _db.Bottles
            .Include(b => b.Comments)
            .Where(b => likedBottleIds.Contains(b.Id))
            .ToListAsync();

        // 按点赞顺序排列
        var items = new List<BottleDto>();
        foreach (var id in likedBottleIds)
        {
            var bottle = bottles.FirstOrDefault(b => b.Id == id);
            if (bottle != null)
                items.Add(await ToDto(bottle, token));
        }

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>我投的瓶子（登录用户 + 匿名用户）</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<BottleDto>>> Mine(
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var userId = GetUserId();

        var query = _db.Bottles.Include(b => b.Comments).AsQueryable();

        if (userId != null)
            // 登录用户：按 UserId 查
            query = query.Where(b => b.UserId == userId.Value);
        else
            // 匿名用户：按 UserToken 查
            query = query.Where(b => b.UserToken == token);

        var bottles = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var result = new List<BottleDto>();
        foreach (var b in bottles)
            result.Add(await ToDto(b, token));

        return Ok(result);
    }

    /// <summary>编辑自己的瓶子</summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<BottleDto>> Edit(int id, [FromBody] EditBottleRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();
        if (bottle.UserId != userId.Value) return Forbid();

        string? imagePath = bottle.ImagePath;
        if (!string.IsNullOrWhiteSpace(req.ImageBase64))
            imagePath = await SaveImage(req.ImageBase64);

        bottle.Content = req.Content;
        bottle.ImagePath = imagePath;
        bottle.AuthorName = string.IsNullOrWhiteSpace(req.AuthorName) ? bottle.AuthorName : req.AuthorName.Trim();
        bottle.EditedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(await ToDto(bottle, ResolveToken(null)));
    }

    /// <summary>删除自己的瓶子</summary>
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();
        if (bottle.UserId != userId.Value) return Forbid();

        _db.Bottles.Remove(bottle);
        await _db.SaveChangesAsync();

        return NoContent();
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

    /// <summary>重新投出瓶子（续期 7 天）</summary>
    [HttpPost("{id}/rethrow")]
    public async Task<ActionResult<BottleDto>> Rethrow(int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var token = ResolveToken(userToken);
        var userId = GetUserId();
        var bottle = await _db.Bottles.FindAsync(id);
        if (bottle == null) return NotFound();

        // 验证作者身份
        bool isAuthor = (bottle.UserId != null && bottle.UserId == userId)
            || (bottle.UserId == null && bottle.UserToken == token);
        if (!isAuthor) return Forbid();

        bottle.ReThrowCount++;
        bottle.LastReThrowAt = DateTime.UtcNow;
        bottle.ExpiresAt = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return Ok(await ToDto(bottle, token));
    }

    /// <summary>举报瓶子 — 创建意见瓶</summary>
    [Authorize]
    [HttpPost("{id}/report")]
    public async Task<ActionResult<BottleDto>> Report(int id,
        [FromBody] ReportBottleRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var target = await _db.Bottles.FindAsync(id);
        if (target == null) return NotFound();

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return Unauthorized();

        string? imagePath = null;
        if (!string.IsNullOrWhiteSpace(req.ImageBase64))
            imagePath = await SaveImage(req.ImageBase64);

        var bottle = new Bottle
        {
            Content = req.Content,
            ImagePath = imagePath,
            AuthorName = user.Username,
            UserToken = "system",
            UserId = userId,
            Type = "suggestion",
            ReportedBottleId = id,
            CommentsPrivate = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };

        _db.Bottles.Add(bottle);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOne), new { id = bottle.Id }, await ToDto(bottle, "system"));
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
            CreatedAt = b.CreatedAt,
            EditedAt = b.EditedAt,
            UserId = b.UserId,
            RequireLogin = b.RequireLogin,
            CommentsPrivate = b.CommentsPrivate,
            ExpiresAt = b.ExpiresAt,
            ReThrowCount = b.ReThrowCount,
            LastReThrowAt = b.LastReThrowAt,
            IsAdminBadge = b.IsAdminBadge,
            ReportedBottleId = b.ReportedBottleId
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

public class ReportBottleRequest
{
    public string Content { get; set; } = string.Empty;
    public string? ImageBase64 { get; set; }
}