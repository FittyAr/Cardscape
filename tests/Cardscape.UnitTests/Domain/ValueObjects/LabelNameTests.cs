using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class LabelNameTests
{
    [Theory]
    [InlineData("urgent")]
    [InlineData("  bug  ")]
    public void Create_WithValidName_ReturnsTrimmedSuccess(string input)
    {
        var result = LabelName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsRequiredError(string? input)
    {
        var result = LabelName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("labels.name.required");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', LabelName.MaxLength + 1);

        var result = LabelName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("labels.name.length");
    }
}
