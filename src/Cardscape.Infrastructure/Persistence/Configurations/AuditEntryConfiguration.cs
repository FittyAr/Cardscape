using Cardscape.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

/// <summary>
/// The append-only administration audit log. The timestamp is stored as
/// UTC ticks so SQLite can order and range-filter it in SQL like every
/// other provider; no foreign keys, because entries must outlive the
/// users, workspaces and boards they mention.
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.OccurredAt);
        builder.Property(x => x.ActorName).HasMaxLength(AuditEntry.NameMaxLength).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(AuditEntry.ActionMaxLength).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(AuditEntry.TargetTypeMaxLength).IsRequired();
        builder.Property(x => x.TargetName).HasMaxLength(AuditEntry.NameMaxLength).IsRequired();
        builder.Property(x => x.WorkspaceName).HasMaxLength(AuditEntry.NameMaxLength);
        builder.Property(x => x.BoardName).HasMaxLength(AuditEntry.NameMaxLength);
        builder.Property(x => x.Details).HasMaxLength(AuditEntry.DetailsMaxLength);

        builder.HasIndex(x => x.OccurredAtUtcTicks);
        builder.HasIndex(x => new { x.WorkspaceId, x.OccurredAtUtcTicks });
        builder.HasIndex(x => x.TargetId);
        builder.HasIndex(x => x.ActorUserId);
    }
}
