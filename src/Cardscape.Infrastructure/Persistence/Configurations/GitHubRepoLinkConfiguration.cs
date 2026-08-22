using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.GitHub;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class GitHubRepoLinkConfiguration : IEntityTypeConfiguration<GitHubRepoLink>
{
    public void Configure(EntityTypeBuilder<GitHubRepoLink> b)
    {
        b.ToTable("github_repo_links");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new GitHubRepoLinkId(v));

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        b.HasIndex(x => x.BoardId);

        b.Property(x => x.RepoFullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Events).IsRequired();
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        b.HasIndex(x => new { x.BoardId, x.RepoFullName })
            .HasDatabaseName("IX_github_repo_links_BoardId_RepoFullName")
            .IsUnique();
    }
}

public sealed class GitHubPullRequestLinkConfiguration : IEntityTypeConfiguration<GitHubPullRequestLink>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequestLink> b)
    {
        b.ToTable("github_pull_request_links");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new GitHubPullRequestLinkId(v));

        b.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new Cardscape.Domain.Cards.CardId(v))
            .IsRequired();
        b.HasIndex(x => x.CardId);

        b.Property(x => x.RepoFullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.PullRequestNumber).IsRequired();
        b.Property(x => x.PullRequestUrl).HasMaxLength(500);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
