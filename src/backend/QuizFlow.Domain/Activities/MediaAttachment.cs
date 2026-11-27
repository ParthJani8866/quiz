using QuizFlow.Domain.Common;

namespace QuizFlow.Domain.Activities;

public sealed class MediaAttachment : OrganizationEntity
{
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? AltText { get; set; }
}
