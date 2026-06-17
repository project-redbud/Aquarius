using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Models;

namespace Aquarius.Api.Data;

public class AquariusDbContext : DbContext
{
    public AquariusDbContext(DbContextOptions<AquariusDbContext> opts) : base(opts) { }

    public DbSet<Bottle> Bottles => Set<Bottle>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<DailyPush> DailyPushes => Set<DailyPush>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Bottle ← Comment
        model.Entity<Comment>()
            .HasOne(c => c.Bottle)
            .WithMany(b => b.Comments)
            .HasForeignKey(c => c.BottleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment self-ref: CommentId → top-level parent
        model.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment self-ref: ParentReplyId → specific reply
        model.Entity<Comment>()
            .HasOne(c => c.ParentReply)
            .WithMany()
            .HasForeignKey(c => c.ParentReplyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Like unique index: one user → one bottle once
        model.Entity<Like>()
            .HasIndex(l => new { l.BottleId, l.UserToken })
            .IsUnique();

        // DailyPush unique index: one type per day
        model.Entity<DailyPush>()
            .HasIndex(d => new { d.Date, d.Type })
            .IsUnique();
    }
}
