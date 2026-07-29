using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardMirrorConfiguration : IEntityTypeConfiguration<CardMirror>
{
    public void Configure(EntityTypeBuilder<CardMirror> b)
    {
        b.ToTable("card_mirrors");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceCardId).HasConversion(id => id.Value, v => new CardId(v));
        b.Property(x => x.MirroredCardId).HasConversion(id => id.Value, v => new CardId(v));
        b.Property(x => x.TargetListId).HasConversion(id => id.Value, v => new BoardListId(v));
        b.Property(x => x.MirroredAt);
        b.Property(x => x.MirroredBy);
        b.HasIndex(x => x.SourceCardId);
        b.HasIndex(x => x.MirroredCardId).IsUnique();
    }
}
