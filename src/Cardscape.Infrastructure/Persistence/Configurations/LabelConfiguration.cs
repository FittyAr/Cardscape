using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> b)
    {
        b.ToTable("labels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new LabelId(v));
        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        b.Property(x => x.Name)
            .HasConversion(n => n.Value, v => LabelName.Create(v).Value)
            .HasMaxLength(LabelName.MaxLength)
            .IsRequired();
        b.Property(x => x.Color)
            .HasConversion(c => c.Value, v => Color.Create(v).Value)
            .HasMaxLength(7)
            .IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.BoardId);
    }
}
