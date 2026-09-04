using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    public void Configure(EntityTypeBuilder<TotpCredential> builder)
    {
        builder.ToTable("totp_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => new TotpCredentialId(v));

        builder.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.Property(c => c.EncryptedSecret)
            .IsRequired();

        builder.Property(c => c.RecoveryCodesHash)
            .IsRequired();

        builder.Property(c => c.LastUsedCounter)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(c => c.ConfirmedAt);
        builder.Ignore(c => c.IsActive);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedBy);
        builder.Property(c => c.IsDeleted);
    }
}
