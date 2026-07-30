using Cardscape.Domain.Integrations.GoogleDrive;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class GoogleDriveConnectionConfiguration : IEntityTypeConfiguration<GoogleDriveConnection>
{
    public void Configure(EntityTypeBuilder<GoogleDriveConnection> b)
    {
        b.ToTable("google_drive_connections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new GoogleDriveConnectionId(v));

        b.Property(x => x.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(x => x.UserId).IsUnique();

        b.Property(x => x.GoogleEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.EncryptedRefreshToken).HasColumnType("TEXT").IsRequired();
        b.Property(x => x.LastUsedAt);
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
