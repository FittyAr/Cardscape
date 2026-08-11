using Cardscape.Mcp.Resources;
using FluentAssertions;

namespace Cardscape.UnitTests.Security;

public sealed class McpResourceUriParserTests
{
    private static readonly Guid ExpectedId = Guid.Parse("8de61e82-2b3c-4595-93f7-13bdad5ac98b");

    [Theory]
    [InlineData("workspace://8de61e82-2b3c-4595-93f7-13bdad5ac98b", "workspace")]
    [InlineData("board://8de61e82-2b3c-4595-93f7-13bdad5ac98b", "board")]
    [InlineData("card://8de61e82-2b3c-4595-93f7-13bdad5ac98b", "card")]
    [InlineData("cards://board/8de61e82-2b3c-4595-93f7-13bdad5ac98b", "cards")]
    [InlineData("lists://board/8de61e82-2b3c-4595-93f7-13bdad5ac98b", "lists")]
    public void Parse_AdvertisedResourceUri_ReturnsExpectedId(string rawUri, string resource)
    {
        var uri = new Uri(rawUri);

        Guid result = Parse(resource, uri);

        result.Should().Be(ExpectedId);
    }

    [Theory]
    [InlineData("board://not-a-guid", "board")]
    [InlineData("board://00000000-0000-0000-0000-000000000000", "board")]
    [InlineData("card://8de61e82-2b3c-4595-93f7-13bdad5ac98b", "board")]
    [InlineData("board://8de61e82-2b3c-4595-93f7-13bdad5ac98b/extra", "board")]
    [InlineData("board://8de61e82-2b3c-4595-93f7-13bdad5ac98b?view=full", "board")]
    [InlineData("board://8de61e82-2b3c-4595-93f7-13bdad5ac98b#details", "board")]
    [InlineData("cards://other/8de61e82-2b3c-4595-93f7-13bdad5ac98b", "cards")]
    [InlineData("cards://board/8de61e82-2b3c-4595-93f7-13bdad5ac98b/extra", "cards")]
    public void Parse_MalformedOrCrossContractUri_Throws(string rawUri, string resource)
    {
        var uri = new Uri(rawUri);

        Action act = () => Parse(resource, uri);

        act.Should().Throw<ArgumentException>().WithMessage("*does not match the resource contract*");
    }

    private static Guid Parse(string resource, Uri uri) => resource switch
    {
        "workspace" => McpResourceUriParser.ParseWorkspaceId(uri),
        "board" => McpResourceUriParser.ParseBoardId(uri),
        "card" => McpResourceUriParser.ParseCardId(uri),
        "cards" => McpResourceUriParser.ParseCardsBoardId(uri),
        "lists" => McpResourceUriParser.ParseListsBoardId(uri),
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
    };
}
