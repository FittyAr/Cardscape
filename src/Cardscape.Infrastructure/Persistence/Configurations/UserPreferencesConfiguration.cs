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
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new UserId(v))
            .ValueGeneratedNever(); // user supplies the id (= UserId); no DB-generated PK

        builder.Property(p => p.ThemeName)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue(UserPreferences.DefaultThemeName);

        builder.Property(p => p.Mode)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(AppearanceMode.System);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.CreatedBy);
        builder.Property(p => p.UpdatedBy);
        builder.Property(p => p.IsDeleted);
    }
}
