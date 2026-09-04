using Cardscape.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new WebhookDeliveryId(v));

        builder.Property(x => x.EndpointId)
            .HasConversion(id => id.Value, v => new WebhookEndpointId(v))
            .IsRequired();
        builder.HasIndex(x => x.EndpointId);

        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.EventType);

        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.LastAttemptAt);
        builder.Property(x => x.LastError);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        // (EndpointId, CreatedAt) makes the per-endpoint list
        // query (newest first) cheap on the hot path the Web UI
        // hits on every poll.
        builder.HasIndex(x => new { x.EndpointId, x.CreatedAt })
            .HasDatabaseName("IX_webhook_deliveries_EndpointId_CreatedAt");
    }
}
