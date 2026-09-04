using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class GitHubRepoLinkConfiguration : IEntityTypeConfiguration<GitHubRepoLink>
{
    public void Configure(EntityTypeBuilder<GitHubRepoLink> builder)
    {
        builder.ToTable("github_repo_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new GitHubRepoLinkId(v));

        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        builder.HasIndex(x => x.BoardId);

        builder.Property(x => x.RepoFullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Events).IsRequired();
        builder.Property(x => x.Active).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.HasIndex(x => new { x.BoardId, x.RepoFullName })
            .HasDatabaseName("IX_github_repo_links_BoardId_RepoFullName")
            .IsUnique();
    }
}

public sealed class GitHubPullRequestLinkConfiguration : IEntityTypeConfiguration<GitHubPullRequestLink>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequestLink> builder)
    {
        builder.ToTable("github_pull_request_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new GitHubPullRequestLinkId(v));

        builder.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new Cardscape.Domain.Cards.CardId(v))
            .IsRequired();
        builder.HasIndex(x => x.CardId);

        builder.Property(x => x.RepoFullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PullRequestNumber).IsRequired();
        builder.Property(x => x.PullRequestUrl).HasMaxLength(500);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
