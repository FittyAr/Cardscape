using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new UserId(v));
        b.Property(x => x.Email)
            .HasConversion(e => e.Value, v => EmailAddress.Create(v).Value)
            .HasMaxLength(EmailAddress.MaxLength)
            .IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.DisplayName)
            .HasConversion(d => d.Value, v => DisplayName.Create(v).Value)
            .HasMaxLength(DisplayName.MaxLength)
            .IsRequired();
        b.Property(x => x.PasswordHash)
            .HasConversion(p => p.Value, v => PasswordHash.FromHashed(v).Value)
            .HasMaxLength(512)
            .IsRequired();
        b.Property(x => x.AvatarUrl).HasMaxLength(2_000);
        b.Property(x => x.LastLoginAt);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.IsDeleted).IsRequired();
        b.Property(x => x.DeletedAt);
        b.Property(x => x.IsAnonymised).IsRequired();
        b.Property(x => x.AnonymisedAt);
        b.Property(x => x.IsRestricted).IsRequired();
        b.Property(x => x.RestrictedAt);
        b.Property(x => x.IsAdmin).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
    }
}
