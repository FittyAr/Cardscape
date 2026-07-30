using Cardscape.Domain.Boards;
using Cardscape.Domain.Dashboards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class DashcardConfiguration : IEntityTypeConfiguration<Dashcard>
{
    public void Configure(EntityTypeBuilder<Dashcard> b)
    {
        b.ToTable("dashcards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new DashcardId(v));
        b.Property(x => x.BoardId).HasConversion(id => id.Value, v => new BoardId(v));
        b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.ConfigurationJson).HasMaxLength(8192);
        b.Property(x => x.Position);
        b.HasIndex(x => x.BoardId);
    }
}
