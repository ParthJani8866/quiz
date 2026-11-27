using QuizFlow.Domain.Common;

namespace QuizFlow.Domain.Organizations;

public sealed class Organization : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<OrganizationMembership> Memberships { get; set; } = [];
}

public sealed class OrganizationMembership : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public OrganizationRole Role { get; set; }
    public Organization? Organization { get; set; }
}

public enum OrganizationRole { Owner, Admin, Editor, Member }
