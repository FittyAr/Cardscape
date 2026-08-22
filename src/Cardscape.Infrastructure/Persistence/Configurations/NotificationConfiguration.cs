using Cardscape.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new NotificationId(v));
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        b.Property(x => x.IsRead).IsRequired();
        b.Property(x => x.ReadAt);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
