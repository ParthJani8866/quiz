using QuizFlow.Domain.Common;

namespace QuizFlow.Domain.Activities;

public sealed class Survey : OrganizationEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Category { get; set; }
    public string Language { get; set; } = "en";
    public ActivityVisibility Visibility { get; set; }
    public ActivityStatus Status { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? ThankYouMessage { get; set; }
    public int? EstimatedCompletionMinutes { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public SurveySettings Settings { get; set; } = new();
    public ICollection<SurveyPage> Pages { get; set; } = [];
}

public sealed class SurveySettings
{
    public bool AnonymousResponses { get; set; } = true;
    public bool RequireAuthentication { get; set; }
    public bool AllowMultipleResponses { get; set; }
    public bool AllowResume { get; set; } = true;
    public bool AutoSaveProgress { get; set; } = true;
    public int? ResponseLimit { get; set; }
    public bool OrganizationMembersOnly { get; set; }
    public bool PasswordProtected { get; set; }
    public string? RedirectUrl { get; set; }
    public string? CustomBrandingJson { get; set; }
}

public sealed class SurveyPage : OrganizationEntity
{
    public Guid SurveyId { get; set; }
    public Survey? Survey { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<SurveyQuestion> Questions { get; set; } = [];
}

public sealed class SurveyQuestion : OrganizationEntity
{
    public Guid SurveyPageId { get; set; }
    public SurveyPage? Page { get; set; }
    public int Position { get; set; }
    public SurveyQuestionType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationConfigurationJson { get; set; }
    public ICollection<SurveyQuestionOption> Options { get; set; } = [];
}

public sealed class SurveyQuestionOption : OrganizationEntity
{
    public Guid SurveyQuestionId { get; set; }
    public SurveyQuestion? Question { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public sealed class SurveyLogicRule : OrganizationEntity
{
    public Guid SurveyId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public Guid? TargetPageId { get; set; }
    public string ConditionJson { get; set; } = "{}";
    public int Priority { get; set; }
}
