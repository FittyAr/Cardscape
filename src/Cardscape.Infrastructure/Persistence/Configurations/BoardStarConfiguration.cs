using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

/// <summary>
/// BETA-5-#1 — see test-results/BETA-TEST-REPORT.md.
///
/// Standalone configuration for <see cref="BoardStar"/> so the
/// table name stays <c>board_stars</c> (snake_case, matching the
/// rest of the schema) instead of the EF Core default
/// <c>BoardStars</c>. The (BoardId, UserId) unique index is the
/// safety net for the BETA-3-#3 atomic star toggle path — a
/// second INSERT for the same (board, user) pair is rejected
/// at the SQLite level regardless of which process got there
/// first.
/// </summary>
public sealed class BoardStarConfiguration : IEntityTypeConfiguration<BoardStar>
{
    public void Configure(EntityTypeBuilder<BoardStar> builder)
    {
        builder.ToTable("board_stars");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.BoardId).HasConversion(id => id.Value, v => new BoardId(v));
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.StarredAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.UpdatedBy);
        builder.Property(s => s.IsDeleted);
        builder.HasIndex(s => new { s.BoardId, s.UserId }).IsUnique();
    }
}
