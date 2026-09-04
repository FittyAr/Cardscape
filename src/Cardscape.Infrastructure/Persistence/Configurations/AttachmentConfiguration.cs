using Cardscape.Domain.Attachments;
using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for attachment metadata persisted by the card
/// attachment endpoints.
/// </summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => new AttachmentId(v));

        builder.Property(a => a.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        builder.HasIndex(a => new { a.CardId, a.IsDeleted, a.CreatedAt });

        builder.Property(a => a.FileName).HasMaxLength(512).IsRequired();
        builder.Property(a => a.MimeType).HasMaxLength(256).IsRequired();
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(a => a.UploaderId).IsRequired();
        builder.HasIndex(a => a.UploaderId);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
        builder.Property(a => a.CreatedBy);
        builder.Property(a => a.UpdatedBy);
        builder.Property(a => a.IsDeleted);
    }
}
