using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> builder)
    {
        builder.ToTable("password_resets");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => new PasswordResetId(v));

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(p => p.UserId);

        builder.Property(p => p.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(p => p.TokenHash).IsUnique();

        builder.Property(p => p.IssuedAt).IsRequired();
        builder.Property(p => p.ExpiresAt).IsRequired();
        builder.Property(p => p.UsedAt);
        builder.Property(p => p.RequestedFromIp).HasMaxLength(64);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.CreatedBy);
        builder.Property(p => p.UpdatedBy);
        builder.Property(p => p.IsDeleted);
    }
}
