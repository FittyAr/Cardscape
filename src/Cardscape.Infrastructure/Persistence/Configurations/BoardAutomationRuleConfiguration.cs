using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardAutomationRuleConfiguration : IEntityTypeConfiguration<BoardAutomationRule>
{
    public void Configure(EntityTypeBuilder<BoardAutomationRule> builder)
    {
        builder.ToTable("board_automation_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardAutomationRuleId(v));

        builder.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        builder.HasIndex(x => new { x.BoardId, x.Position });
        builder.HasIndex(x => new { x.BoardId, x.IsEnabled, x.Position });

        builder.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Trigger).HasConversion<int>().IsRequired();
        builder.Property(x => x.TriggerListId);
        builder.Property(x => x.Action).HasConversion<int>().IsRequired();
        builder.Property(x => x.ActionArgument).HasMaxLength(200);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.Position).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy);
        builder.Property(x => x.UpdatedBy);
        builder.Property(x => x.IsDeleted);
    }
}
