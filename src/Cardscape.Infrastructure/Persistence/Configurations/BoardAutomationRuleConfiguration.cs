using Cardscape.Domain.Boards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cardscape.Infrastructure.Persistence.Configurations;

public sealed class BoardAutomationRuleConfiguration : IEntityTypeConfiguration<BoardAutomationRule>
{
    public void Configure(EntityTypeBuilder<BoardAutomationRule> b)
    {
        b.ToTable("board_automation_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardAutomationRuleId(v));

        b.Property(x => x.BoardId)
            .HasConversion(id => id.Value, v => new BoardId(v))
            .IsRequired();
        b.HasIndex(x => x.BoardId);

        b.Property(x => x.Name)
            .HasMaxLength(120)
            .IsRequired();

        b.Property(x => x.Trigger).HasConversion<int>().IsRequired();
        b.Property(x => x.TriggerListId);
        b.Property(x => x.Action).HasConversion<int>().IsRequired();
        b.Property(x => x.ActionArgument).HasMaxLength(200);
        b.Property(x => x.IsEnabled).IsRequired();
        b.Property(x => x.Position).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt);
        b.Property(x => x.CreatedBy);
        b.Property(x => x.UpdatedBy);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.RowVersion).IsConcurrencyToken().HasDefaultValue(0u);
    }
}
