using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailAddressConfiguration : IEntityTypeConfiguration<InboundEmailAddress>
{
    public void Configure(EntityTypeBuilder<InboundEmailAddress> builder)
    {
        builder.ToTable("inbound_email_addresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new InboundEmailAddressId(v));

        builder.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        builder.HasIndex(x => x.WorkspaceId);

        builder.Property(x => x.EmailAddress).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => x.EmailAddress).IsUnique();

        builder.Property(x => x.TargetListId)
            .HasConversion(id => id.Value, v => new BoardListId(v))
            .IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Active).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
