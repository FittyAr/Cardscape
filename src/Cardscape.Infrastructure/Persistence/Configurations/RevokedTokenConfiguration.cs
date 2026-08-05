using Cardscape.Domain.Authentication.RevokedTokens;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedToken>
{
    public void Configure(EntityTypeBuilder<RevokedToken> b)
    {
        b.ToTable("revoked_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasConversion(id => id.Value, v => new RevokedTokenId(v));

        b.Property(t => t.Jti)
            .HasMaxLength(64)
            .IsRequired();
        // Hot path: every authenticated JWT request asks
        // the repository "is this jti revoked?". The unique
        // index on Jti makes the lookup a single-row seek;
        // the non-unique secondary index on TokenExpiresAt
        // makes the sweeper's purge query a range scan.
        b.HasIndex(t => t.Jti).IsUnique();

        b.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(t => t.UserId);

        b.Property(t => t.RevokedAt).IsRequired();
        b.Property(t => t.TokenExpiresAt).IsRequired();
        b.HasIndex(t => t.TokenExpiresAt);

        b.Property(t => t.Reason).HasMaxLength(200);

        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);
        b.Property(t => t.CreatedBy);
        b.Property(t => t.UpdatedBy);
        b.Property(t => t.IsDeleted);
        b.Property(t => t.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
