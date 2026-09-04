using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasConversion(id => id.Value, v => new IdempotencyKeyId(v));

        builder.Property(k => k.OwnerId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(k => k.OwnerId);

        builder.Property(k => k.Key)
            .HasConversion(k => k.Value, v => IdempotencyKeyValue.Create(v).Value)
            .HasMaxLength(IdempotencyKeyValue.MaxLength)
            .IsRequired();
        builder.HasIndex(k => new { k.OwnerId, k.Key }).IsUnique();

        builder.Property(k => k.RequestHash)
            .HasMaxLength(IdempotencyKey.RequestHashLength)
            .IsRequired();

        builder.Property(k => k.ResponseStatusCode)
            .IsRequired()
            .HasDefaultValue(200);

        builder.Property(k => k.ResponseJson)
            .IsRequired();

        builder.Property(k => k.ExpiresAt).IsRequired();
        builder.HasIndex(k => k.ExpiresAt);

        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.UpdatedAt);
        builder.Property(k => k.CreatedBy);
        builder.Property(k => k.UpdatedBy);
        builder.Property(k => k.IsDeleted);
    }
}
