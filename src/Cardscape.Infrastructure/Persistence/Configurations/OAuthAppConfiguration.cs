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
    public void Configure(EntityTypeBuilder<OAuthApp> b)
    {
        b.ToTable("oauth_apps");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(id => id.Value, v => new OAuthAppId(v));

        b.Property(a => a.Name)
            .HasMaxLength(200)
            .IsRequired();
        b.HasIndex(a => a.Name);

        b.Property(a => a.ClientId)
            .HasMaxLength(64)
            .IsRequired();
        b.HasIndex(a => a.ClientId).IsUnique();

        b.Property(a => a.ClientSecretHash)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(a => a.OwnerId)
            .HasConversion(id => id, v => v)
            .IsRequired();
        b.HasIndex(a => a.OwnerId);

        b.Property(a => a.AllowedScopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        b.Property(a => a.AllowedScopes).IsRequired();

        b.Property(a => a.RedirectUris)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        b.Property(a => a.RedirectUris).IsRequired();

        b.Property(a => a.IsRevoked).IsRequired();

        b.Property(a => a.CreatedAt).IsRequired();
        b.Property(a => a.UpdatedAt);
    }
}

public sealed class OAuthAuthorizationCodeConfiguration : IEntityTypeConfiguration<OAuthAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<OAuthAuthorizationCode> b)
    {
        b.ToTable("oauth_authorization_codes");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(id => id.Value, v => new OAuthAuthorizationCodeId(v));

        b.Property(c => c.AppId)
            .HasConversion(id => id.Value, v => new OAuthAppId(v))
            .IsRequired();
        b.HasIndex(c => c.AppId);

        b.Property(c => c.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(c => c.UserId);

        b.Property(c => c.RedirectUri)
            .HasMaxLength(2000)
            .IsRequired();

        b.Property(c => c.CodeHash)
            .HasMaxLength(64)
            .IsRequired();
        b.HasIndex(c => c.CodeHash).IsUnique();

        b.Property(c => c.Scopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        b.Property(c => c.Scopes).IsRequired();

        b.Property(c => c.ExpiresAt).IsRequired();
        b.Property(c => c.IsConsumed).IsRequired();

        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.UpdatedAt);
    }
}

public sealed class OAuthAccessTokenConfiguration : IEntityTypeConfiguration<OAuthAccessToken>
{
    public void Configure(EntityTypeBuilder<OAuthAccessToken> b)
    {
        b.ToTable("oauth_access_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasConversion(id => id.Value, v => new OAuthAccessTokenId(v));

        b.Property(t => t.AppId)
            .HasConversion(id => id.Value, v => new OAuthAppId(v))
            .IsRequired();
        b.HasIndex(t => t.AppId);

        b.Property(t => t.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(t => new { t.UserId, t.CreatedAt });

        b.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique();

        b.Property(t => t.Scopes)
            .HasConversion(
                s => string.Join(';', s),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(StringArrayValueComparer.Instance);
        b.Property(t => t.Scopes).IsRequired();

        b.Property(t => t.ExpiresAt).IsRequired();
        b.HasIndex(t => t.ExpiresAt);

        b.Property(t => t.RefreshedAt);
        b.Property(t => t.RevokedAt);
        b.HasIndex(t => t.RevokedAt);

        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);
    }
}
