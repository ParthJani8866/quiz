using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuizFlow.Infrastructure.Identity;

namespace QuizFlow.Infrastructure.Persistence;

public sealed class QuizFlowDbContext(DbContextOptions<QuizFlowDbContext> options)
    : IdentityDbContext<AppUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(token => token.ConcurrencyStamp).HasMaxLength(32).IsConcurrencyToken();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.ExpiresAt });
            entity.HasOne<AppUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
