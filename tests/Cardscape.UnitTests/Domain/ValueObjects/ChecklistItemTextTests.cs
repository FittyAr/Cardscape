using Cardscape.Domain.Checklists;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class ChecklistItemTextTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Verify migrations applied")]
    [InlineData("  trimmed  ", "trimmed")]
    public void Create_WithValidText_Succeeds(string input, string? expected = null)
    {
        var result = ChecklistItemText.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected ?? input);
    }

    [Fact]
    public void Create_WithNull_TreatsAsEmpty()
    {
        var result = ChecklistItemText.Create(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void Create_WithTextExceedingMaxLength_ReturnsTooLongError()
    {
        var input = new string('x', ChecklistItemText.MaxLength + 1);

        var result = ChecklistItemText.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("checklists.item_text.too_long");
    }
}
