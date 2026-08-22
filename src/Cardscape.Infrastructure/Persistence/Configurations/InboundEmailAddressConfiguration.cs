using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailAddressConfiguration : IEntityTypeConfiguration<InboundEmailAddress>
{
    public void Configure(EntityTypeBuilder<InboundEmailAddress> b)
    {
        b.ToTable("inbound_email_addresses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new InboundEmailAddressId(v));

        b.Property(x => x.WorkspaceId)
            .HasConversion(id => id.Value, v => new WorkspaceId(v))
            .IsRequired();
        b.HasIndex(x => x.WorkspaceId);

        b.Property(x => x.EmailAddress).HasMaxLength(320).IsRequired();
        b.HasIndex(x => x.EmailAddress).IsUnique();

        b.Property(x => x.TargetListId)
            .HasConversion(id => id.Value, v => new BoardListId(v))
            .IsRequired();
        b.Property(x => x.Label).HasMaxLength(200).IsRequired();
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
    }
}
