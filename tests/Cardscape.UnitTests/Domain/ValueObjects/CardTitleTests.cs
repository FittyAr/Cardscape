using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class CardTitleTests
{
    [Theory]
    [InlineData("Buy milk")]
    [InlineData("  Refactor auth module  ")]
    public void Create_WithValidTitle_ReturnsTrimmedSuccess(string input)
    {
        var result = CardTitle.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingTitle_ReturnsRequiredError(string? input)
    {
        var result = CardTitle.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.title.required");
    }

    [Fact]
    public void Create_WithTitleExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', CardTitle.MaxLength + 1);

        var result = CardTitle.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cards.title.length");
    }
}
