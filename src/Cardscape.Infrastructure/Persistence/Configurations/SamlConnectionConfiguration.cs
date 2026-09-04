using Cardscape.Domain.Authentication.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SamlConnectionConfiguration : IEntityTypeConfiguration<SamlConnection>
{
    public void Configure(EntityTypeBuilder<SamlConnection> builder)
    {
        builder.ToTable("saml_connections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new SamlConnectionId(v));
        builder.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        builder.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IdpEntityId).HasMaxLength(512).IsRequired();
        builder.Property(x => x.IdpMetadataUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.IdpMetadataXml).HasMaxLength(8192);
        builder.Property(x => x.SpEntityId).HasMaxLength(512).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.WorkspaceId).IsUnique();
    }
}
