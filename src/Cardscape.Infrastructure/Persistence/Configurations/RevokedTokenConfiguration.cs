using Cardscape.Domain.Authentication.RevokedTokens;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedToken>
{
    public void Configure(EntityTypeBuilder<RevokedToken> builder)
    {
        builder.ToTable("revoked_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => new RevokedTokenId(v));

        builder.Property(t => t.Jti)
            .HasMaxLength(64)
            .IsRequired();
        // Hot path: every authenticated JWT request asks
        // the repository "is this jti revoked?". The unique
        // index on Jti makes the lookup a single-row seek;
        // the non-unique secondary index on TokenExpiresAt
        // makes the sweeper's purge query a range scan.
        builder.HasIndex(t => t.Jti).IsUnique();

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(t => t.UserId);

        builder.Property(t => t.RevokedAt).IsRequired();
        builder.Property(t => t.TokenExpiresAt).IsRequired();
        builder.HasIndex(t => t.TokenExpiresAt);

        builder.Property(t => t.Reason).HasMaxLength(200);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
        builder.Property(t => t.CreatedBy);
        builder.Property(t => t.UpdatedBy);
        builder.Property(t => t.IsDeleted);
    }
}
