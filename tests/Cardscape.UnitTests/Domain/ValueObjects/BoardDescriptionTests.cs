using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class BoardDescriptionTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("a friendly description", "a friendly description")]
    [InlineData("  trimmed  ", "trimmed")]
    public void Create_WithValidInput_ReturnsExpectedValue(string? input, string expected)
    {
        var result = BoardDescription.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Create_WithDescriptionAtMaxLength_Succeeds()
    {
        var input = new string('x', BoardDescription.MaxLength);

        var result = BoardDescription.Create(input);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithDescriptionExceedingMaxLength_ReturnsTooLongError()
    {
        var input = new string('x', BoardDescription.MaxLength + 1);

        var result = BoardDescription.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("boards.description.too_long");
    }
}
