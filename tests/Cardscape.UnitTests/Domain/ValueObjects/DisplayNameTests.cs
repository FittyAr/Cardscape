using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class DisplayNameTests
{
    [Theory]
    [InlineData("Alice")]
    [InlineData("  Bob  ")]                 // whitespace is trimmed
    [InlineData("María José")]
    public void Create_WithValidName_ReturnsTrimmedSuccess(string input)
    {
        var result = DisplayName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsRequiredValidationError(string? input)
    {
        var result = DisplayName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("members.display_name.required");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        var input = new string('x', DisplayName.MaxLength);

        var result = DisplayName.Create(input);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', DisplayName.MaxLength + 1);

        var result = DisplayName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("members.display_name.length");
    }

    [Fact]
    public void ToString_ReturnsRawValue()
    {
        var name = DisplayName.Create("  Carol  ").Value;

        name.ToString().Should().Be("Carol");
    }
}
