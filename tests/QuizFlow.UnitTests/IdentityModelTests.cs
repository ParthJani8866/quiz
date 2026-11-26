using Microsoft.EntityFrameworkCore;
using QuizFlow.Infrastructure.Identity;
using QuizFlow.Infrastructure.Persistence;

namespace QuizFlow.UnitTests;

public sealed class IdentityModelTests
{
    [Fact]
    public void RefreshToken_HasUniqueHashAndOptimisticConcurrencyStamp()
    {
        var options = new DbContextOptionsBuilder<QuizFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=quizflow;Username=unused;Password=unused")
            .Options;
        using var db = new QuizFlowDbContext(options);
        var entity = db.Model.FindEntityType(typeof(RefreshToken));

        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(RefreshToken.TokenHash)));
        Assert.True(entity.FindProperty(nameof(RefreshToken.ConcurrencyStamp))!.IsConcurrencyToken);
    }
}
