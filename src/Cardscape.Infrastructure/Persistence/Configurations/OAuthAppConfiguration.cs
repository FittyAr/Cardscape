using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared element-by-element value comparer for <c>IReadOnlyList&lt;string&gt;</c>
/// properties stored as delimited text. EF Core raises a
/// <c>CollectionWithoutComparer</c> warning whenever a property has
/// a value converter but no comparer, because reference equality
/// on collections is the default and silently breaks change
/// detection — see BUG #15 in
/// <c>test-results/BETA-TEST-REPORT.md</c>.
/// </summary>
internal static class StringArrayValueComparer
{
    public static readonly ValueComparer<IReadOnlyList<string>> Instance = new(
        (left, right) => left!.SequenceEqual(right!),
        values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
        values => values.ToArray());
}

public sealed class OAuthAppConfiguration : IEntityTypeConfiguration<OAuthApp>
{
    public void Configure(EntityTypeBuilder<OAuthApp> builder)
    {
        builder.ToTable("oauth_apps");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => new OAuthAppId(v));

        builder.Property(a => a.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(a => a.Name);

        builder.Property(a => a.ClientId)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(a => a.ClientId).IsUnique();

        builder.Property(a => a.ClientSecretHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.OwnerId)
            .HasConversion(id => id, v => v)
            .IsRequired();
        builder.HasIndex(a => a.OwnerId);

        builder.Property(a => a.AllowedScopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        builder.Property(a => a.AllowedScopes).IsRequired();

        builder.Property(a => a.RedirectUris)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        builder.Property(a => a.RedirectUris).IsRequired();

        builder.Property(a => a.IsRevoked).IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
    }
}

public sealed class OAuthAuthorizationCodeConfiguration : IEntityTypeConfiguration<OAuthAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<OAuthAuthorizationCode> builder)
    {
        builder.ToTable("oauth_authorization_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(id => id.Value, v => new OAuthAuthorizationCodeId(v));

        builder.Property(c => c.AppId)
            .HasConversion(id => id.Value, v => new OAuthAppId(v))
            .IsRequired();
        builder.HasIndex(c => c.AppId);

        builder.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(c => c.UserId);

        builder.Property(c => c.RedirectUri)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.CodeHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(c => c.CodeHash).IsUnique();

        builder.Property(c => c.Scopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        builder.Property(c => c.Scopes).IsRequired();

        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.IsConsumed).IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
    }
}

public sealed class OAuthAccessTokenConfiguration : IEntityTypeConfiguration<OAuthAccessToken>
{
    public void Configure(EntityTypeBuilder<OAuthAccessToken> builder)
    {
        builder.ToTable("oauth_access_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => new OAuthAccessTokenId(v));

        builder.Property(t => t.AppId)
            .HasConversion(id => id.Value, v => new OAuthAppId(v))
            .IsRequired();
        builder.HasIndex(t => t.AppId);

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });

        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.Scopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        builder.Property(t => t.Scopes).IsRequired();

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.HasIndex(t => t.ExpiresAt);

        builder.Property(t => t.RefreshedAt);
        builder.Property(t => t.RevokedAt);
        builder.HasIndex(t => t.RevokedAt);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
    }
}
