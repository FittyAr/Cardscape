using Cardscape.Domain.Integrations.Slack;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SlackWorkspaceConfiguration : IEntityTypeConfiguration<SlackWorkspace>
{
    public void Configure(EntityTypeBuilder<SlackWorkspace> builder)
    {
        builder.ToTable("slack_workspaces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new SlackWorkspaceId(v));

        builder.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        builder.HasIndex(x => x.WorkspaceId).IsUnique();

        builder.Property(x => x.TeamId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TeamName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProtectedBotToken).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.LastUsedAt);
        builder.Property(x => x.Active).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
