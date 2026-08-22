using Cardscape.Domain.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> b)
    {
        b.ToTable("background_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BackgroundJobId(v));

        b.Property(x => x.Type).HasMaxLength(200).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.ScheduledFor).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Attempts).IsRequired();
        b.Property(x => x.MaxAttempts).IsRequired();
        b.Property(x => x.StartedAt);
        b.Property(x => x.CompletedAt);
        b.Property(x => x.LastError);

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        // The dispatcher's hot-path query is "pending + due"; this
        // composite index makes that O(log n) instead of a full scan.
        b.HasIndex(x => new { x.Status, x.ScheduledFor })
            .HasDatabaseName("IX_background_jobs_Status_ScheduledFor");
        b.HasIndex(x => new { x.Status, x.CompletedAt })
            .HasDatabaseName("IX_background_jobs_Status_CompletedAt");
    }
}
