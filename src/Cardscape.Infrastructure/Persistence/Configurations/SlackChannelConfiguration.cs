using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SlackChannelConfiguration : IEntityTypeConfiguration<SlackChannel>
{
    public void Configure(EntityTypeBuilder<SlackChannel> builder)
    {
        builder.ToTable("slack_channels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new SlackChannelId(v));

        builder.Property(x => x.SlackWorkspaceId)
            .HasConversion(id => id.Value, v => new SlackWorkspaceId(v))
            .IsRequired();
        builder.HasIndex(x => x.SlackWorkspaceId);

        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        builder.HasIndex(x => x.BoardId);

        builder.Property(x => x.ChannelId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ChannelName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Events).IsRequired();
        builder.Property(x => x.Active).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.HasIndex(x => new { x.SlackWorkspaceId, x.BoardId, x.ChannelId })
            .HasDatabaseName("IX_slack_channels_WorkspaceId_BoardId_ChannelId")
            .IsUnique();
    }
}
