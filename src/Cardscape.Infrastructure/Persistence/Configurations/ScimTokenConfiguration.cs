using Cardscape.Domain.Authentication.Scim;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ScimTokenConfiguration : IEntityTypeConfiguration<ScimToken>
{
    public void Configure(EntityTypeBuilder<ScimToken> builder)
    {
        builder.ToTable("scim_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new ScimTokenId(v));
        builder.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.TokenPrefix).HasMaxLength(16).IsRequired();
        builder.Property(x => x.LastUsedAt);
        builder.Property(x => x.IsRevoked).IsRequired();
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
