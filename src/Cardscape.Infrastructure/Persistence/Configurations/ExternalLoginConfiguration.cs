using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> b)
    {
        b.ToTable("external_logins");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasConversion(id => id.Value, v => new ExternalLoginId(v));

        b.Property(l => l.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        b.HasIndex(l => new { l.UserId, l.LastUsedAt });

        b.Property(l => l.Provider)
            .HasConversion<int>()
            .IsRequired();

        b.Property(l => l.Subject)
            .HasConversion(s => s.Value, v => SubjectId.Create(v).Value)
            .HasMaxLength(SubjectId.MaxLength)
            .IsRequired();

        b.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();

        b.Property(l => l.Email).HasMaxLength(320);
        b.Property(l => l.DisplayName).HasMaxLength(200);
        b.Property(l => l.LastUsedAt).IsRequired();

        b.Property(l => l.CreatedAt).IsRequired();
        b.Property(l => l.UpdatedAt);
        b.Property(l => l.CreatedBy);
        b.Property(l => l.UpdatedBy);
        b.Property(l => l.IsDeleted);
    }
}
