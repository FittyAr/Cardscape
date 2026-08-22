using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> b)
    {
        b.ToTable("idempotency_keys");
        b.HasKey(k => k.Id);
        b.Property(k => k.Id).HasConversion(id => id.Value, v => new IdempotencyKeyId(v));

        b.Property(k => k.OwnerId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(k => k.OwnerId);

        b.Property(k => k.Key)
            .HasConversion(k => k.Value, v => IdempotencyKeyValue.Create(v).Value)
            .HasMaxLength(IdempotencyKeyValue.MaxLength)
            .IsRequired();
        b.HasIndex(k => new { k.OwnerId, k.Key }).IsUnique();

        b.Property(k => k.RequestHash)
            .HasMaxLength(IdempotencyKey.RequestHashLength)
            .IsRequired();

        b.Property(k => k.ResponseStatusCode)
            .IsRequired()
            .HasDefaultValue(200);

        b.Property(k => k.ResponseJson)
            .IsRequired();

        b.Property(k => k.ExpiresAt).IsRequired();
        b.HasIndex(k => k.ExpiresAt);

        b.Property(k => k.CreatedAt).IsRequired();
        b.Property(k => k.UpdatedAt);
        b.Property(k => k.CreatedBy);
        b.Property(k => k.UpdatedBy);
        b.Property(k => k.IsDeleted);
        b.Property(k => k.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
