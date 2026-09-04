using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardRecurrenceConfiguration : IEntityTypeConfiguration<CardRecurrence>
{
    public void Configure(EntityTypeBuilder<CardRecurrence> builder)
    {
        builder.ToTable("card_recurrences");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new CardRecurrenceId(v));

        builder.Property(r => r.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        builder.HasIndex(r => r.CardId).IsUnique();

        builder.Property(r => r.IntervalDays).IsRequired();
        builder.Property(r => r.NextOccurrenceAt).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
    }
}
