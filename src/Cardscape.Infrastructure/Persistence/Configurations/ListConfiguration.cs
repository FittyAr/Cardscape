using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardListConfiguration : IEntityTypeConfiguration<BoardList>
{
    public void Configure(EntityTypeBuilder<BoardList> b)
    {
        b.ToTable("lists");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardListId(v));
        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        b.Property(x => x.Name)
            .HasConversion(n => n.Value, v => ListName.Create(v).Value)
            .HasMaxLength(ListName.MaxLength)
            .IsRequired();
        b.Property(x => x.Position).HasConversion(p => p.Value, v => Position.From(v)).IsRequired();
        b.Property(x => x.IsArchived).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.HasIndex(x => x.BoardId);
        b.HasIndex(x => new { x.BoardId, x.Position });
    }
}
