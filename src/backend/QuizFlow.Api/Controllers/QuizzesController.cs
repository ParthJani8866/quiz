using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizFlow.Domain.Activities;
using QuizFlow.Infrastructure.Persistence;

namespace QuizFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/quizzes")]
public sealed class QuizzesController(QuizFlowDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizSummary>>> List(CancellationToken cancellationToken)
    {
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(quiz => quiz.OrganizationId == organizationId && quiz.DeletedAtUtc == null)
            .OrderByDescending(quiz => quiz.UpdatedAtUtc)
            .Select(quiz => new QuizSummary(quiz.Id, quiz.Title, quiz.Description, quiz.Status.ToString(),
                quiz.Visibility.ToString(), quiz.Questions.Count, quiz.UpdatedAtUtc, quiz.Category, quiz.EstimatedDurationMinutes))
            .ToListAsync(cancellationToken);
        return Ok(quizzes);
    }

    [HttpGet("folders")]
    public async Task<ActionResult<IReadOnlyList<QuizFolderDto>>> ListFolders(CancellationToken cancellationToken)
    {
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        var folders = await db.QuizFolders.AsNoTracking().Where(folder => folder.OrganizationId == organizationId && folder.DeletedAtUtc == null)
            .OrderBy(folder => folder.Name).Select(folder => new QuizFolderDto(folder.Id, folder.Name, folder.ParentFolderId)).ToListAsync(cancellationToken);
        return Ok(folders);
    }

    [HttpPost("folders")]
    public async Task<ActionResult<QuizFolderDto>> CreateFolder(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        var name = request.Name.Trim();
        if (name.Length is < 1 or > 120) return BadRequest(new { errors = new { name = new[] { "Folder names must contain 1 to 120 characters." } } });
        if (request.ParentFolderId.HasValue && !await db.QuizFolders.AnyAsync(folder => folder.Id == request.ParentFolderId && folder.OrganizationId == organizationId && folder.DeletedAtUtc == null, cancellationToken))
            return BadRequest(new { errors = new { parentFolderId = new[] { "The parent folder is not available in this workspace." } } });
        var folder = new QuizFolder { OrganizationId = organizationId.Value, Name = name, ParentFolderId = request.ParentFolderId, CreatedBy = GetUserId() };
        db.QuizFolders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new QuizFolderDto(folder.Id, folder.Name, folder.ParentFolderId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuizDetails>> Get(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        var quiz = await db.Quizzes.AsNoTracking().Include(x => x.Questions).ThenInclude(x => x.Options)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.DeletedAtUtc == null, cancellationToken);
        return quiz is null ? NotFound() : Ok(ToDetails(quiz));
    }

    [HttpPost]
    public async Task<ActionResult<QuizDetails>> Create(SaveQuizRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request, out var errors)) return BadRequest(new { errors });
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        if (!await IsFolderAvailable(request.FolderId, organizationId.Value, cancellationToken))
            return BadRequest(new { errors = new { folderId = new[] { "Choose a folder from this workspace." } } });
        var userId = GetUserId();
        var quiz = new Quiz { OrganizationId = organizationId.Value, CreatedBy = userId };
        Apply(quiz, request, userId);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = quiz.Id }, ToDetails(quiz));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuizDetails>> Update(Guid id, SaveQuizRequest request, CancellationToken cancellationToken)
    {
        if (!Validate(request, out var errors)) return BadRequest(new { errors });
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        if (!await IsFolderAvailable(request.FolderId, organizationId.Value, cancellationToken))
            return BadRequest(new { errors = new { folderId = new[] { "Choose a folder from this workspace." } } });
        var quiz = await db.Quizzes.Include(x => x.Questions).ThenInclude(x => x.Options)
            .SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.DeletedAtUtc == null, cancellationToken);
        if (quiz is null) return NotFound();
        var userId = GetUserId();
        Apply(quiz, request, userId);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDetails(quiz));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = await GetOrganizationId(cancellationToken);
        if (organizationId is null) return Forbid();
        var quiz = await db.Quizzes.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId && x.DeletedAtUtc == null, cancellationToken);
        if (quiz is null) return NotFound();
        quiz.Status = ActivityStatus.Archived;
        quiz.UpdatedAtUtc = DateTimeOffset.UtcNow;
        quiz.UpdatedBy = GetUserId();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Guid?> GetOrganizationId(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null ? null : await db.OrganizationMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.DeletedAtUtc == null)
            .OrderBy(membership => membership.CreatedAtUtc)
            .Select(membership => (Guid?)membership.OrganizationId).FirstOrDefaultAsync(cancellationToken);
    }

    private Guid? GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    private async Task<bool> IsFolderAvailable(Guid? folderId, Guid organizationId, CancellationToken cancellationToken) =>
        folderId is null || await db.QuizFolders.AnyAsync(folder => folder.Id == folderId && folder.OrganizationId == organizationId && folder.DeletedAtUtc == null, cancellationToken);

    private static bool Validate(SaveQuizRequest request, out Dictionary<string, string[]> errors)
    {
        var issues = new Dictionary<string, string[]>();
        if (request.Questions.Count > 100) issues["questions"] = ["A quiz can contain at most 100 questions."];
        if (request.Questions.Any(question => question.Options.Count > 50)) issues["options"] = ["A question can contain at most 50 options."];
        if (request.Questions.Any(question => question.Type is "SingleChoice" or "MultipleSelect" or "TrueFalse" && question.Options.Count < 2))
            issues["questions.options"] = ["Choice questions need at least two answer options."];
        if (request.Questions.Any(question => question.Points < 0 || question.NegativePoints < 0))
            issues["questions.points"] = ["Points cannot be negative."];
        if (request.Settings.MaximumAttempts is < 1 or > 100) issues["settings.maximumAttempts"] = ["Maximum attempts must be from 1 to 100."];
        if (request.Settings.PassingPercentage is < 0 or > 100) issues["settings.passingPercentage"] = ["Passing percentage must be from 0 to 100."];
        if (request.StartsAtUtc.HasValue && request.EndsAtUtc.HasValue && request.EndsAtUtc <= request.StartsAtUtc)
            issues["endsAtUtc"] = ["End date must be later than the start date."];
        errors = issues;
        return issues.Count == 0;
    }

    private void Apply(Quiz quiz, SaveQuizRequest request, Guid? userId)
    {
        quiz.Title = request.Title.Trim();
        quiz.FolderId = request.FolderId;
        quiz.Description = request.Description?.Trim();
        quiz.CoverImageUrl = request.CoverImageUrl;
        quiz.ThumbnailUrl = request.ThumbnailUrl;
        quiz.Category = request.Category?.Trim();
        quiz.Language = request.Language;
        quiz.Visibility = Enum.Parse<ActivityVisibility>(request.Visibility, true);
        quiz.Status = Enum.Parse<ActivityStatus>(request.Status, true);
        quiz.Instructions = request.Instructions;
        quiz.StartsAtUtc = request.StartsAtUtc;
        quiz.EndsAtUtc = request.EndsAtUtc;
        quiz.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        quiz.Tags = request.Tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
        quiz.Settings = request.Settings.ToDomain();
        quiz.UpdatedAtUtc = DateTimeOffset.UtcNow;
        quiz.UpdatedBy = userId;
        var originalQuestions = quiz.Questions.ToDictionary(question => question.Id);
        var retainedIds = request.Questions.Where(question => question.Id.HasValue).Select(question => question.Id!.Value).ToHashSet();
        foreach (var removed in quiz.Questions.Where(question => !retainedIds.Contains(question.Id)).ToList()) db.QuizQuestions.Remove(removed);
        var ordered = new List<QuizQuestion>();
        for (var index = 0; index < request.Questions.Count; index++)
        {
            var source = request.Questions[index];
            var question = source.Id.HasValue && originalQuestions.TryGetValue(source.Id.Value, out var existing)
                ? existing : new QuizQuestion { OrganizationId = quiz.OrganizationId, QuizId = quiz.Id, CreatedBy = userId };
            question.Position = index;
            question.Type = Enum.Parse<QuizQuestionType>(source.Type, true);
            question.Title = source.Title.Trim();
            question.Description = source.Description?.Trim();
            question.Explanation = source.Explanation?.Trim();
            question.Points = source.Points;
            question.NegativePoints = source.NegativePoints;
            question.TimeLimitSeconds = source.TimeLimitSeconds;
            question.Difficulty = Enum.Parse<QuestionDifficulty>(source.Difficulty, true);
            question.RandomizeOptions = source.RandomizeOptions;
            question.IsRequired = source.IsRequired;
            question.AnswerConfigurationJson = source.AnswerConfigurationJson;
            question.UpdatedAtUtc = DateTimeOffset.UtcNow;
            question.UpdatedBy = userId;
            var existingOptions = question.Options.ToDictionary(option => option.Id);
            var optionIds = source.Options.Where(option => option.Id.HasValue).Select(option => option.Id!.Value).ToHashSet();
            foreach (var removed in question.Options.Where(option => !optionIds.Contains(option.Id)).ToList()) db.QuizQuestionOptions.Remove(removed);
            question.Options = source.Options.Select((option, optionIndex) =>
            {
                var entity = option.Id.HasValue && existingOptions.TryGetValue(option.Id.Value, out var existingOption)
                    ? existingOption : new QuizQuestionOption { OrganizationId = quiz.OrganizationId, QuizQuestionId = question.Id, CreatedBy = userId };
                entity.Position = optionIndex;
                entity.Text = option.Text.Trim();
                entity.IsCorrect = option.IsCorrect;
                entity.MediaUrl = option.MediaUrl;
                entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
                entity.UpdatedBy = userId;
                return entity;
            }).ToList();
            ordered.Add(question);
        }
        quiz.Questions = ordered;
    }

    private static QuizDetails ToDetails(Quiz quiz) => new(quiz.Id, quiz.Title, quiz.Description, quiz.CoverImageUrl,
        quiz.ThumbnailUrl, quiz.Category, quiz.Language, quiz.Visibility.ToString(), quiz.Status.ToString(), quiz.Instructions,
        quiz.StartsAtUtc, quiz.EndsAtUtc, quiz.EstimatedDurationMinutes, quiz.Tags, quiz.FolderId,
        QuizSettingsDto.From(quiz.Settings), quiz.Questions.OrderBy(question => question.Position)
            .Select(question => new QuizQuestionDto(question.Id, question.Type.ToString(), question.Title, question.Description,
                question.Explanation, question.Points, question.NegativePoints, question.TimeLimitSeconds,
                question.Difficulty.ToString(), question.RandomizeOptions, question.IsRequired, question.AnswerConfigurationJson,
                question.Options.OrderBy(option => option.Position).Select(option => new QuizOptionDto(option.Id, option.Text, option.IsCorrect, option.MediaUrl)).ToList())).ToList());

    public sealed record QuizSummary(Guid Id, string Title, string? Description, string Status, string Visibility,
        int QuestionCount, DateTimeOffset UpdatedAtUtc, string? Category, int? EstimatedDurationMinutes);
    public sealed record QuizDetails(Guid Id, string Title, string? Description, string? CoverImageUrl, string? ThumbnailUrl,
        string? Category, string Language, string Visibility, string Status, string? Instructions, DateTimeOffset? StartsAtUtc,
        DateTimeOffset? EndsAtUtc, int? EstimatedDurationMinutes, string[] Tags, Guid? FolderId, QuizSettingsDto Settings, List<QuizQuestionDto> Questions);
    public sealed record QuizFolderDto(Guid Id, string Name, Guid? ParentFolderId);
    public sealed class CreateFolderRequest
    {
        [Required, StringLength(120, MinimumLength = 1)] public string Name { get; init; } = string.Empty;
        public Guid? ParentFolderId { get; init; }
    }
    public sealed record QuizQuestionDto(Guid Id, string Type, string Title, string? Description, string? Explanation,
        decimal Points, decimal NegativePoints, int? TimeLimitSeconds, string Difficulty, bool RandomizeOptions,
        bool IsRequired, string? AnswerConfigurationJson, List<QuizOptionDto> Options);
    public sealed record QuizOptionDto(Guid Id, string Text, bool IsCorrect, string? MediaUrl);

    public sealed class SaveQuizRequest
    {
        [Required, StringLength(200, MinimumLength = 1)] public string Title { get; init; } = string.Empty;
        [StringLength(4000)] public string? Description { get; init; }
        [Url, StringLength(2048)] public string? CoverImageUrl { get; init; }
        [Url, StringLength(2048)] public string? ThumbnailUrl { get; init; }
        [StringLength(100)] public string? Category { get; init; }
        [Required, StringLength(16)] public string Language { get; init; } = "en";
        [Required, RegularExpression("^(Private|Public|Organization)$")] public string Visibility { get; init; } = "Private";
        [Required, RegularExpression("^(Draft|Published|Archived)$")] public string Status { get; init; } = "Draft";
        [StringLength(4000)] public string? Instructions { get; init; }
        public Guid? FolderId { get; init; }
        public DateTimeOffset? StartsAtUtc { get; init; }
        public DateTimeOffset? EndsAtUtc { get; init; }
        [Range(1, 1440)] public int? EstimatedDurationMinutes { get; init; }
        public string[] Tags { get; init; } = [];
        public QuizSettingsDto Settings { get; init; } = new();
        public List<SaveQuizQuestionRequest> Questions { get; init; } = [];
    }
    public sealed class QuizSettingsDto
    {
        public bool RandomizeQuestionOrder { get; init; }
        public bool RandomizeAnswerOptions { get; init; }
        public bool ShowCorrectAnswersImmediately { get; init; }
        public bool ShowCorrectAnswersAtEnd { get; init; }
        public bool ShowExplanations { get; init; }
        public bool AllowRetries { get; init; }
        [Range(1, 100)] public int? MaximumAttempts { get; init; }
        [Range(typeof(decimal), "0", "100")] public decimal? PassingPercentage { get; init; }
        [Range(1, 86400)] public int? TimeLimitSeconds { get; init; }
        [Range(1, 3600)] public int? PerQuestionTimeLimitSeconds { get; init; }
        public bool EnableLeaderboard { get; init; } = true;
        public bool RequireParticipantName { get; init; }
        public bool AllowAnonymousParticipation { get; init; } = true;
        public bool DisplayParticipantResults { get; init; } = true;
        public bool AllowAnswerReview { get; init; }
        public bool PreventMultipleSubmissions { get; init; } = true;
        internal QuizSettings ToDomain() => new()
        {
            RandomizeQuestionOrder = RandomizeQuestionOrder, RandomizeAnswerOptions = RandomizeAnswerOptions,
            ShowCorrectAnswersImmediately = ShowCorrectAnswersImmediately, ShowCorrectAnswersAtEnd = ShowCorrectAnswersAtEnd,
            ShowExplanations = ShowExplanations, AllowRetries = AllowRetries, MaximumAttempts = MaximumAttempts,
            PassingPercentage = PassingPercentage, TimeLimitSeconds = TimeLimitSeconds,
            PerQuestionTimeLimitSeconds = PerQuestionTimeLimitSeconds, EnableLeaderboard = EnableLeaderboard,
            RequireParticipantName = RequireParticipantName, AllowAnonymousParticipation = AllowAnonymousParticipation,
            DisplayParticipantResults = DisplayParticipantResults, AllowAnswerReview = AllowAnswerReview,
            PreventMultipleSubmissions = PreventMultipleSubmissions
        };
        internal static QuizSettingsDto From(QuizSettings source) => new()
        {
            RandomizeQuestionOrder = source.RandomizeQuestionOrder, RandomizeAnswerOptions = source.RandomizeAnswerOptions,
            ShowCorrectAnswersImmediately = source.ShowCorrectAnswersImmediately, ShowCorrectAnswersAtEnd = source.ShowCorrectAnswersAtEnd,
            ShowExplanations = source.ShowExplanations, AllowRetries = source.AllowRetries, MaximumAttempts = source.MaximumAttempts,
            PassingPercentage = source.PassingPercentage, TimeLimitSeconds = source.TimeLimitSeconds,
            PerQuestionTimeLimitSeconds = source.PerQuestionTimeLimitSeconds, EnableLeaderboard = source.EnableLeaderboard,
            RequireParticipantName = source.RequireParticipantName, AllowAnonymousParticipation = source.AllowAnonymousParticipation,
            DisplayParticipantResults = source.DisplayParticipantResults, AllowAnswerReview = source.AllowAnswerReview,
            PreventMultipleSubmissions = source.PreventMultipleSubmissions
        };
    }
    public sealed class SaveQuizQuestionRequest
    {
        public Guid? Id { get; init; }
        [Required, RegularExpression("^(SingleChoice|MultipleSelect|TrueFalse|TypeAnswer|FillInTheBlanks|Numeric|ImageBased|AudioBased|VideoBased|Matching|Ordering|OpenEnded|Poll)$")]
        public string Type { get; init; } = "SingleChoice";
        [Required, StringLength(2000, MinimumLength = 1)] public string Title { get; init; } = string.Empty;
        [StringLength(8000)] public string? Description { get; init; }
        [StringLength(8000)] public string? Explanation { get; init; }
        [Range(typeof(decimal), "0", "100000")] public decimal Points { get; init; } = 100;
        [Range(typeof(decimal), "0", "100000")] public decimal NegativePoints { get; init; }
        [Range(1, 3600)] public int? TimeLimitSeconds { get; init; }
        [Required, RegularExpression("^(Easy|Medium|Hard)$")] public string Difficulty { get; init; } = "Medium";
        public bool RandomizeOptions { get; init; }
        public bool IsRequired { get; init; } = true;
        public string? AnswerConfigurationJson { get; init; }
        public List<SaveQuizOptionRequest> Options { get; init; } = [];
    }
    public sealed class SaveQuizOptionRequest
    {
        public Guid? Id { get; init; }
        [Required, StringLength(2000, MinimumLength = 1)] public string Text { get; init; } = string.Empty;
        public bool IsCorrect { get; init; }
        [Url, StringLength(2048)] public string? MediaUrl { get; init; }
    }
}
