using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardSnoozeConfiguration : IEntityTypeConfiguration<CardSnooze>
{
    public void Configure(EntityTypeBuilder<CardSnooze> b)
    {
        b.ToTable("card_snoozes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        b.Property(x => x.Until);
        b.Property(x => x.SnoozedBy);
        b.Property(x => x.SnoozedAt);
    }
}
