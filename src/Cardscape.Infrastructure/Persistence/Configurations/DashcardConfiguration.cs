using Cardscape.Domain.Dashboards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class DashcardConfiguration : IEntityTypeConfiguration<Dashcard>
{
    public void Configure(EntityTypeBuilder<Dashcard> builder)
    {
        builder.ToTable("dashcards");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, v => new DashcardId(v))
            .HasColumnType("TEXT");
        builder.Property(d => d.BoardId)
            .HasConversion(id => id.Value, v => new Cardscape.Domain.Boards.BoardId(v))
            .HasColumnType("TEXT");
        builder.Property(d => d.Kind).HasConversion<int>().HasColumnType("INTEGER");
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.ConfigurationJson).HasColumnType("TEXT");
        builder.Property(d => d.Position).HasColumnType("INTEGER");
        builder.Property(d => d.CreatedAt).HasColumnType("TEXT");
        builder.Property(d => d.UpdatedAt).HasColumnType("TEXT");
        builder.Property(d => d.CreatedBy).HasColumnType("TEXT");
        builder.Property(d => d.UpdatedBy).HasColumnType("TEXT");
        builder.Property(d => d.RowVersion).HasColumnType("INTEGER").IsConcurrencyToken().HasDefaultValue(0u);
        builder.Property(d => d.IsDeleted).HasColumnType("INTEGER");

        builder.HasIndex(d => d.BoardId);
    }
}
