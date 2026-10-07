using Cardscape.Seeder.Company;
using Cardscape.Seeder.Generators;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Personal API tokens in every state the token page renders: a read-only
/// token per persona, a recently used read/write token for the engineers,
/// plus a revoked and an expired one on the demo admin. Only hashes are
/// stored; the plaintext never leaves this method.
/// </summary>
internal sealed class ApiTokensSeedStep : SeedStepBase
{
    private static readonly string[] ReadScopes = ["read"];
    private static readonly string[] ReadWriteScopes = ["read", "write"];

    public override string Name => "API tokens";
    public override int Order => 100;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        SeedTimeline timeline = context.Timeline;
        foreach (Persona persona in NexoraStudios.Personas)
        {
            User user = context.User(persona);
            Issue(context, user, "Read-only reports", ReadScopes, timeline.OnDay(-timeline.Next(20, 60)), expiresAt: null);
        }

        foreach (string engineer in new[] { "ada.lovelace", "linus.pauling", "tobias.reyes" })
        {
            ApiToken token = Issue(context, context.User(engineer), "CLI (read/write)", ReadWriteScopes,
                timeline.OnDay(-30), expiresAt: context.Now.AddMonths(6));
            token.RecordUse(timeline.OnDay(-timeline.Next(0, 3)));
        }

        User ada = context.User(NexoraStudios.Personas[0]);
        ApiToken leaked = Issue(context, ada, "Old laptop", ReadWriteScopes, timeline.OnDay(-70), expiresAt: null);
        leaked.Revoke(ada.Id.Value, "Laptop retired", timeline.OnDay(-40));
        Issue(context, ada, "Q2 data export", ReadScopes, timeline.OnDay(-70), expiresAt: timeline.OnDay(-10));

        Log(log, SeedLogLevel.Success, $"Inserted {context.ApiTokens.Count} API tokens (1 revoked, 1 expired).");
        return Task.CompletedTask;
    }

    private static ApiToken Issue(
        SeedContext context, User user, string name, string[] scopes, DateTimeOffset at, DateTimeOffset? expiresAt)
    {
        string secret = PasswordGenerator.RandomUrlSafeToken(32);
        ApiToken token = ApiToken.Create(
            user.Id,
            ApiTokenName.Create(name).Value,
            PasswordGenerator.Sha256Hex(secret),
            PasswordGenerator.Prefix(secret, ApiToken.SecretPrefixLength),
            ApiTokenScopes.Create(scopes).Value,
            expiresAt,
            at).Value;
        context.Db.ApiTokens.Add(token);
        context.ApiTokens.Add(token);
        return token;
    }
}
