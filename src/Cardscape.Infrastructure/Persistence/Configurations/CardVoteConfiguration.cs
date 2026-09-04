using Cardscape.Domain.Cards;
using Cardscape.Domain.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CardVoteConfiguration : IEntityTypeConfiguration<CardVote>
{
    public void Configure(EntityTypeBuilder<CardVote> builder)
    {
        builder.ToTable("card_votes");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasConversion(id => id.Value, v => new CardVoteId(v));

        builder.Property(v => v.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        builder.HasIndex(v => v.CardId);

        builder.Property(v => v.UserId).IsRequired();
        builder.HasIndex(v => new { v.CardId, v.UserId }).IsUnique();

        builder.Property(v => v.VotedAt).IsRequired();

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt);
        builder.Property(v => v.CreatedBy);
        builder.Property(v => v.UpdatedBy);
        builder.Property(v => v.IsDeleted);
    }
}
