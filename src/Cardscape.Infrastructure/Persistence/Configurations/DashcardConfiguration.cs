using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class DashcardConfiguration : IEntityTypeConfiguration<Dashcard>
{
    public void Configure(EntityTypeBuilder<Dashcard> builder)
    {
        builder.ToTable("dashcards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new DashcardId(v));
        builder.Property(x => x.BoardId).HasConversion(id => id.Value, v => new BoardId(v));
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ConfigurationJson).HasMaxLength(8192);
        builder.Property(x => x.Position);
        builder.HasIndex(x => x.BoardId);
    }
}
