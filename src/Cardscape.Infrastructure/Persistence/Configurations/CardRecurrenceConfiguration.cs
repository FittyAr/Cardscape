using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardRecurrenceConfiguration : IEntityTypeConfiguration<CardRecurrence>
{
    public void Configure(EntityTypeBuilder<CardRecurrence> b)
    {
        b.ToTable("card_recurrences");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasConversion(id => id.Value, v => new CardRecurrenceId(v));

        b.Property(r => r.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        b.HasIndex(r => r.CardId).IsUnique();

        b.Property(r => r.IntervalDays).IsRequired();
        b.Property(r => r.NextOccurrenceAt).IsRequired();
        b.Property(r => r.IsActive).IsRequired();
        b.Property(r => r.CreatedBy).IsRequired();
        b.Property(r => r.CreatedAt).IsRequired();
        b.Property(r => r.UpdatedAt);
    }
}
