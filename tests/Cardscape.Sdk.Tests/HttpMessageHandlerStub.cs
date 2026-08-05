namespace Cardscape.Sdk.Tests;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> subclass that lets
/// each test supply a response factory. The test does not
/// arrange a real socket; the handler is a pure in-process
/// stub. The single shared instance lives in this file so both
/// the client-construction tests and the sub-client tests
/// see the same surface.
/// </summary>
internal sealed class HttpMessageHandlerStub : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncFactory;

    public HttpMessageHandlerStub()
        : this(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") })
    {
    }

    public HttpMessageHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _factory = factory;
    }

    public HttpMessageHandlerStub(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncFactory)
    {
        _factory = _ => throw new NotSupportedException("Use the async overload.");
        _asyncFactory = asyncFactory;
    }

    public void SendProbe() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_asyncFactory is not null)
        {
            return await _asyncFactory(request);
        }
        return _factory(request);
    }
}
