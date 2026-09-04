using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("boards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardId(v));
        builder.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v))
            .IsRequired();
        builder.Property(x => x.Name)
            .HasConversion(n => n.Value, v => BoardName.Create(v).Value)
            .HasMaxLength(BoardName.MaxLength)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasConversion(d => d.Value, v => BoardDescription.Create(v).Value)
            .HasMaxLength(BoardDescription.MaxLength)
            .IsRequired();
        builder.Property(x => x.Visibility).HasConversion<int>().IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => x.WorkspaceId);

        builder.OwnsMany(x => x.Members, mb =>
        {
            mb.ToTable("board_members");
            mb.HasKey(m => m.Id);
            mb.Property(m => m.Id).HasConversion(id => id.Value, v => new BoardMemberId(v));
            mb.Property(m => m.BoardId).HasConversion(id => id.Value, v => new BoardId(v));
            mb.Property(m => m.UserId).IsRequired();
            mb.Property(m => m.Role).HasConversion<int>().IsRequired();
            mb.Property(m => m.JoinedAt).IsRequired();
            mb.HasIndex(m => new { m.BoardId, m.UserId }).IsUnique();
        });

        // BETA-5-#1 — see test-results/BETA-TEST-REPORT.md.
        //
        // The previous configuration declared Stars as an
        // OWNED entity collection (builder.OwnsMany(x => x.Stars, ...))
        // which made the rows addressable only through the
        // owning Board aggregate. The star toggle path in
        // BoardRepository.AddStarIfMissingAsync /
        // RemoveStarIfPresentAsync issues a direct INSERT or
        // DELETE on the board_stars table — which is the
        // correct atomicity model for the lost-update race
        // fixed in BETA-3-#3 — but EF Core refused with
        // "Cannot create a DbSet for 'BoardStar' because it
        // is configured as an owned entity type".
        //
        // The fix is to model Stars as a regular
        // one-to-many association instead of an owned
        // collection. The (BoardId, UserId) unique index is
        // preserved, so the atomicity contract from R4 is
        // intact. The Board aggregate still exposes the
        // Stars navigation property and EF Core still
        // hydrates it from the board_stars table when a
        // Board is loaded with .Include(builder => builder.Stars).
        builder.HasMany(x => x.Stars).WithOne()
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
