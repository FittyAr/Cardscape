using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> b)
    {
        b.ToTable("workspaces");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new WorkspaceId(v));
        b.Property(x => x.Name)
            .HasConversion(n => n.Value, v => WorkspaceName.Create(v).Value)
            .HasMaxLength(WorkspaceName.MaxLength)
            .IsRequired();
        b.Property(x => x.OwnerId).IsRequired();
        b.Property(x => x.IsArchived).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.OwnsMany(x => x.Members, mb =>
        {
            mb.ToTable("workspace_members");
            mb.HasKey(m => m.Id);
            mb.Property(m => m.Id).HasConversion(id => id.Value, v => new WorkspaceMemberId(v));
            mb.Property(m => m.WorkspaceId).HasConversion(id => id.Value, v => new WorkspaceId(v));
            mb.Property(m => m.UserId).IsRequired();
            mb.Property(m => m.Role).HasConversion<int>().IsRequired();
            mb.Property(m => m.JoinedAt).IsRequired();
            mb.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
        });
    }
}
