using Cardscape.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new ActivityId(v));
        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        builder.Property(x => x.CardId);
        builder.Property(x => x.ActorId).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => new { x.BoardId, x.OccurredAt, x.Id });
        builder.HasIndex(x => new { x.CardId, x.OccurredAt, x.Id });
        builder.HasIndex(x => new { x.ActorId, x.OccurredAt });
    }
}
