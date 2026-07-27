using Cardscape.Api.Extensions;
using Cardscape.Application.DependencyInjection;
using Cardscape.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidation();

builder.Services.AddCardscapeApplication();
builder.Services.AddCardscapeInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ── Health check ─────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Cardscape.Api",
    timestamp = DateTime.UtcNow
}))
   .WithName("HealthCheck")
   .WithTags("Health");

app.Run();

// Required for WebApplicationFactory in integration tests.
public partial class Program;
