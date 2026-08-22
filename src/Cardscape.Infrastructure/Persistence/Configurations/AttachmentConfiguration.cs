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
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(id => id.Value, v => new AttachmentId(v));

        b.Property(a => a.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        b.HasIndex(a => new { a.CardId, a.IsDeleted, a.CreatedAt });

        b.Property(a => a.FileName).HasMaxLength(512).IsRequired();
        b.Property(a => a.MimeType).HasMaxLength(256).IsRequired();
        b.Property(a => a.SizeBytes).IsRequired();
        b.Property(a => a.StorageKey).HasMaxLength(1024).IsRequired();
        b.Property(a => a.UploaderId).IsRequired();
        b.HasIndex(a => a.UploaderId);

        b.Property(a => a.CreatedAt).IsRequired();
        b.Property(a => a.UpdatedAt);
        b.Property(a => a.CreatedBy);
        b.Property(a => a.UpdatedBy);
        b.Property(a => a.IsDeleted);
    }
}
