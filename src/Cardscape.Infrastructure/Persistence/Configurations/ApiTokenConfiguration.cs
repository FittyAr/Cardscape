using Cardscape.Domain.Members;
using Cardscape.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> b)
    {
        b.ToTable("api_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasConversion(id => id.Value, v => new ApiTokenId(v));

        b.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(t => new { t.UserId, t.CreatedAt });

        b.Property(t => t.Name)
            .HasConversion(n => n.Value, v => ApiTokenName.Create(v).Value)
            .HasMaxLength(ApiTokenName.MaxLength)
            .IsRequired();

        b.Property(t => t.HashedSecret)
            .HasMaxLength(64)
            .IsRequired();
        b.HasIndex(t => t.HashedSecret).IsUnique();

        b.Property(t => t.SecretPrefix)
            .HasMaxLength(ApiToken.SecretPrefixLength)
            .IsRequired();

        b.Property(t => t.Scopes)
            .HasConversion(
                s => s.ToString(),
                v => ApiTokenScopes.Create(v.Split(';', StringSplitOptions.RemoveEmptyEntries)).Value)
            .IsRequired();

        b.Property(t => t.ExpiresAt);
        b.Property(t => t.LastUsedAt);

        b.Property(t => t.RevokedAt);
        b.Property(t => t.RevokedBy);
        b.Property(t => t.RevokedReason).HasMaxLength(200);

        b.Property(t => t.RateLimitPerHour)
            .IsRequired()
            .HasDefaultValue(ApiToken.DefaultRateLimitPerHour);
        b.Property(t => t.BurstSize)
            .IsRequired()
            .HasDefaultValue(ApiToken.DefaultBurstSize);

        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);
        b.Property(t => t.CreatedBy);
        b.Property(t => t.UpdatedBy);
        b.Property(t => t.IsDeleted);
        b.Property(t => t.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
