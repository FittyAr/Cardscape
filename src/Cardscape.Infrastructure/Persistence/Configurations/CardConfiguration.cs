using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CardId(v));
        builder.Property(x => x.ListId)
            .HasConversion(id => id.Value, v => new Domain.Lists.BoardListId(v))
            .IsRequired();
        builder.Property(x => x.Title)
            .HasConversion(t => t.Value, v => CardTitle.Create(v).Value)
            .HasMaxLength(CardTitle.MaxLength)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasConversion(d => d.Value, v => CardDescription.Create(v).Value)
            .IsRequired();
        builder.Property(x => x.Position).HasConversion(p => p.Value, v => Position.From(v)).IsRequired();
        builder.Property(x => x.DueDate);
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.IsCompleted).IsRequired();
        builder.Property(x => x.CoverColor)
            .HasConversion(
                c => c == null ? null : c.Value,
                v => v == null ? null : Color.Create(v).Value)
            .HasMaxLength(7);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => x.ListId);
        builder.HasIndex(x => new { x.ListId, x.Position });

        builder.OwnsMany(x => x.Members, mb =>
        {
            mb.ToTable("card_members");
            mb.HasKey(m => m.Id);
            mb.Property(m => m.Id).HasConversion(id => id.Value, v => new CardMemberId(v));
            mb.Property(m => m.CardId).HasConversion(id => id.Value, v => new CardId(v));
            mb.Property(m => m.UserId).IsRequired();
            mb.Property(m => m.AssignedAt).IsRequired();
            mb.HasIndex(m => new { m.CardId, m.UserId }).IsUnique();
        });

        builder.OwnsMany(x => x.CardLabels, lb =>
        {
            lb.ToTable("card_labels");
            lb.HasKey(cl => cl.Id);
            lb.Property(cl => cl.Id).HasConversion(id => id.Value, v => new Domain.Labels.CardLabelId(v));
            lb.Property(cl => cl.CardId).HasConversion(id => id.Value, v => new CardId(v));
            lb.Property(cl => cl.LabelId).HasConversion(id => id.Value, v => new Domain.Labels.LabelId(v));
            lb.HasIndex(cl => new { cl.CardId, cl.LabelId }).IsUnique();
        });
    }
}
