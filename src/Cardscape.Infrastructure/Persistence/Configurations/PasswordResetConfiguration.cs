using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> b)
    {
        b.ToTable("password_resets");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasConversion(id => id.Value, v => new PasswordResetId(v));

        b.Property(p => p.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(p => p.UserId);

        b.Property(p => p.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(p => p.TokenHash).IsUnique();

        b.Property(p => p.IssuedAt).IsRequired();
        b.Property(p => p.ExpiresAt).IsRequired();
        b.Property(p => p.UsedAt);
        b.Property(p => p.RequestedFromIp).HasMaxLength(64);

        b.Property(p => p.CreatedAt).IsRequired();
        b.Property(p => p.UpdatedAt);
        b.Property(p => p.CreatedBy);
        b.Property(p => p.UpdatedBy);
        b.Property(p => p.IsDeleted);
        b.Property(p => p.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
