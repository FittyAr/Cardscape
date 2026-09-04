using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion(id => id.Value, v => new WorkspaceInvitationId(v));

        builder.Property(i => i.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        builder.HasIndex(i => new { i.WorkspaceId, i.InvitedAt });

        builder.Property(i => i.Email)
            .HasMaxLength(320) // RFC 5321 maximum email length
            .IsRequired();
        builder.HasIndex(i => new { i.Email, i.AcceptedAt, i.RevokedAt, i.InvitedAt });

        builder.Property(i => i.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.InvitedBy).IsRequired();

        builder.Property(i => i.InvitedAt).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();

        builder.Property(i => i.TokenHash)
            .HasMaxLength(64) // SHA-256 hex
            .IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.Property(i => i.TokenPrefix)
            .HasMaxLength(InvitationToken.PrefixLength)
            .IsRequired();

        builder.Property(i => i.AcceptedAt);
        builder.Property(i => i.AcceptedBy);
        builder.Property(i => i.RevokedAt);
        builder.Property(i => i.RevokedBy);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt);
        builder.Property(i => i.CreatedBy);
        builder.Property(i => i.UpdatedBy);
        builder.Property(i => i.IsDeleted);
    }
}
