var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// TODO step 3 (out of scope for this commit):
// builder.Services.AddApplication();
// builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
// builder.Services.AddApiAuthentication(builder.Configuration);
//   ^ AddInfrastructure picks the EF Core provider from configuration:
//     Database:Provider = Sqlite | PostgreSQL | MariaDB

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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
