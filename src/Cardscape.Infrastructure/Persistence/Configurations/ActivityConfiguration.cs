using Cardscape.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("activities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new ActivityId(v));
        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new Domain.Boards.BoardId(v))
            .IsRequired();
        b.Property(x => x.CardId);
        b.Property(x => x.ActorId).IsRequired();
        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        b.Property(x => x.OccurredAt).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion);
        b.HasIndex(x => new { x.BoardId, x.OccurredAt });
    }
}
