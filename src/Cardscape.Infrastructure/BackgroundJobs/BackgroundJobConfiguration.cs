using Cardscape.Domain.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("background_jobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new BackgroundJobId(v));

        builder.Property(x => x.Type).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.ScheduledFor).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.MaxAttempts).IsRequired();
        builder.Property(x => x.StartedAt);
        builder.Property(x => x.CompletedAt);
        builder.Property(x => x.LastError);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        // The dispatcher's hot-path query is "pending + due"; this
        // composite index makes that O(log n) instead of a full scan.
        builder.HasIndex(x => new { x.Status, x.ScheduledFor })
            .HasDatabaseName("IX_background_jobs_Status_ScheduledFor");
        builder.HasIndex(x => new { x.Status, x.CompletedAt })
            .HasDatabaseName("IX_background_jobs_Status_CompletedAt");
    }
}
