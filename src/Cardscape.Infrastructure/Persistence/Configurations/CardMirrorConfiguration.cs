using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardMirrorConfiguration : IEntityTypeConfiguration<CardMirror>
{
    public void Configure(EntityTypeBuilder<CardMirror> builder)
    {
        builder.ToTable("card_mirrors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceCardId).HasConversion(id => id.Value, v => new CardId(v));
        builder.Property(x => x.MirroredCardId).HasConversion(id => id.Value, v => new CardId(v));
        builder.Property(x => x.TargetListId).HasConversion(id => id.Value, v => new BoardListId(v));
        builder.Property(x => x.MirroredAt);
        builder.Property(x => x.MirroredBy);
        builder.HasIndex(x => x.SourceCardId);
        builder.HasIndex(x => x.MirroredCardId).IsUnique();
    }
}
