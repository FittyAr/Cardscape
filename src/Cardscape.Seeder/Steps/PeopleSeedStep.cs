using Cardscape.Application.Abstractions.Security;
using Cardscape.Seeder.Company;
using Cardscape.Seeder.Generators;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;
using Cardscape.Seeder.Simulation;

namespace Cardscape.Seeder.Steps;

/// <summary>
/// Plants every account the demo needs: the personas (each with their own
/// theme and appearance mode), a deactivated former employee, a restricted
/// contractor and the API-only CI bot. All share the demo password so any
/// of them can be used to explore the app from a different seat.
/// </summary>
internal sealed class PeopleSeedStep(IPasswordHasher hasher) : SeedStepBase
{
    private static readonly Persona CiBot =
        new("CI Bot", "ci-bot", "Service account", WorkspaceRole.Member, "default", AppearanceMode.Light);

    public override string Name => "People + preferences";
    public override int Order => 10;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        SeedTimeline timeline = context.Timeline;
        DateTimeOffset joined = timeline.OnDay(-SeedTimeline.HistoryDays - 10);

        foreach (Persona persona in NexoraStudios.Personas)
        {
            User user = Register(context, persona, joined);
            user.SetAdmin(persona.Key == "ada.lovelace", joined);
            user.RecordLogin(timeline.OnDay(-timeline.Next(0, 6)));
            AddPreferences(context, user, persona, joined);
        }

        User former = Register(context, NexoraStudios.FormerEmployee, joined);
        former.RecordLogin(timeline.OnDay(-40));
        former.Deactivate(timeline.OnDay(-35));

        DateTimeOffset contractorJoined = timeline.OnDay(-12);
        User contractor = Register(context, NexoraStudios.Contractor, contractorJoined);
        contractor.SetRestricted(true, contractorJoined);
        contractor.RecordLogin(timeline.OnDay(-1));
        AddPreferences(context, contractor, NexoraStudios.Contractor, contractorJoined);

        Register(context, CiBot, joined);

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.Users.Count} users (1 deactivated, 1 restricted, 1 service account) and {context.UserPreferences.Count} preference rows.");
        return Task.CompletedTask;
    }

    private User Register(SeedContext context, Persona persona, DateTimeOffset at)
    {
        User user = User.Register(
            UserId.New(),
            EmailAddress.Create(persona.Email).Value,
            DisplayName.Create(persona.DisplayName).Value,
            hasher.Hash(PasswordGenerator.DemoPassword()),
            at).Value;
        user.MarkEmailVerified(at);
        context.Track(context.Users, user);
        return user;
    }

    private static void AddPreferences(SeedContext context, User user, Persona persona, DateTimeOffset at)
    {
        UserPreferences preferences = UserPreferences.Create(user.Id, persona.Theme, persona.Mode, at).Value;
        context.Add(preferences);
        context.UserPreferences.Add(preferences);
    }
}
