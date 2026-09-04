using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardSnoozeConfiguration : IEntityTypeConfiguration<CardSnooze>
{
    public void Configure(EntityTypeBuilder<CardSnooze> builder)
    {
        builder.ToTable("card_snoozes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        builder.Property(x => x.Until);
        builder.Property(x => x.SnoozedBy);
        builder.Property(x => x.SnoozedAt);
    }
}
