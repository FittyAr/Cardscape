using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardExtensionConfiguration : IEntityTypeConfiguration<BoardExtension>
{
    public void Configure(EntityTypeBuilder<BoardExtension> builder)
    {
        builder.ToTable("board_extensions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardExtensionId(v));

        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();

        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.ConfigJson);
        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        // (BoardId, Kind) is unique — one row per extension per board.
        builder.HasIndex(x => new { x.BoardId, x.Kind }).IsUnique();
    }
}
