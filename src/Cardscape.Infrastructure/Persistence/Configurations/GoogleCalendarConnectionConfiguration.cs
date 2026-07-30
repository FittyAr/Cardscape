using Cardscape.Domain.Integrations.GoogleCalendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class GoogleCalendarConnectionConfiguration
    : IEntityTypeConfiguration<GoogleCalendarConnection>
{
    public void Configure(EntityTypeBuilder<GoogleCalendarConnection> b)
    {
        b.ToTable("google_calendar_connections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new GoogleCalendarConnectionId(v));
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        b.Property(x => x.GoogleEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.EncryptedRefreshToken).HasMaxLength(2048).IsRequired();
        b.Property(x => x.CalendarId).HasMaxLength(256).IsRequired();
        b.Property(x => x.LastSyncedAt);
        b.Property(x => x.LastSyncErrorAt);
        b.Property(x => x.LastSyncError).HasMaxLength(1024);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        b.HasIndex(x => x.UserId).IsUnique();
    }
}
