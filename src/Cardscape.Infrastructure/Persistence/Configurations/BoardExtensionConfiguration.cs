using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardExtensionConfiguration : IEntityTypeConfiguration<BoardExtension>
{
    public void Configure(EntityTypeBuilder<BoardExtension> b)
    {
        b.ToTable("board_extensions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardExtensionId(v));

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();

        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.ConfigJson);
        b.Property(x => x.IsEnabled).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);

        // (BoardId, Kind) is unique — one row per extension per board.
        b.HasIndex(x => new { x.BoardId, x.Kind }).IsUnique();
    }
}
