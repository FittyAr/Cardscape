using Cardscape.Contracts.Settings;

namespace Cardscape.Api.Settings;

/// <summary>
/// Read-only report of the startup configuration that shapes this instance.
/// These values are bound once at startup (options, DI, middleware), so the
/// admin UI shows them instead of offering fields that could not take
/// effect. The catalogue is declarative: add a descriptor to report a key.
/// </summary>
public sealed class RuntimeConfigurationReport(IConfiguration configuration)
{
    private static readonly Descriptor[] Catalogue =
    [
        new("Database", "Provider", "Database:Provider"),
        new("Database", "Connection string", "ConnectionStrings:Default", IsSecret: true),

        new("Security", "JWT issuer", "Jwt:Issuer"),
        new("Security", "JWT audience", "Jwt:Audience"),
        new("Security", "Access token lifetime (minutes)", "Jwt:AccessTokenMinutes"),
        new("Security", "JWT signing key", "Jwt:SigningKey", IsSecret: true),
        new("Security", "Allowed CORS origins", "Cors:AllowedOrigins"),
        new("Security", "Data-protection key directory", "Cardscape:DataProtection:KeyDirectory"),
        new("Security", "Cache admin claim in tokens", "Cardscape:Api:AdminAuthorization:CacheAdminClaim"),

        new("Sign-in providers", "Google client", "Authentication:Google:ClientId", IsSecret: true),
        new("Sign-in providers", "Microsoft client", "Authentication:Microsoft:ClientId", IsSecret: true),
        new("Sign-in providers", "Apple client", "Authentication:Apple:ClientId", IsSecret: true),

        new("Storage", "Attachment storage root", "Storage:LocalRoot"),
        new("Storage", "Data root (settings, keys)", "Cardscape:DataRoot"),

        new("Infrastructure", "Rate limiter backend", "Cardscape:Infrastructure:RateLimiter:Backend"),
        new("Infrastructure", "Pending 2FA store backend", "Cardscape:Infrastructure:PendingTotpStore:Backend"),
        new("Infrastructure", "Redis connection", "Cardscape:Infrastructure:Redis:ConnectionString", IsSecret: true),
        new("Infrastructure", "Deployment region", "Cardscape:Deployment:Region"),

        new("Integrations", "Google Calendar client", "Integrations:GoogleCalendar:ClientId", IsSecret: true),
        new("Integrations", "GitHub token", "Integrations:GitHub:Token", IsSecret: true),
        new("Integrations", "Inbound e-mail signing secret", "InboundEmail:SigningSecret", IsSecret: true),
        new("Integrations", "MCP server URL", "Cardscape:Mcp:BaseUrl"),
        new("Integrations", "MCP broadcast secret", "Cardscape:Internal:Secret", IsSecret: true),

        new("Observability", "OpenTelemetry endpoint", "Otel:EndpointUrl"),
        new("Observability", "OpenTelemetry service name", "Otel:ServiceName"),
        new("Observability", "Log directory", "Serilog:File:RootPath"),
    ];

    public IReadOnlyList<RuntimeConfigurationEntry> Build() => [.. Catalogue.Select(Describe)];

    private RuntimeConfigurationEntry Describe(Descriptor descriptor)
    {
        IConfigurationSection section = configuration.GetSection(descriptor.Key);
        string? value = section.Value is { Length: > 0 } scalar
            ? scalar
            : section.GetChildren().Select(child => child.Value).OfType<string>().ToArray() is { Length: > 0 } items
                ? string.Join(", ", items)
                : null;

        return new RuntimeConfigurationEntry(
            descriptor.Category,
            descriptor.Name,
            descriptor.IsSecret ? null : value,
            descriptor.IsSecret,
            IsConfigured: value is not null,
            EnvironmentVariable: descriptor.Key.Replace(":", "__", StringComparison.Ordinal));
    }

    private sealed record Descriptor(string Category, string Name, string Key, bool IsSecret = false);
}
