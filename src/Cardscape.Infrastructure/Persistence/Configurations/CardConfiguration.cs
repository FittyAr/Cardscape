using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> b)
    {
        b.ToTable("cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        b.Property(x => x.ListId)
            .HasConversion(id => id.Value, v => new Domain.Lists.BoardListId(v))
            .IsRequired();
        b.Property(x => x.Title)
            .HasConversion(t => t.Value, v => CardTitle.Create(v).Value)
            .HasMaxLength(CardTitle.MaxLength)
            .IsRequired();
        b.Property(x => x.Description)
            .HasConversion(d => d.Value, v => CardDescription.Create(v).Value)
            .IsRequired();
        b.Property(x => x.Position).HasConversion(p => p.Value, v => Position.From(v)).IsRequired();
        b.Property(x => x.DueDate);
        b.Property(x => x.IsArchived).IsRequired();
        b.Property(x => x.IsCompleted).IsRequired();
        b.Property(x => x.CoverColor)
            .HasConversion(
                c => c == null ? null : c.Value,
                v => v == null ? null : Color.Create(v).Value)
            .HasMaxLength(7);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
        b.HasIndex(x => x.ListId);
        b.HasIndex(x => new { x.ListId, x.Position });

        b.OwnsMany(x => x.Members, mb =>
        {
            mb.ToTable("card_members");
            mb.HasKey(m => m.Id);
            mb.Property(m => m.Id).HasConversion(id => id.Value, v => new CardMemberId(v));
            mb.Property(m => m.CardId).HasConversion(id => id.Value, v => new CardId(v));
            mb.Property(m => m.UserId).IsRequired();
            mb.Property(m => m.AssignedAt).IsRequired();
            mb.Property(m => m.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
            mb.HasIndex(m => new { m.CardId, m.UserId }).IsUnique();
        });

        b.OwnsMany(x => x.CardLabels, lb =>
        {
            lb.ToTable("card_labels");
            lb.HasKey(cl => cl.Id);
            lb.Property(cl => cl.Id).HasConversion(id => id.Value, v => new Domain.Labels.CardLabelId(v));
            lb.Property(cl => cl.CardId).HasConversion(id => id.Value, v => new CardId(v));
            lb.Property(cl => cl.LabelId).HasConversion(id => id.Value, v => new Domain.Labels.LabelId(v));
            lb.Property(cl => cl.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
            lb.HasIndex(cl => new { cl.CardId, cl.LabelId }).IsUnique();
        });
    }
}
