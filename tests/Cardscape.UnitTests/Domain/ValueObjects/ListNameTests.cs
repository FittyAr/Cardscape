using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class ListNameTests
{
    [Theory]
    [InlineData("To Do")]
    [InlineData("  In Progress  ")]
    public void Create_WithValidName_ReturnsTrimmedSuccess(string input)
    {
        var result = ListName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsRequiredError(string? input)
    {
        var result = ListName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("lists.name.required");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', ListName.MaxLength + 1);

        var result = ListName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("lists.name.length");
    }
}
