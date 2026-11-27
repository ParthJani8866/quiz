using QuizFlow.Domain.Common;

namespace QuizFlow.Domain.Activities;

public sealed class Poll : OrganizationEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Category { get; set; }
    public ActivityVisibility Visibility { get; set; }
    public ActivityStatus Status { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public PollSettings Settings { get; set; } = new();
    public ICollection<PollQuestion> Questions { get; set; } = [];
}

public sealed class PollSettings
{
    public bool AllowMultipleSelections { get; set; }
    public int? MinimumSelections { get; set; }
    public int? MaximumSelections { get; set; }
    public bool AnonymousVoting { get; set; } = true;
    public bool RequireParticipantName { get; set; }
    public bool OneVotePerParticipant { get; set; } = true;
    public bool AllowChangingVotes { get; set; }
    public bool ShowResultsImmediately { get; set; } = true;
    public bool HideResultsUntilClosed { get; set; }
    public bool AllowComments { get; set; }
    public bool EnableLiveResultUpdates { get; set; } = true;
    public bool OrganizationMembersOnly { get; set; }
    public bool AllowSkippingOptionalQuestions { get; set; }
}

public sealed class PollQuestion : OrganizationEntity
{
    public Guid PollId { get; set; }
    public Poll? Poll { get; set; }
    public int Position { get; set; }
    public PollQuestionType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? ConfigurationJson { get; set; }
    public ICollection<PollOption> Options { get; set; } = [];
}

public sealed class PollOption : OrganizationEntity
{
    public Guid PollQuestionId { get; set; }
    public PollQuestion? Question { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
