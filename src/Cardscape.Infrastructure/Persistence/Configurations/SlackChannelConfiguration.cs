using Cardscape.Domain.Boards;
using Cardscape.Domain.Integrations.Slack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SlackChannelConfiguration : IEntityTypeConfiguration<SlackChannel>
{
    public void Configure(EntityTypeBuilder<SlackChannel> b)
    {
        b.ToTable("slack_channels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new SlackChannelId(v));

        b.Property(x => x.SlackWorkspaceId)
            .HasConversion(id => id.Value, v => new SlackWorkspaceId(v))
            .IsRequired();
        b.HasIndex(x => x.SlackWorkspaceId);

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        b.HasIndex(x => x.BoardId);

        b.Property(x => x.ChannelId).HasMaxLength(32).IsRequired();
        b.Property(x => x.ChannelName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Events).IsRequired();
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        b.HasIndex(x => new { x.SlackWorkspaceId, x.BoardId, x.ChannelId })
            .HasDatabaseName("IX_slack_channels_WorkspaceId_BoardId_ChannelId")
            .IsUnique();
    }
}
