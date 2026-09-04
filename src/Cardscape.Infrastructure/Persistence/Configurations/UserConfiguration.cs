using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(x => x.Email)
            .HasConversion(e => e.Value, v => EmailAddress.Create(v).Value)
            .HasMaxLength(EmailAddress.MaxLength)
            .IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.DisplayName)
            .HasConversion(d => d.Value, v => DisplayName.Create(v).Value)
            .HasMaxLength(DisplayName.MaxLength)
            .IsRequired();
        builder.Property(x => x.PasswordHash)
            .HasConversion(p => p.Value, v => PasswordHash.FromHashed(v).Value)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(x => x.AvatarUrl).HasMaxLength(2_000);
        builder.Property(x => x.LastLoginAt);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.IsAnonymised).IsRequired();
        builder.Property(x => x.AnonymisedAt);
        builder.Property(x => x.IsRestricted).IsRequired();
        builder.Property(x => x.RestrictedAt);
        builder.Property(x => x.IsAdmin).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
