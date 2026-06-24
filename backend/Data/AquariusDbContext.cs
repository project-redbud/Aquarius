using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Models;

namespace Aquarius.Api.Data;

public class AquariusDbContext : DbContext
{
    public AquariusDbContext(DbContextOptions<AquariusDbContext> opts) : base(opts) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Bottle> Bottles => Set<Bottle>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<DailyPush> DailyPushes => Set<DailyPush>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<BottleLog> BottleLogs => Set<BottleLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // User unique indexes
        model.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        model.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // User → Bottles
        model.Entity<Bottle>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bottles)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // User → Comments
        model.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // BottleLog → Bottle
        model.Entity<BottleLog>()
            .HasOne(l => l.Bottle)
            .WithMany()
            .HasForeignKey(l => l.BottleId)
            .OnDelete(DeleteBehavior.Cascade);

        // BottleLog → OperatorUser
        model.Entity<BottleLog>()
            .HasOne(l => l.OperatorUser)
            .WithMany()
            .HasForeignKey(l => l.OperatorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Notification → User
        model.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
