using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizFlow.Domain.Activities;
using QuizFlow.Domain.Common;
using QuizFlow.Domain.Organizations;
using QuizFlow.Infrastructure.Identity;

namespace QuizFlow.Infrastructure.Persistence.Configurations;

internal static class EntityConfiguration
{
    public static void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : Entity
    {
        entity.ToTable(table);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.CreatedAtUtc).IsRequired();
        entity.Property(item => item.UpdatedAtUtc).IsRequired();
        entity.HasIndex(item => item.DeletedAtUtc);
    }

    public static void ConfigureTenant<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : OrganizationEntity
    {
        ConfigureBase(entity, table);
        entity.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(item => item.OrganizationId);
    }
}

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        EntityConfiguration.ConfigureBase(entity, "Organizations");
        entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> entity)
    {
        EntityConfiguration.ConfigureBase(entity, "OrganizationMemberships");
        entity.HasOne(x => x.Organization).WithMany(x => x.Memberships).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        entity.HasIndex(x => x.UserId);
        entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "Quizzes");
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Language).HasMaxLength(16).IsRequired();
        entity.Property(x => x.Tags).HasColumnType("text[]");
        entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.OwnsOne(x => x.Settings, settings => settings.ToJson("Settings"));
        entity.HasIndex(x => new { x.OrganizationId, x.Status, x.UpdatedAtUtc });
        entity.HasIndex(x => new { x.OrganizationId, x.FolderId });
        entity.HasOne(x => x.Folder).WithMany().HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(x => x.Questions).WithOne(x => x.Quiz).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizFolderConfiguration : IEntityTypeConfiguration<QuizFolder>
{
    public void Configure(EntityTypeBuilder<QuizFolder> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "QuizFolders");
        entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        entity.HasIndex(x => new { x.OrganizationId, x.ParentFolderId, x.Name }).IsUnique();
        entity.HasOne(x => x.ParentFolder).WithMany().HasForeignKey(x => x.ParentFolderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "QuizQuestions");
        entity.Property(x => x.Title).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(8000);
        entity.Property(x => x.Explanation).HasMaxLength(8000);
        entity.Property(x => x.Points).HasPrecision(10, 2);
        entity.Property(x => x.NegativePoints).HasPrecision(10, 2);
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(16);
        entity.Property(x => x.AnswerConfigurationJson).HasColumnType("jsonb");
        entity.HasIndex(x => new { x.QuizId, x.Position }).IsUnique();
        entity.HasMany(x => x.Options).WithOne(x => x.Question).HasForeignKey(x => x.QuizQuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizQuestionOptionConfiguration : IEntityTypeConfiguration<QuizQuestionOption>
{
    public void Configure(EntityTypeBuilder<QuizQuestionOption> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "QuizQuestionOptions");
        entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        entity.HasIndex(x => new { x.QuizQuestionId, x.Position }).IsUnique();
    }
}

public sealed class PollConfiguration : IEntityTypeConfiguration<Poll>
{
    public void Configure(EntityTypeBuilder<Poll> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "Polls");
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.OwnsOne(x => x.Settings, settings => settings.ToJson("Settings"));
        entity.HasIndex(x => new { x.OrganizationId, x.Status, x.UpdatedAtUtc });
        entity.HasMany(x => x.Questions).WithOne(x => x.Poll).HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PollQuestionConfiguration : IEntityTypeConfiguration<PollQuestion>
{
    public void Configure(EntityTypeBuilder<PollQuestion> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "PollQuestions");
        entity.Property(x => x.Title).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.ConfigurationJson).HasColumnType("jsonb");
        entity.HasIndex(x => new { x.PollId, x.Position }).IsUnique();
        entity.HasMany(x => x.Options).WithOne(x => x.Question).HasForeignKey(x => x.PollQuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "PollOptions");
        entity.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        entity.HasIndex(x => new { x.PollQuestionId, x.Position }).IsUnique();
    }
}

public sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "Surveys");
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Language).HasMaxLength(16).IsRequired();
        entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.OwnsOne(x => x.Settings, settings => settings.ToJson("Settings"));
        entity.HasIndex(x => new { x.OrganizationId, x.Status, x.UpdatedAtUtc });
        entity.HasMany(x => x.Pages).WithOne(x => x.Survey).HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SurveyPageConfiguration : IEntityTypeConfiguration<SurveyPage>
{
    public void Configure(EntityTypeBuilder<SurveyPage> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "SurveyPages");
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.HasIndex(x => new { x.SurveyId, x.Position }).IsUnique();
        entity.HasMany(x => x.Questions).WithOne(x => x.Page).HasForeignKey(x => x.SurveyPageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SurveyQuestionConfiguration : IEntityTypeConfiguration<SurveyQuestion>
{
    public void Configure(EntityTypeBuilder<SurveyQuestion> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "SurveyQuestions");
        entity.Property(x => x.Title).HasMaxLength(2000).IsRequired();
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.ValidationConfigurationJson).HasColumnType("jsonb");
        entity.HasIndex(x => new { x.SurveyPageId, x.Position }).IsUnique();
        entity.HasMany(x => x.Options).WithOne(x => x.Question).HasForeignKey(x => x.SurveyQuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SurveyQuestionOptionConfiguration : IEntityTypeConfiguration<SurveyQuestionOption>
{
    public void Configure(EntityTypeBuilder<SurveyQuestionOption> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "SurveyQuestionOptions");
        entity.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        entity.HasIndex(x => new { x.SurveyQuestionId, x.Position }).IsUnique();
    }
}

public sealed class SurveyLogicRuleConfiguration : IEntityTypeConfiguration<SurveyLogicRule>
{
    public void Configure(EntityTypeBuilder<SurveyLogicRule> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "SurveyLogicRules");
        entity.Property(x => x.ConditionJson).HasColumnType("jsonb").IsRequired();
        entity.HasIndex(x => new { x.SurveyId, x.Priority });
        entity.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<SurveyQuestion>().WithMany().HasForeignKey(x => x.SourceQuestionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<SurveyPage>().WithMany().HasForeignKey(x => x.TargetPageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MediaAttachmentConfiguration : IEntityTypeConfiguration<MediaAttachment>
{
    public void Configure(EntityTypeBuilder<MediaAttachment> entity)
    {
        EntityConfiguration.ConfigureTenant(entity, "MediaAttachments");
        entity.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        entity.Property(x => x.AltText).HasMaxLength(500);
        entity.HasIndex(x => new { x.OrganizationId, x.StorageKey }).IsUnique();
    }
}
