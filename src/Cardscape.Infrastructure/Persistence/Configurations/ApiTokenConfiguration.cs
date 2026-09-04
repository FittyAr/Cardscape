using Cardscape.Domain.Members;
using Cardscape.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("api_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => new ApiTokenId(v));

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });

        builder.Property(t => t.Name)
            .HasConversion(n => n.Value, v => ApiTokenName.Create(v).Value)
            .HasMaxLength(ApiTokenName.MaxLength)
            .IsRequired();

        builder.Property(t => t.HashedSecret)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(t => t.HashedSecret).IsUnique();

        builder.Property(t => t.SecretPrefix)
            .HasMaxLength(ApiToken.SecretPrefixLength)
            .IsRequired();

        builder.Property(t => t.Scopes)
            .HasConversion(
                s => s.ToString(),
                v => ApiTokenScopes.Create(v.Split(';', StringSplitOptions.RemoveEmptyEntries)).Value)
            .IsRequired();

        builder.Property(t => t.ExpiresAt);
        builder.Property(t => t.LastUsedAt);

        builder.Property(t => t.RevokedAt);
        builder.Property(t => t.RevokedBy);
        builder.Property(t => t.RevokedReason).HasMaxLength(200);

        builder.Property(t => t.RateLimitPerHour)
            .IsRequired()
            .HasDefaultValue(ApiToken.DefaultRateLimitPerHour);
        builder.Property(t => t.BurstSize)
            .IsRequired()
            .HasDefaultValue(ApiToken.DefaultBurstSize);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
        builder.Property(t => t.CreatedBy);
        builder.Property(t => t.UpdatedBy);
        builder.Property(t => t.IsDeleted);
    }
}
