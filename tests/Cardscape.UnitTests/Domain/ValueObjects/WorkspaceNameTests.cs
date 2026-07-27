using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class WorkspaceNameTests
{
    [Theory]
    [InlineData("Acme")]
    [InlineData("  Personal  ")]
    public void Create_WithValidName_ReturnsTrimmedSuccess(string input)
    {
        var result = WorkspaceName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingName_ReturnsRequiredError(string? input)
    {
        var result = WorkspaceName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.name.required");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsLengthError()
    {
        var input = new string('x', WorkspaceName.MaxLength + 1);

        var result = WorkspaceName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workspaces.name.length");
    }
}
