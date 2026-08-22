using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> b)
    {
        b.ToTable("custom_field_definitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CustomFieldDefinitionId(v));

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.OptionsJson).HasColumnType("TEXT");
        b.Property(x => x.Position).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        b.HasIndex(x => new { x.BoardId, x.Position })
            .HasDatabaseName("IX_custom_field_definitions_BoardId_Position");
    }
}

public sealed class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> b)
    {
        b.ToTable("custom_field_values");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new CustomFieldValueId(v));

        b.Property(x => x.FieldDefinitionId)
            .HasConversion(id => id.Value, v => new CustomFieldDefinitionId(v))
            .IsRequired();
        b.Property(x => x.CardId)
            .HasConversion(id => id.Value, v => new CardId(v))
            .IsRequired();
        b.Property(x => x.ValueJson).HasColumnType("TEXT").IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);

        // (FieldDefinitionId, CardId) is the natural unique key.
        b.HasIndex(x => new { x.FieldDefinitionId, x.CardId })
            .IsUnique()
            .HasDatabaseName("IX_custom_field_values_Field_Card");

        b.HasIndex(x => x.CardId).HasDatabaseName("IX_custom_field_values_CardId");
    }
}
