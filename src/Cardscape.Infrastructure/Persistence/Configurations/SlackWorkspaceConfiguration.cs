using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SlackWorkspaceConfiguration : IEntityTypeConfiguration<SlackWorkspace>
{
    public void Configure(EntityTypeBuilder<SlackWorkspace> b)
    {
        b.ToTable("slack_workspaces");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new SlackWorkspaceId(v));

        b.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        b.HasIndex(x => x.WorkspaceId).IsUnique();

        b.Property(x => x.TeamId).HasMaxLength(32).IsRequired();
        b.Property(x => x.TeamName).HasMaxLength(200).IsRequired();
        b.Property(x => x.BotTokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.LastUsedAt);
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
