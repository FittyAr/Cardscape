using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Boards;

public class CustomFieldValueTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidateShape_Text_Accepts_JSON_String()
    {
        var result = CustomFieldValue.ValidateShape("\"hello\"", CustomFieldKind.Text);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateShape_Text_Rejects_Number()
    {
        var result = CustomFieldValue.ValidateShape("42", CustomFieldKind.Text);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.value_not_text");
    }

    [Fact]
    public void ValidateShape_Text_Rejects_String_Longer_Than_4000()
    {
        string tooLong = "\"" + new string('a', 4001) + "\"";
        var result = CustomFieldValue.ValidateShape(tooLong, CustomFieldKind.Text);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.text_too_long");
    }

    [Fact]
    public void ValidateShape_Number_Accepts_JSON_Number()
    {
        var result = CustomFieldValue.ValidateShape("3.14", CustomFieldKind.Number);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateShape_Number_Rejects_String()
    {
        var result = CustomFieldValue.ValidateShape("\"3.14\"", CustomFieldKind.Number);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.value_not_number");
    }

    [Fact]
    public void ValidateShape_Date_Accepts_ISO_8601_String()
    {
        var result = CustomFieldValue.ValidateShape("\"2026-07-15T00:00:00Z\"", CustomFieldKind.Date);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateShape_Date_Rejects_Non_Date_String()
    {
        var result = CustomFieldValue.ValidateShape("\"not a date\"", CustomFieldKind.Date);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.value_not_iso8601");
    }

    [Fact]
    public void ValidateShape_Checkbox_Accepts_Boolean()
    {
        CustomFieldValue.ValidateShape("true", CustomFieldKind.Checkbox).IsSuccess.Should().BeTrue();
        CustomFieldValue.ValidateShape("false", CustomFieldKind.Checkbox).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateShape_Checkbox_Rejects_String()
    {
        var result = CustomFieldValue.ValidateShape("\"true\"", CustomFieldKind.Checkbox);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.value_not_bool");
    }

    [Fact]
    public void SetValue_With_Empty_String_Clears_Value()
    {
        var value = CustomFieldValue.Create(
            CustomFieldDefinitionId.New(), CardId.New(), "\"old\"", Now).Value;

        value.SetValue(string.Empty, CustomFieldKind.Text, Now).IsSuccess.Should().BeTrue();
        value.ValueJson.Should().BeEmpty();
    }

    [Fact]
    public void SetValue_With_Mismatched_Kind_Returns_Failure()
    {
        var value = CustomFieldValue.Create(
            CustomFieldDefinitionId.New(), CardId.New(), "\"old\"", Now).Value;

        var result = value.SetValue("42", CustomFieldKind.Text, Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.value_not_text");
    }
}
