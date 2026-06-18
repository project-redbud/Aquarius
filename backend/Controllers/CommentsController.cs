using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>获取瓶子的顶级评论列表（含前3条子回复）</summary>
    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> List(int bottleId)
    {
        var exists = await _db.Bottles.AnyAsync(b => b.Id == bottleId);
        if (!exists) return NotFound();

        var comments = await _db.Comments
            .Include(c => c.Replies).ThenInclude(r => r.Replies)
            .Where(c => c.BottleId == bottleId && c.CommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(comments.Select(c => ToDto(c, includeReplies: true)).ToList());
    }

    /// <summary>展开某条评论/回复的全部子回复</summary>
    [HttpGet("{id}/replies")]
    public async Task<ActionResult<List<CommentDto>>> GetReplies(int bottleId, int id)
    {
        var replies = await _db.Comments
            .Include(c => c.Replies)
            .Include(c => c.ParentReply)
            .Where(c => c.CommentId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return Ok(replies.Select(r => ToDto(r, includeReplies: false)).ToList());
    }

    /// <summary>发表评论/回复</summary>
    [HttpPost]
    public async Task<ActionResult<CommentDto>> Add(int bottleId,
        [FromBody] AddCommentRequest req,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var exists = await _db.Bottles.AnyAsync(b => b.Id == bottleId);
        if (!exists) return NotFound();

        var token = !string.IsNullOrWhiteSpace(userToken) ? userToken.Trim()
            : Guid.NewGuid().ToString("N");

        var userId = GetUserId();

        // 如果是回复，确定 commentId
        int? commentId = req.CommentId;
        int? parentReplyId = req.ParentReplyId;

        if (commentId == null && parentReplyId != null)
        {
            var parentReply = await _db.Comments.FindAsync(parentReplyId.Value);
            if (parentReply == null) return BadRequest("目标回复不存在");
            commentId = parentReply.CommentId ?? parentReply.Id;
        }

        var comment = new Comment
        {
            BottleId = bottleId,
            Content = req.Content,
            UserToken = token,
            UserId = userId,
            CommentId = commentId,
            ParentReplyId = parentReplyId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Load parent reply for quoting
        if (parentReplyId != null)
            await _db.Entry(comment).Reference(c => c.ParentReply).LoadAsync();

        return CreatedAtAction(nameof(List), new { bottleId }, ToDto(comment, includeReplies: false));
    }

    /// <summary>编辑自己的评论</summary>
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<CommentDto>> Edit(int bottleId, int id, [FromBody] EditCommentRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var comment = await _db.Comments
            .Include(c => c.ParentReply)
            .FirstOrDefaultAsync(c => c.Id == id && c.BottleId == bottleId);
        if (comment == null) return NotFound();
        if (comment.UserId != userId.Value) return Forbid();

        comment.Content = req.Content;
        comment.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(comment, includeReplies: false));
    }

    /// <summary>删除自己的评论</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int bottleId, int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var userId = GetUserId();
        var token = userToken ?? "";

        var comment = await _db.Comments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == id && c.BottleId == bottleId);
        if (comment == null) return NotFound();

        // 登录用户按 UserId 验证，匿名用户按 UserToken 验证
        if (userId != null)
        {
            if (comment.UserId != userId.Value) return Forbid();
        }
        else
        {
            if (comment.UserToken != token.Trim()) return Forbid();
        }

        _db.Comments.RemoveRange(comment.Replies);
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── helpers ────────────────────────────────────────────

    private static CommentDto ToDto(Comment c, bool includeReplies)
    {
        string? parentContent = null;
        if (c.ParentReply != null)
        {
            var body = c.ParentReply.Content.Length > 30
                ? c.ParentReply.Content[..30] + "..."
                : c.ParentReply.Content;
            parentContent = $"#{c.ParentReplyId}: {body}";
        }

        var dto = new CommentDto
        {
            Id = c.Id,
            Content = c.Content,
            CommentId = c.CommentId,
            ParentReplyId = c.ParentReplyId,
            CreatedAt = c.CreatedAt,
            EditedAt = c.EditedAt,
            UserId = c.UserId,
            ReplyCount = c.Replies.Count,
            ParentContent = parentContent
        };

        if (includeReplies)
        {
            dto.Replies = c.Replies
                .OrderBy(r => r.CreatedAt)
                .Take(3)
                .Select(r => ToDto(r, false))
                .ToList();
        }

        return dto;
    }
}

/// <summary>独立的评论查询控制器</summary>
[ApiController]
[Route("api/comments")]
public class MyCommentsController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public MyCommentsController(AquariusDbContext db) => _db = db;

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet("mine")]
    public async Task<ActionResult> Mine(
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var userId = GetUserId();
        var token = (userToken ?? "").Trim();

        var query = _db.Comments.Include(c => c.Bottle).AsQueryable();

        if (userId != null)
            query = query.Where(c => c.UserId == userId.Value);
        else if (!string.IsNullOrEmpty(token))
            query = query.Where(c => c.UserToken == token);
        else
            return Ok(new List<object>());

        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.CreatedAt,
                c.EditedAt,
                c.CommentId,
                c.ParentReplyId,
                BottleId = c.BottleId,
                BottleContent = c.Bottle!.Content.Length > 50
                    ? c.Bottle.Content.Substring(0, 50) + "..."
                    : c.Bottle.Content
            })
            .ToListAsync();

        return Ok(comments);
    }
}