using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Dtos;
using Aquarius.Api.Models;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/bottles/{bottleId:int}/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public CommentsController(AquariusDbContext db) => _db = db;

    /// <summary>获取某个瓶的评论列表（匿名，不返回 UserToken）</summary>
    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> List(int bottleId)
    {
        var exists = await _db.Bottles.AnyAsync(b => b.Id == bottleId);
        if (!exists) return NotFound();

        var comments = await _db.Comments
            .Where(c => c.BottleId == bottleId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                CreatedAt = c.CreatedAt
                // UserToken intentionally null for normal users
            })
            .ToListAsync();

        return Ok(comments);
    }

    /// <summary>匿名评论</summary>
    [HttpPost]
    public async Task<ActionResult<CommentDto>> Add(int bottleId,
        [FromBody] AddCommentRequest req,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var exists = await _db.Bottles.AnyAsync(b => b.Id == bottleId);
        if (!exists) return NotFound();

        var token = !string.IsNullOrWhiteSpace(userToken) ? userToken.Trim()
            : Guid.NewGuid().ToString("N");

        var comment = new Comment
        {
            BottleId = bottleId,
            Content = req.Content,
            UserToken = token,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new { bottleId }, new CommentDto
        {
            Id = comment.Id,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        });
    }

    /// <summary>删除自己发的评论</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int bottleId, int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id && c.BottleId == bottleId);
        if (comment == null) return NotFound();

        if (comment.UserToken != (userToken ?? "").Trim())
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
