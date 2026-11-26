using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuizFlow.Infrastructure.Persistence;

public sealed class QuizFlowDesignTimeFactory : IDesignTimeDbContextFactory<QuizFlowDbContext>
{
    public QuizFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("QUIZFLOW_DESIGN_CONNECTION")
            ?? throw new InvalidOperationException("Set QUIZFLOW_DESIGN_CONNECTION when generating EF migrations.");
        var options = new DbContextOptionsBuilder<QuizFlowDbContext>().UseNpgsql(connectionString).Options;
        return new QuizFlowDbContext(options);
    }
}
