using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Cardscape.Domain.UserPreferences;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants one row per persona plus a couple of helper
/// accounts (the demo admin, the API-only service user). The
/// password is hashed via the configured <c>IPasswordHasher</c>
/// so the seeded accounts are real, sign-in-able users in
/// Development.</summary>
public sealed class UsersSeedStep : SeedStepBase
{
    private readonly IPasswordHasher _hasher;

    public UsersSeedStep(IPasswordHasher hasher) => _hasher = hasher;

    public override string Name => "Users + preferences";
    public override int Order => 10;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        var random = new Random(20260810);
        DateTimeOffset now = context.Now;

        // 1. Persona users — one per row in NexoraStudios.Personas.
        foreach (Persona persona in NexoraStudios.Personas)
        {
            UserId id = UserId.New();
            string email = $"{persona.EmailLocalPart}@{NexoraStudios.DemoEmailDomain}";
            EmailAddress emailVo = EmailAddress.Create(email).Value;
            DisplayName displayName = DisplayName.Create(persona.DisplayName).Value;
            PasswordHash hash = _hasher.Hash(Generators.PasswordGenerator.DemoPassword());

            Result<User> registered = User.Register(id, emailVo, displayName, hash, now);
            if (registered.IsFailure)
            {
                Log(log, SeedLogLevel.Error, $"Failed to register persona {persona.DisplayName}: {registered.Error.Message}");
                continue;
            }

            User user = registered.Value;
            user.RecordLogin(now.AddDays(-random.Next(1, 14)));
            user.SetAdmin(persona.EmailLocalPart == "ada.lovelace", now);
            context.Db.Users.Add(user);
            context.Users.Add(user);

            // Preferences — one row per user. The factory requires
            // a valid theme name; we use the Radzen default.
            UserPreferences prefs = UserPreferences.Create(
                user.Id,
                themeName: "default",
                mode: persona.EmailLocalPart == "ada.lovelace" ? AppearanceMode.Dark : AppearanceMode.Light,
                at: now).Value;
            context.Add(prefs);
            context.UserPreferences.Add(prefs);

            Log(log, SeedLogLevel.Info, $"  · {persona.DisplayName} <{email}> ({persona.JobTitle})");
        }

        // 2. An additional service user for the API-only token
        //    flows (no persona, no preferences, no workspace
        //    membership). Same email pattern as the personas.
        UserId svcId = UserId.New();
        Result<User> svc = User.Register(
            svcId,
            EmailAddress.Create("ci-bot@nexora.example").Value,
            DisplayName.Create("CI Bot").Value,
            _hasher.Hash(Generators.PasswordGenerator.DemoPassword()),
            now);
        if (svc.IsSuccess)
        {
            context.Db.Users.Add(svc.Value);
            context.Users.Add(svc.Value);
            Log(log, SeedLogLevel.Info, "  · CI Bot <ci-bot@nexora.example> (service account)");
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Users.Count} users and {context.UserPreferences.Count} preference rows.");
        return Task.CompletedTask;
    }
}
