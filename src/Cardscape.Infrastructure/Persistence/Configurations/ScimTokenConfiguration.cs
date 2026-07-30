using Cardscape.Domain.Authentication.Scim;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ScimTokenConfiguration : IEntityTypeConfiguration<ScimToken>
{
    public void Configure(EntityTypeBuilder<ScimToken> b)
    {
        b.ToTable("scim_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new ScimTokenId(v));
        b.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.TokenPrefix).HasMaxLength(16).IsRequired();
        b.Property(x => x.LastUsedAt);
        b.Property(x => x.IsRevoked).IsRequired();
        b.Property(x => x.RevokedAt);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
