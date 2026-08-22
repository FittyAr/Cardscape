using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

/// <summary>EF Core fluent configuration for
/// <see cref="UserPreferences"/>. The aggregate is 1:1 with
/// <see cref="User"/>; <c>UserId</c> is the primary key (not
/// a separate <c>Id</c> column), which lets the row live or
/// die with its owner without a foreign-key cascade dance.</summary>
public sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> b)
    {
        b.ToTable("user_preferences");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new UserId(v))
            .ValueGeneratedNever(); // user supplies the id (= UserId); no DB-generated PK

        b.Property(p => p.ThemeName)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(UserPreferences.DefaultThemeName);

        b.Property(p => p.Mode)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(AppearanceMode.System);

        b.Property(p => p.CreatedAt).IsRequired();
        b.Property(p => p.UpdatedAt);
        b.Property(p => p.CreatedBy);
        b.Property(p => p.UpdatedBy);
        b.Property(p => p.IsDeleted);
    }
}
