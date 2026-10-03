using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PowerHub.Identity.Data;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.DisplayName).HasMaxLength(100);
        });
        builder.Entity<AppRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<RefreshSession>(session =>
        {
            session.ToTable("refresh_sessions");
            session.HasIndex(s => s.TokenHash).IsUnique();
            session.HasIndex(s => s.FamilyId);
            session.HasIndex(s => s.UserId);
            session.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OneTimeToken>(token =>
        {
            token.ToTable("one_time_tokens");
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => new { t.UserId, t.Purpose });
            token.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditEvent>(audit =>
        {
            audit.ToTable("audit_events");
            audit.Property(a => a.Action).HasMaxLength(100);
            audit.Property(a => a.TargetType).HasMaxLength(50);
            audit.Property(a => a.TargetId).HasMaxLength(100);
            audit.Property(a => a.Result).HasMaxLength(20);
            audit.Property(a => a.Source).HasMaxLength(64);
            audit.Property(a => a.CorrelationId).HasMaxLength(64);
            audit.Property(a => a.Detail).HasMaxLength(500);
            audit.HasIndex(a => a.OccurredAt);
            audit.HasIndex(a => new { a.TargetType, a.TargetId });
        });
    }
}
