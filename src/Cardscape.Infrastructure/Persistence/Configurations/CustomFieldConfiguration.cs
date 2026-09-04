using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("custom_field_definitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CustomFieldDefinitionId(v));

        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.OptionsJson);
        builder.Property(x => x.Position).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        builder.HasIndex(x => new { x.BoardId, x.Position })
            .HasDatabaseName("IX_custom_field_definitions_BoardId_Position");
    }
}

public sealed class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.ToTable("custom_field_values");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new CustomFieldValueId(v));

        builder.Property(x => x.FieldDefinitionId)
            .HasConversion(id => id.Value, v => new CustomFieldDefinitionId(v))
            .IsRequired();
        builder.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        builder.Property(x => x.ValueJson).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);

        // (FieldDefinitionId, CardId) is the natural unique key.
        builder.HasIndex(x => new { x.FieldDefinitionId, x.CardId })
            .IsUnique()
            .HasDatabaseName("IX_custom_field_values_Field_Card");

        builder.HasIndex(x => x.CardId).HasDatabaseName("IX_custom_field_values_CardId");
    }
}
