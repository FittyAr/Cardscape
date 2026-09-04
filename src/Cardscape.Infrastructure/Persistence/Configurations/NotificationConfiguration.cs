using Cardscape.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new NotificationId(v));
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.IsRead).IsRequired();
        builder.Property(x => x.ReadAt);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
