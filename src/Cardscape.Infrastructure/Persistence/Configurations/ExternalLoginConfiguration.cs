using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasConversion(id => id.Value, v => new ExternalLoginId(v));

        builder.Property(l => l.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasIndex(l => new { l.UserId, l.LastUsedAt });

        builder.Property(l => l.Provider)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.Subject)
            .HasConversion(s => s.Value, v => SubjectId.Create(v).Value)
            .HasMaxLength(SubjectId.MaxLength)
            .IsRequired();

        builder.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();

        builder.Property(l => l.Email).HasMaxLength(320);
        builder.Property(l => l.DisplayName).HasMaxLength(200);
        builder.Property(l => l.LastUsedAt).IsRequired();

        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.UpdatedAt);
        builder.Property(l => l.CreatedBy);
        builder.Property(l => l.UpdatedBy);
        builder.Property(l => l.IsDeleted);
    }
}
