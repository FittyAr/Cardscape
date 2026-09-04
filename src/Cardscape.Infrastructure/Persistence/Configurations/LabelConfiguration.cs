using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new LabelId(v));
        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        builder.Property(x => x.Name)
            .HasConversion(n => n.Value, v => LabelName.Create(v).Value)
            .HasMaxLength(LabelName.MaxLength)
            .IsRequired();
        builder.Property(x => x.Color)
            .HasConversion(c => c.Value, v => Color.Create(v).Value)
            .HasMaxLength(7)
            .IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => new { x.BoardId, x.IsDeleted, x.Name });
    }
}
