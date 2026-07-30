using Cardscape.Domain.Authentication.Saml;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class SamlConnectionConfiguration : IEntityTypeConfiguration<SamlConnection>
{
    public void Configure(EntityTypeBuilder<SamlConnection> b)
    {
        b.ToTable("saml_connections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new SamlConnectionId(v));
        b.Property(x => x.WorkspaceId).HasConversion(id => id.Value, v => new Domain.Workspaces.WorkspaceId(v));
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.IdpEntityId).HasMaxLength(512).IsRequired();
        b.Property(x => x.IdpMetadataUrl).HasMaxLength(2048).IsRequired();
        b.Property(x => x.IdpMetadataXml).HasMaxLength(8192);
        b.Property(x => x.SpEntityId).HasMaxLength(512).IsRequired();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.WorkspaceId).IsUnique();
    }
}
