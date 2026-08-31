using Cardscape.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddCardscapeApiHost();

var app = builder.Build();
app.ConfigureCardscapePipeline();
app.Run();

namespace Cardscape.Api
{
    /// <summary>Entry point exposed for WebApplicationFactory-based tests.</summary>
    public partial class Program;
}
