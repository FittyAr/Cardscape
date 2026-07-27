using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class ChecklistTitleTests
{
    [Theory]
    [InlineData("Pre-flight checks")]
    [InlineData("  Release steps  ")]
    public void Create_WithValidTitle_ReturnsTrimmedSuccess(string input)
    {
        var result = ChecklistTitle.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithMissingTitle_ReturnsRequiredError(string? input)
    {
        var result = ChecklistTitle.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("checklists.title.required");
    }

    [Fact]
    public void Create_WithTitleExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', ChecklistTitle.MaxLength + 1);

        var result = ChecklistTitle.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("checklists.title.length");
    }
}
