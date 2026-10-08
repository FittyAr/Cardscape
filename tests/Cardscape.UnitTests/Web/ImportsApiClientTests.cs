using System.Net;
using System.Text;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Moq;

namespace Cardscape.UnitTests.Web;

public sealed class ImportsApiClientTests
{
    private static readonly Guid WorkspaceId = new("5b0e4c4e-3b1d-4d8c-9a55-0c43f0a1c7e2");

    [Theory]
    [InlineData(true, "preview")]
    [InlineData(false, "apply")]
    public async Task ImportKanbanAsync_PostsMultipartThroughTheAuthenticatedApiClient(bool previewOnly, string action)
    {
        RecordingHandler handler = new("""{"importedBoardIds":[],"preview":{"boardCount":2,"wasApplied":false}}""");
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("Cardscape.Api"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        ImportsApiClient sut = new(factory.Object);

        ApiResult<ImportResultDto> result = await sut.ImportKanbanAsync(
            WorkspaceId,
            new MemoryStream(Encoding.UTF8.GetBytes("{}")),
            "boards.json",
            previewOnly,
            TestContext.Current.CancellationToken);

        result.HasValue.Should().BeTrue();
        result.Value!.Preview!.BoardCount.Should().Be(2);
        handler.RequestUri.Should().Be(new Uri($"https://api.example.test/api/imports/kanban/{action}"));
        handler.Body.Should().Contain("name=targetWorkspaceId").And.Contain(WorkspaceId.ToString());
        handler.Body.Should().Contain("name=file; filename=boards.json");
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
