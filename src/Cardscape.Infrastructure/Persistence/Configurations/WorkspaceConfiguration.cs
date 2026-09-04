using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new WorkspaceId(v));
        builder.Property(x => x.Name)
            .HasConversion(n => n.Value, v => WorkspaceName.Create(v).Value)
            .HasMaxLength(WorkspaceName.MaxLength)
            .IsRequired();
        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.Region).HasConversion<int>().IsRequired();
        builder.HasIndex(x => x.Region);
        builder.Property(x => x.RequireTwoFactor).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.OwnsMany(x => x.Members, mb =>
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
