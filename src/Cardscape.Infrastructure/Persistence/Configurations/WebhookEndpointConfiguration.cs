using Cardscape.Domain.Boards;
using Cardscape.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> b)
    {
        b.ToTable("webhook_endpoints");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new WebhookEndpointId(v));

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        b.HasIndex(x => x.BoardId);

        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.Property(x => x.ProtectedSecret).HasMaxLength(2048).IsRequired();
        b.Property(x => x.Events).IsRequired();
        b.Property(x => x.Active).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
    }
}
