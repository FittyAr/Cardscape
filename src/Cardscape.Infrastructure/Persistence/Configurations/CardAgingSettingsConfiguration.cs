using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardAgingSettingsConfiguration : IEntityTypeConfiguration<CardAgingSettings>
{
    public void Configure(EntityTypeBuilder<CardAgingSettings> b)
    {
        b.ToTable("card_aging_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        b.Property(x => x.Mode).HasConversion<int>();
        b.Property(x => x.StaleAfterDays);
        b.Property(x => x.UpdatedAt);
    }
}
