using Cardscape.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

internal sealed class DomainEventOutboxMessageConfiguration
    : IEntityTypeConfiguration<DomainEventOutboxMessage>
{
    public void Configure(EntityTypeBuilder<DomainEventOutboxMessage> builder)
    {
        builder.ToTable("domain_event_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(512).IsRequired();
        builder.Property(x => x.BroadcasterType).HasMaxLength(512).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2048);
        builder.Ignore(x => x.OccurredAt);
        builder.Ignore(x => x.CreatedAt);
        builder.Ignore(x => x.NextAttemptAt);
        builder.Ignore(x => x.LockedUntil);
        builder.Ignore(x => x.ProcessedAt);
        builder.HasIndex(x => new
        {
            x.ProcessedAtUtcTicks,
            x.NextAttemptAtUtcTicks,
            x.LockedUntilUtcTicks,
            x.CreatedAtUtcTicks
        });
    }
}
