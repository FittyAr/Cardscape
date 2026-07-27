using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> b)
    {
        b.ToTable("boards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardId(v));
        b.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v))
            .IsRequired();
        b.Property(x => x.Name)
            .HasConversion(n => n.Value, v => BoardName.Create(v).Value)
            .HasMaxLength(BoardName.MaxLength)
            .IsRequired();
        b.Property(x => x.Description)
            .HasConversion(d => d.Value, v => BoardDescription.Create(v).Value)
            .HasMaxLength(BoardDescription.MaxLength)
            .IsRequired();
        b.Property(x => x.Visibility).HasConversion<int>().IsRequired();
        b.Property(x => x.IsArchived).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion);
        b.HasIndex(x => x.WorkspaceId);

        b.OwnsMany(x => x.Members, mb =>
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

        b.OwnsMany(x => x.Stars, sb =>
        {
            sb.ToTable("board_stars");
            sb.HasKey(s => s.Id);
            sb.Property(s => s.BoardId).HasConversion(id => id.Value, v => new BoardId(v));
            sb.Property(s => s.UserId).IsRequired();
            sb.Property(s => s.StarredAt).IsRequired();
            sb.HasIndex(s => new { s.BoardId, s.UserId }).IsUnique();
        });
    }
}
