using System.Text.Json;
using Cardscape.Domain.Boards;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Boards;

public class CustomFieldDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_With_Text_Kind_Stores_Name_And_Kind()
    {
        var result = CustomFieldDefinition.Create(
            BoardId.New(), "Priority", CustomFieldKind.Text, null, position: 0, at: Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Priority");
        result.Value.Kind.Should().Be(CustomFieldKind.Text);
        result.Value.OptionsJson.Should().Be("[]");
    }

    [Fact]
    public void Create_With_Blank_Name_Returns_Validation_Error()
    {
        var result = CustomFieldDefinition.Create(
            BoardId.New(), "   ", CustomFieldKind.Text, null, 0, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.name_required");
    }

    [Fact]
    public void Create_With_Name_Longer_Than_120_Chars_Returns_Validation_Error()
    {
        string tooLong = new('a', 121);

        var result = CustomFieldDefinition.Create(
            BoardId.New(), tooLong, CustomFieldKind.Text, null, 0, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.name_too_long");
    }

    [Fact]
    public void Create_Dropdown_Without_Options_Returns_Validation_Error()
    {
        var result = CustomFieldDefinition.Create(
            BoardId.New(), "Severity", CustomFieldKind.Dropdown,
            dropdownOptions: null, position: 0, at: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.dropdown_options_required");
    }

    [Fact]
    public void Create_Dropdown_With_Duplicate_Options_Returns_Validation_Error()
    {
        var result = CustomFieldDefinition.Create(
            BoardId.New(), "Severity", CustomFieldKind.Dropdown,
            new[] { "Low", "low", "High" }, position: 0, at: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.dropdown_options_duplicate");
    }

    [Fact]
    public void Create_Dropdown_With_Valid_Options_Serializes_To_Json()
    {
        var result = CustomFieldDefinition.Create(
            BoardId.New(), "Severity", CustomFieldKind.Dropdown,
            new[] { "Low", "Medium", "High" }, position: 0, at: Now);

        result.IsSuccess.Should().BeTrue();
        string[]? options = JsonSerializer.Deserialize<string[]>(result.Value.OptionsJson);
        options.Should().Equal("Low", "Medium", "High");
    }

    [Fact]
    public void Rename_Updates_Name_And_Stamps_UpdatedAt()
    {
        var field = CustomFieldDefinition.Create(
            BoardId.New(), "Priority", CustomFieldKind.Text, null, 0, Now).Value;

        field.Rename("Importance", Now.AddHours(1)).IsSuccess.Should().BeTrue();
        field.Name.Should().Be("Importance");
    }

    [Fact]
    public void Reorder_Updates_Position()
    {
        var field = CustomFieldDefinition.Create(
            BoardId.New(), "Priority", CustomFieldKind.Text, null, 0, Now).Value;

        field.Reorder(5, Now);
        field.Position.Should().Be(5);
    }
}
