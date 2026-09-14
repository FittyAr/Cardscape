using Cardscape.Infrastructure.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

internal sealed class ExternalMessageReceiptConfiguration
    : IEntityTypeConfiguration<ExternalMessageReceipt>
{
    public void Configure(EntityTypeBuilder<ExternalMessageReceipt> builder)
    {
        builder.ToTable("external_message_inbox");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Source).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.MessageHash).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.CreatedAt).IsRequired();
        builder.Property(receipt => receipt.LeaseExpiresAtUtcTicks).IsRequired();
        builder.HasIndex(receipt => new { receipt.Source, receipt.MessageHash }).IsUnique();
        builder.HasIndex(receipt => receipt.LeaseExpiresAtUtcTicks);
    }
}
