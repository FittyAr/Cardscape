using Cardscape.Domain.Cards;
using Cardscape.Domain.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardVoteConfiguration : IEntityTypeConfiguration<CardVote>
{
    public void Configure(EntityTypeBuilder<CardVote> b)
    {
        b.ToTable("card_votes");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasConversion(id => id.Value, v => new CardVoteId(v));

        b.Property(v => v.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        b.HasIndex(v => v.CardId);

        b.Property(v => v.UserId).IsRequired();
        b.HasIndex(v => new { v.CardId, v.UserId }).IsUnique();

        b.Property(v => v.VotedAt).IsRequired();

        b.Property(v => v.CreatedAt).IsRequired();
        b.Property(v => v.UpdatedAt);
        b.Property(v => v.CreatedBy);
        b.Property(v => v.UpdatedBy);
        b.Property(v => v.IsDeleted);
        b.Property(v => v.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
