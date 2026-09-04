using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardListConfiguration : IEntityTypeConfiguration<BoardList>
{
    public void Configure(EntityTypeBuilder<BoardList> builder)
    {
        builder.ToTable("lists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardListId(v));
        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        builder.Property(x => x.Name)
            .HasConversion(n => n.Value, v => ListName.Create(v).Value)
            .HasMaxLength(ListName.MaxLength)
            .IsRequired();
        builder.Property(x => x.Position).HasConversion(p => p.Value, v => Position.From(v)).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => x.BoardId);
        builder.HasIndex(x => new { x.BoardId, x.Position });
    }
}
