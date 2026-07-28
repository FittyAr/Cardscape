using Cardscape.Domain.Boards;

namespace Cardscape.UnitTests.Boards;

public class BoardExtensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Enable_Stores_Board_Kind_And_Flags_Enabled()
    {
        var result = BoardExtension.Enable(
            BoardId.New(),
            ExtensionKind.CustomFields,
            configJson: null,
            at: Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.Kind.Should().Be(ExtensionKind.CustomFields);
        result.Value.ConfigJson.Should().BeNull();
    }

    [Fact]
    public void Enable_Strips_Blank_ConfigJson_To_Null()
    {
        var result = BoardExtension.Enable(
            BoardId.New(), ExtensionKind.Voting, "   ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConfigJson.Should().BeNull();
    }

    [Fact]
    public void Enable_Accepts_Json_Object_ConfigJson()
    {
        var result = BoardExtension.Enable(
            BoardId.New(),
            ExtensionKind.Voting,
            """{"upLabel":"Like","downLabel":"Nope"}""",
            Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConfigJson.Should().Contain("Like");
    }

    [Fact]
    public void Enable_Rejects_ConfigJson_Larger_Than_Limit()
    {
        string huge = "{" + new string('x', 8001);

        var result = BoardExtension.Enable(
            BoardId.New(), ExtensionKind.CustomFields, huge, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("extension.config_too_large");
    }

    [Fact]
    public void Enable_Rejects_Non_Json_ConfigJson()
    {
        var result = BoardExtension.Enable(
            BoardId.New(), ExtensionKind.Voting, "not-json", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("extension.config_not_json");
    }

    [Fact]
    public void Disable_Sets_IsEnabled_To_False()
    {
        var ext = Enabled();

        ext.Disable(Now).IsSuccess.Should().BeTrue();
        ext.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_When_Already_Disabled_Is_Conflict()
    {
        var ext = Enabled();
        ext.Disable(Now).IsSuccess.Should().BeTrue();

        var result = ext.Disable(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("extension.already_disabled");
    }

    [Fact]
    public void Enable_After_Disable_Reactivates()
    {
        var ext = Enabled();
        ext.Disable(Now).IsSuccess.Should().BeTrue();
        ext.Enable(Now);

        ext.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateConfig_Replaces_ConfigJson()
    {
        var ext = Enabled();

        var result = ext.UpdateConfig("""{"foo":1}""", Now);

        result.IsSuccess.Should().BeTrue();
        ext.ConfigJson.Should().Be("""{"foo":1}""");
    }

    [Fact]
    public void UpdateConfig_With_Invalid_Json_Returns_Validation_Error()
    {
        var ext = Enabled();

        var result = ext.UpdateConfig("nope", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("extension.config_not_json");
    }

    private static BoardExtension Enabled() =>
        BoardExtension.Enable(BoardId.New(), ExtensionKind.Voting, null, Now).Value;
}
