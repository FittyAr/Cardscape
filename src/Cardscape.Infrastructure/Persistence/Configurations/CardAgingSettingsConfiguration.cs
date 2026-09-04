using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardAgingSettingsConfiguration : IEntityTypeConfiguration<CardAgingSettings>
{
    public void Configure(EntityTypeBuilder<CardAgingSettings> builder)
    {
        builder.ToTable("card_aging_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        builder.Property(x => x.Mode).HasConversion<int>();
        builder.Property(x => x.StaleAfterDays);
        builder.Property(x => x.UpdatedAt);
    }
}
