using Cardscape.Domain.Common;

namespace Cardscape.UnitTests.Domain.ValueObjects;

public class ColorTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#abcdef")]
    [InlineData("#123456")]
    public void Create_WithValidHexColor_Succeeds(string input)
    {
        var result = Color.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithMissingValue_ReturnsRequiredError(string? input)
    {
        var result = Color.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("common.color.required");
    }

    [Theory]
    [InlineData("000000")]      // missing #
    [InlineData("#fff")]        // 3-digit shorthand
    [InlineData("#fffffff")]    // 7 digits
    [InlineData("#xyzxyz")]     // not hex
    [InlineData("rgb(0,0,0)")]
    public void Create_WithMalformedHex_ReturnsInvalidError(string input)
    {
        var result = Color.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("common.color.invalid");
    }

    [Fact]
    public void Palette_AllEntriesAreValidHex()
    {
        // Trello-style palette: each entry must round-trip through Create.
        var palette = new[]
        {
            Color.Palette.Yellow,
            Color.Palette.Purple,
            Color.Palette.Blue,
            Color.Palette.Red,
            Color.Palette.Green,
            Color.Palette.Orange,
            Color.Palette.Black,
            Color.Palette.Sky,
            Color.Palette.Lime,
            Color.Palette.Pink,
            Color.Palette.Gray
        };

        palette.Should().AllSatisfy(c =>
            c.Value.Should().MatchRegex("^#[0-9a-fA-F]{6}$"));
    }

    [Fact]
    public void Palette_HasElevenEntries()
    {
        // Locked-down count: any change here is a breaking change for boards
        // that store a label colour by Palette index.
        var palette = new[]
        {
            Color.Palette.Yellow, Color.Palette.Purple, Color.Palette.Blue,
            Color.Palette.Red, Color.Palette.Green, Color.Palette.Orange,
            Color.Palette.Black, Color.Palette.Sky, Color.Palette.Lime,
            Color.Palette.Pink, Color.Palette.Gray
        };

        palette.Should().HaveCount(11);
    }
}
