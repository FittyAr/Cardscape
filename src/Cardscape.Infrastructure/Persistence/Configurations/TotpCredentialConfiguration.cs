using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    public void Configure(EntityTypeBuilder<TotpCredential> b)
    {
        b.ToTable("totp_credentials");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(id => id.Value, v => new TotpCredentialId(v));

        b.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(c => c.UserId).IsUnique();

        b.Property(c => c.EncryptedSecret)
            .HasColumnType("TEXT")
            .IsRequired();

        b.Property(c => c.RecoveryCodesHash)
            .HasColumnType("TEXT")
            .IsRequired();

        b.Property(c => c.LastUsedCounter)
            .IsRequired()
            .HasDefaultValue(0L);

        b.Property(c => c.ConfirmedAt);
        b.Ignore(c => c.IsActive);

        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.UpdatedAt);
        b.Property(c => c.CreatedBy);
        b.Property(c => c.UpdatedBy);
        b.Property(c => c.IsDeleted);
        b.Property(c => c.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
