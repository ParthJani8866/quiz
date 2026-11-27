using QuizFlow.Domain.Common;

namespace QuizFlow.Domain.Activities;

public sealed class Quiz : OrganizationEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Category { get; set; }
    public string Language { get; set; } = "en";
    public ActivityVisibility Visibility { get; set; }
    public ActivityStatus Status { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public Guid? FolderId { get; set; }
    public QuizFolder? Folder { get; set; }
    public string[] Tags { get; set; } = [];
    public QuizSettings Settings { get; set; } = new();
    public ICollection<QuizQuestion> Questions { get; set; } = [];
}

public sealed class QuizFolder : OrganizationEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public QuizFolder? ParentFolder { get; set; }
}

public sealed class QuizSettings
{
    public bool RandomizeQuestionOrder { get; set; }
    public bool RandomizeAnswerOptions { get; set; }
    public bool ShowCorrectAnswersImmediately { get; set; }
    public bool ShowCorrectAnswersAtEnd { get; set; }
    public bool ShowExplanations { get; set; }
    public bool AllowRetries { get; set; }
    public int? MaximumAttempts { get; set; }
    public decimal? PassingPercentage { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public int? PerQuestionTimeLimitSeconds { get; set; }
    public bool EnableLeaderboard { get; set; } = true;
    public bool RequireParticipantName { get; set; }
    public bool AllowAnonymousParticipation { get; set; } = true;
    public bool DisplayParticipantResults { get; set; } = true;
    public bool AllowAnswerReview { get; set; }
    public bool PreventMultipleSubmissions { get; set; } = true;
}

public sealed class QuizQuestion : OrganizationEntity
{
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public int Position { get; set; }
    public QuizQuestionType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Explanation { get; set; }
    public decimal Points { get; set; } = 100;
    public decimal NegativePoints { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public QuestionDifficulty Difficulty { get; set; }
    public bool RandomizeOptions { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? AnswerConfigurationJson { get; set; }
    public ICollection<QuizQuestionOption> Options { get; set; } = [];
}

public sealed class QuizQuestionOption : OrganizationEntity
{
    public Guid QuizQuestionId { get; set; }
    public QuizQuestion? Question { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string? MediaUrl { get; set; }
}
