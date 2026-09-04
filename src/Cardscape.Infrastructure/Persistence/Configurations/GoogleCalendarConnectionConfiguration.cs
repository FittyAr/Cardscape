using Cardscape.Domain.Integrations.GoogleCalendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class GoogleCalendarConnectionConfiguration
    : IEntityTypeConfiguration<GoogleCalendarConnection>
{
    public void Configure(EntityTypeBuilder<GoogleCalendarConnection> builder)
    {
        builder.ToTable("google_calendar_connections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new GoogleCalendarConnectionId(v));
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        builder.Property(x => x.GoogleEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.EncryptedRefreshToken).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.CalendarId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EventMappingsJson).IsRequired();
        builder.Property(x => x.LastSyncedAt);
        builder.Property(x => x.LastSyncErrorAt);
        builder.Property(x => x.LastSyncError).HasMaxLength(1024);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.WorkspaceId);
    }
}
