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
    public async Task<ActionResult<List<CommentDto>>> List(int bottleId,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var exists = await _db.Bottles.AnyAsync(b => b.Id == bottleId);
        if (!exists) return NotFound();

        var bottle = await _db.Bottles.FindAsync(bottleId);
        if (bottle == null) return NotFound();

        var comments = await _db.Comments
            .Include(c => c.Replies).ThenInclude(r => r.Replies)
            .Where(c => c.BottleId == bottleId && c.CommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // 评论仅作者可见保护
        if (bottle.CommentsPrivate)
        {
            var userId = GetUserId();
            var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
            var token = (userToken ?? "").Trim();
            bool isAuthor = (bottle.UserId != null && bottle.UserId == userId)
                || (bottle.UserId == null && bottle.UserToken == token);

            // 管理员私评开关
            if (isAdmin && userId != null)
            {
                var admin = await _db.Users.FindAsync(userId.Value);
                if (admin != null && !admin.ViewPrivateComments)
                    isAdmin = false;
            }

            if (isAdmin)
            {
                Response.Headers["X-Comments-Private"] = "admin";
            }
            else if (!isAuthor)
            {
                // 非瓶主、非管理员：只保留评论者自己的评论
                comments = comments
                    .Where(c => IsOwnComment(c, userId, token))
                    .Select(c => FilterRepliesForUser(c, userId, token))
                    .ToList();
                Response.Headers["X-Comments-Private"] = "true";
            }
        }

        var result = comments.Select(c => ToDto(c, includeReplies: true)).ToList();

        // 为管理员评论填充用户名（从 raw 实体收集，DTO 可能已清空 UserId）
        var adminIds = comments
            .Where(c => c.IsAdminBadge && c.UserId != null)
            .Select(c => c.UserId!.Value)
            .Union(comments.SelectMany(c => c.Replies)
                .Where(r => r.IsAdminBadge && r.UserId != null)
                .Select(r => r.UserId!.Value))
            .Distinct().ToList();
        var adminNames = new Dictionary<int, string>();
        foreach (var aid in adminIds)
        {
            var u = await _db.Users.FindAsync(aid);
            if (u != null && u.ShowAdminUsername) adminNames[aid] = u.Username;
        }
        foreach (var c in result)
        {
            if (c.IsAdminBadge && c.UserId != null && adminNames.ContainsKey(c.UserId.Value))
                c.AdminUsername = adminNames[c.UserId.Value];
            // Also set for nested replies
            SetReplyAdminUsernames(c.Replies, adminNames);
        }

        return Ok(result);
    }

    private static void SetReplyAdminUsernames(List<CommentDto> replies, Dictionary<int, string> names)
    {
        foreach (var r in replies)
        {
            if (r.IsAdminBadge && r.UserId != null && names.ContainsKey(r.UserId.Value))
                r.AdminUsername = names[r.UserId.Value];
            if (r.Replies.Count > 0)
                SetReplyAdminUsernames(r.Replies, names);
        }
    }

    private static bool IsOwnComment(Comment c, int? userId, string token)
    {
        return (c.UserId != null && c.UserId == userId)
            || (c.UserId == null && c.UserToken == token);
    }

    private static Comment FilterRepliesForUser(Comment c, int? userId, string token)
    {
        c.Replies = c.Replies
            .Where(r => (r.UserId != null && r.UserId == userId)
                     || (r.UserId == null && r.UserToken == token))
            .ToList();
        return c;
    }

    /// <summary>展开某条评论/回复的全部子回复</summary>
    [HttpGet("{id}/replies")]
    public async Task<ActionResult<List<CommentDto>>> GetReplies(int bottleId, int id,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var bottle = await _db.Bottles.FindAsync(bottleId);
        if (bottle == null) return NotFound();

        var replies = await _db.Comments
            .Include(c => c.Replies)
            .Include(c => c.ParentReply)
            .Where(c => c.CommentId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        // 评论仅作者可见保护
        if (bottle.CommentsPrivate)
        {
            var userId = GetUserId();
            var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
            var token = (userToken ?? "").Trim();
            bool isAuthor = (bottle.UserId != null && bottle.UserId == userId)
                || (bottle.UserId == null && bottle.UserToken == token);

            // 管理员私评开关
            if (isAdmin && userId != null)
            {
                var admin = await _db.Users.FindAsync(userId.Value);
                if (admin != null && !admin.ViewPrivateComments)
                    isAdmin = false;
            }

            if (!isAdmin && !isAuthor)
            {
                replies = replies
                    .Where(r => IsOwnComment(r, userId, token))
                    .ToList();
            }
        }

        var replyDtos = replies.Select(r => ToDto(r, includeReplies: false)).ToList();

        var adminIds = replies
            .Where(r => r.IsAdminBadge && r.UserId != null)
            .Select(r => r.UserId!.Value)
            .Union(replies.SelectMany(r => r.Replies)
                .Where(rr => rr.IsAdminBadge && rr.UserId != null)
                .Select(rr => rr.UserId!.Value))
            .Distinct().ToList();
        var names = new Dictionary<int, string>();
        foreach (var aid in adminIds)
        {
            var u = await _db.Users.FindAsync(aid);
            if (u != null && u.ShowAdminUsername) names[aid] = u.Username;
        }
        foreach (var c in replyDtos)
        {
            if (c.IsAdminBadge && c.UserId != null && names.ContainsKey(c.UserId.Value))
                c.AdminUsername = names[c.UserId.Value];
            SetReplyAdminUsernames(c.Replies, names);
        }

        return Ok(replyDtos);
    }

    /// <summary>发表评论/回复（需要登录 + 邮箱已验证）</summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CommentDto>> Add(int bottleId,
        [FromBody] AddCommentRequest req,
        [FromHeader(Name = "X-User-Token")] string? userToken)
    {
        var bottle = await _db.Bottles.FindAsync(bottleId);
        if (bottle == null) return NotFound();

        var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
        if (bottle.IsClosed && !isAdmin)
            return BadRequest(new { error = "瓶子已关闭，无法评论" });

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // 邮箱未验证不可评论
        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null || !user.EmailVerified)
            return Unauthorized(new { error = "请先验证邮箱后再评论" });

        var token = !string.IsNullOrWhiteSpace(userToken) ? userToken.Trim()
            : Guid.NewGuid().ToString("N");

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
            CreatedAt = DateTime.UtcNow,
            IsAdminBadge = req.IsAdminBadge && User.FindFirst("isAdmin")?.Value == "true",
            IsBottleOwnerBadge = req.IsBottleOwnerBadge
                && ((bottle.UserId != null && bottle.UserId == userId)
                 || (bottle.UserId == null && bottle.UserToken == token))
        };

        _db.Comments.Add(comment);

        // 通知瓶主（评论）— 不通知自己
        if (bottle.UserId != null && bottle.UserId != userId)
        {
            _db.Notifications.Add(new Models.Notification
            {
                UserId = bottle.UserId.Value,
                Type = "comment",
                Title = "瓶子有人评论了",
                Content = comment.Content.Length > 200 ? comment.Content[..200] + "..." : comment.Content,
                RelatedBottleId = bottleId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 通知被回复的评论作者（楼中楼）— 不通知自己，也不重复通知瓶主
        if (parentReplyId != null)
        {
            var parentReply = await _db.Comments.FindAsync(parentReplyId.Value);
            if (parentReply?.UserId != null && parentReply.UserId != userId && parentReply.UserId != bottle.UserId)
            {
                _db.Notifications.Add(new Models.Notification
                {
                    UserId = parentReply.UserId.Value,
                    Type = "comment",
                    Title = "有人回复了你的评论",
                    Content = comment.Content.Length > 200 ? comment.Content[..200] + "..." : comment.Content,
                    RelatedBottleId = bottleId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

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

    private CommentDto ToDto(Comment c, bool includeReplies)
    {
        string? parentContent = null;
        if (c.ParentReply != null)
        {
            var body = c.ParentReply.Content.Length > 30
                ? c.ParentReply.Content[..30] + "..."
                : c.ParentReply.Content;
            parentContent = $"#{c.ParentReplyId}: {body}";
        }

        var isAdmin = User.FindFirst("isAdmin")?.Value == "true";
        var currentUserId = GetUserId();
        var dto = new CommentDto
        {
            Id = c.Id,
            Content = c.Content,
            CommentId = c.CommentId,
            ParentReplyId = c.ParentReplyId,
            CreatedAt = c.CreatedAt,
            EditedAt = c.EditedAt,
            UserId = (isAdmin || c.IsAdminBadge || c.UserId == currentUserId) ? c.UserId : null,
            ReplyCount = c.Replies.Count,
            ParentContent = parentContent,
            IsAdminBadge = c.IsAdminBadge,
            IsBottleOwnerBadge = c.IsBottleOwnerBadge
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
        [FromHeader(Name = "X-User-Token")] string? userToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        var userId = GetUserId();
        var token = (userToken ?? "").Trim();

        var query = _db.Comments.Include(c => c.Bottle).AsQueryable();

        if (userId != null)
            query = query.Where(c => c.UserId == userId.Value);
        else if (!string.IsNullOrEmpty(token))
            query = query.Where(c => c.UserToken == token);
        else
            return Ok(new { items = new List<object>(), total = 0, page = 1, pageSize = 15 });

        var total = await query.CountAsync();
        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.CreatedAt,
                c.EditedAt,
                c.CommentId,
                c.ParentReplyId,
                BottleId = c.BottleId,
                BottleContent = c.Bottle!.Content.Length > 200
                    ? c.Bottle.Content.Substring(0, 200) + "..."
                    : c.Bottle.Content
            })
            .ToListAsync();

        return Ok(new { items = comments, total, page, pageSize });
    }
}