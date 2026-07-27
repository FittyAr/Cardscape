using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class BoardNameTests
{
    [Theory]
    [InlineData("My Board")]
    [InlineData("  Sprint Backlog  ")]
    public void Create_WithValidName_ReturnsTrimmedSuccess(string input)
    {
        var result = BoardName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsRequiredError(string? input)
    {
        var result = BoardName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("boards.name.required");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        var input = new string('x', BoardName.MaxLength);

        var result = BoardName.Create(input);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', BoardName.MaxLength + 1);

        var result = BoardName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("boards.name.length");
    }
}
