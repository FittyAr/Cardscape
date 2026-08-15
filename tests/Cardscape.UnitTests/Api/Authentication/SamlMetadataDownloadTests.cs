using System.Net;
using System.Text;
using Cardscape.Api.Authentication;

namespace Cardscape.UnitTests.Api.Authentication;

public sealed class SamlMetadataDownloadTests
{
    [Fact]
    public async Task ReadMetadataResponseAsync_WhenBodyIsWithinLimit_ReturnsXml()
    {
        const string xml = "<EntityDescriptor />";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };

        string result = await SamlAuthenticationHandler.ReadMetadataResponseAsync(
            response, TestContext.Current.CancellationToken);

        result.Should().Be(xml);
    }

    [Fact]
    public async Task ReadMetadataResponseAsync_WhenDeclaredLengthExceedsLimit_RejectsBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[SamlAuthenticationHandler.MaxMetadataBytes + 1])
        };

        Func<Task> act = () => SamlAuthenticationHandler.ReadMetadataResponseAsync(
            response, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 MiB*");
    }

    [Fact]
    public async Task ReadMetadataResponseAsync_WhenStreamWithoutLengthExceedsLimit_RejectsBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(
                new byte[SamlAuthenticationHandler.MaxMetadataBytes + 1])
        };
        response.Content.Headers.ContentLength.Should().BeNull();

        Func<Task> act = () => SamlAuthenticationHandler.ReadMetadataResponseAsync(
            response, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 MiB*");
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
