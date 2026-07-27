using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class CardDescriptionTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("Markdown body", "Markdown body")]
    [InlineData("  trimmed  ", "trimmed")]
    public void Create_WithValidInput_ReturnsExpectedValue(string? input, string expected)
    {
        var result = CardDescription.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Create_WithDescriptionExceedingMaxLength_ReturnsTooLongError()
    {
        var input = new string('x', CardDescription.MaxLength + 1);

        var result = CardDescription.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.description.too_long");
    }
}
