using Cardscape.Domain.Comments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CommentId(v));
        builder.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new Domain.Cards.CardId(v))
            .IsRequired();
        builder.Property(x => x.AuthorId).IsRequired();
        builder.Property(x => x.Body)
            .HasConversion(builder => builder.Value, v => CommentBody.Create(v).Value)
            .HasMaxLength(CommentBody.MaxLength)
            .IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
        builder.HasIndex(x => new { x.CardId, x.IsDeleted, x.CreatedAt });
        builder.HasIndex(x => x.AuthorId);
    }
}
