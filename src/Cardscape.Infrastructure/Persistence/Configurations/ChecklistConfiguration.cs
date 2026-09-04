using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ChecklistConfiguration : IEntityTypeConfiguration<Checklist>
{
    public void Configure(EntityTypeBuilder<Checklist> builder)
    {
        builder.ToTable("checklists");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => new ChecklistId(v));

        builder.Property(c => c.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        builder.HasIndex(c => new { c.CardId, c.IsDeleted, c.CreatedAt });

        builder.Property(c => c.Title)
            .HasConversion(t => t.Value, v => ChecklistTitle.Create(v).Value)
            .HasMaxLength(ChecklistTitle.MaxLength)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedBy);
        builder.Property(c => c.IsDeleted);

        // Items are an owned collection — EF stores them in a
        // separate table (checklist_items) and reloads them as
        // part of the Checklist aggregate. This means the
        // domain's `Checklist.Items` collection always reflects
        // what's persisted, without an extra round-trip.
        builder.OwnsMany(c => c.Items, ib =>
        {
            ib.ToTable("checklist_items");
            ib.HasKey(i => i.Id);
            ib.Property(i => i.Id).HasConversion(id => id.Value, v => new ChecklistItemId(v));
            ib.Property(i => i.ChecklistId)
                .HasConversion(id => id.Value, v => new ChecklistId(v))
                .IsRequired();
            ib.Property(i => i.Text)
                .HasConversion(t => t.Value, v => ChecklistItemText.Create(v).Value)
                .HasMaxLength(ChecklistItemText.MaxLength)
                .IsRequired();
            ib.Property(i => i.IsCompleted).IsRequired();
            ib.Property(i => i.Position)
                .HasConversion(p => p.Value, v => Position.From(v))
                .IsRequired();
            ib.Property(i => i.AssignedTo);
            ib.Property(i => i.CreatedAt).IsRequired();
            ib.Property(i => i.UpdatedAt);
            ib.Property(i => i.CreatedBy);
            ib.Property(i => i.UpdatedBy);
            ib.Property(i => i.IsDeleted);
            ib.HasIndex(i => new { i.ChecklistId, i.Position });
        });
    }
}
