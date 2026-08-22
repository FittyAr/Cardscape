using Cardscape.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new WebhookDeliveryId(v));

        b.Property(x => x.EndpointId)
            .HasConversion(id => id.Value, v => new WebhookEndpointId(v))
            .IsRequired();
        b.HasIndex(x => x.EndpointId);

        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.EventType);

        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.AttemptCount).IsRequired();
        b.Property(x => x.LastAttemptAt);
        b.Property(x => x.LastError);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        // (EndpointId, CreatedAt) makes the per-endpoint list
        // query (newest first) cheap on the hot path the Web UI
        // hits on every poll.
        b.HasIndex(x => new { x.EndpointId, x.CreatedAt })
            .HasDatabaseName("IX_webhook_deliveries_EndpointId_CreatedAt");
    }
}
