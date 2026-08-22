using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> b)
    {
        b.ToTable("workspace_invitations");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasConversion(id => id.Value, v => new WorkspaceInvitationId(v));

        b.Property(i => i.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        b.HasIndex(i => new { i.WorkspaceId, i.InvitedAt });

        b.Property(i => i.Email)
            .HasMaxLength(320) // RFC 5321 maximum email length
            .IsRequired();
        b.HasIndex(i => new { i.Email, i.AcceptedAt, i.RevokedAt, i.InvitedAt });

        b.Property(i => i.Role)
            .HasConversion<int>()
            .IsRequired();

        b.Property(i => i.InvitedBy).IsRequired();

        b.Property(i => i.InvitedAt).IsRequired();
        b.Property(i => i.ExpiresAt).IsRequired();

        b.Property(i => i.TokenHash)
            .HasMaxLength(64) // SHA-256 hex
            .IsRequired();
        b.HasIndex(i => i.TokenHash).IsUnique();

        b.Property(i => i.TokenPrefix)
            .HasMaxLength(InvitationToken.PrefixLength)
            .IsRequired();

        b.Property(i => i.AcceptedAt);
        b.Property(i => i.AcceptedBy);
        b.Property(i => i.RevokedAt);
        b.Property(i => i.RevokedBy);

        b.Property(i => i.CreatedAt).IsRequired();
        b.Property(i => i.UpdatedAt);
        b.Property(i => i.CreatedBy);
        b.Property(i => i.UpdatedBy);
        b.Property(i => i.IsDeleted);
        b.Property(i => i.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
