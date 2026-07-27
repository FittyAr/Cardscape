using Cardscape.Domain.Comments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CommentId(v));
        b.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new Domain.Cards.CardId(v))
            .IsRequired();
        b.Property(x => x.AuthorId).IsRequired();
        b.Property(x => x.Body)
            .HasConversion(b => b.Value, v => CommentBody.Create(v).Value)
            .HasMaxLength(CommentBody.MaxLength)
            .IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.CardId);
    }
}
