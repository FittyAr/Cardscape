using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Domain.Integrations.GitHub;
using Cardscape.Domain.Integrations.GoogleCalendar;
using Cardscape.Domain.Integrations.InboundEmail;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Integrations.Slack;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants a row per external integration the demo
/// workspace connects to: Slack, GitHub, Google Calendar, Google
/// Drive, and an inbound email address. Every row uses the
/// domain's public factory so the integration-specific
/// validations (URL shape, repo full name, event kinds) all
/// run.</summary>
internal sealed class IntegrationsSeedStep(ISecretProtector secretProtector) : SeedStepBase
{
    private static readonly string[] ReadScopes = ["read"];
    private static readonly string[] ReadWriteScopes = ["read", "write"];
    private static readonly string[] RedirectUris =
        ["https://cli.nexora.example/callback", "http://localhost:8765/callback"];

    public override string Name => "Integrations (Slack, GitHub, Google, inbound email)";
    public override int Order => 110;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;

        // 1. Slack: one workspace + one channel per board.
        string slackBotToken = Generators.PasswordGenerator.RandomUrlSafeToken(32);
        string protectedSlackBotToken = secretProtector.Protect(slackBotToken);

        Result<SlackWorkspace> slack = SlackWorkspace.Connect(
            SlackWorkspaceId.New(),
            context.WorkspaceId,
            "T0NEXORA",
            "Nexora Studios",
            protectedSlackBotToken,
            now);
        if (slack.IsSuccess)
        {
            context.Add(slack.Value);
            context.SlackWorkspaces.Add(slack.Value);

            int idx = 0;
            foreach (Board board in context.Boards)
            {
                Result<SlackChannel> ch = SlackChannel.Link(
                    SlackChannelId.New(),
                    slack.Value.Id,
                    board.Id,
                    $"C0NEXORA{idx:D2}",
                    $"#{board.Name.Value.ToLowerInvariant().Replace(' ', '-')}",
                    SlackEventTypes.All,
                    now);
                if (ch.IsSuccess)
                {
                    context.Add(ch.Value);
                    context.SlackChannels.Add(ch.Value);
                }
                idx++;
            }
        }

        // 2. GitHub: a repo link per Engineering board.
        Board engineering = context.Boards.FirstOrDefault(b => b.Name.Value == "Engineering") ?? context.Boards[0];
        Result<GitHubRepoLink> gh = GitHubRepoLink.Link(
            GitHubRepoLinkId.New(),
            engineering.Id,
            "nexora-studios/cardscape",
            GitHubEventTypes.All,
            now);
        if (gh.IsSuccess)
        {
            context.Add(gh.Value);
            context.GitHubRepoLinks.Add(gh.Value);

            // A handful of PR links so the card detail page
            // shows the badge.
            List<Card> engCards = context.Cards
                .Where(c => context.Lists.Any(l => l.Id.Value == c.ListId.Value && l.BoardId.Value == engineering.Id.Value))
                .Take(5)
                .ToList();
            int prNumber = 1000;
            foreach (Card card in engCards)
            {
                Result<GitHubPullRequestLink> pr = GitHubPullRequestLink.Create(
                    card.Id,
                    "nexora-studios/cardscape",
                    prNumber++,
                    $"https://github.com/nexora-studios/cardscape/pull/{prNumber - 1}",
                    now.AddDays(-1));
                if (pr.IsSuccess)
                {
                    context.Add(pr.Value);
                    context.GitHubPullRequestLinks.Add(pr.Value);
                }
            }
        }

        // 3. Google Calendar: one connection per active user.
        foreach (User user in context.Users.Take(5))
        {
            string encrypted = Generators.PasswordGenerator.RandomUrlSafeToken(48);
            Result<GoogleCalendarConnection> gcal = GoogleCalendarConnection.Establish(
                GoogleCalendarConnectionId.New(),
                user.Id.Value,
                context.WorkspaceId,
                user.Email.Value,
                encrypted,
                "primary",
                now);
            if (gcal.IsSuccess)
            {
                gcal.Value.RecordSyncSuccess(now.AddDays(-1));
                context.Db.GoogleCalendarConnections.Add(gcal.Value);
                context.GoogleCalendarConnections.Add(gcal.Value);
            }
        }

        // 4. Inbound email: one address per board that points
        //    at the "Doing" list (so inbound email becomes a
        //    card in the right place).
        foreach (Board board in context.Boards)
        {
            BoardList? doingList = context.Lists.FirstOrDefault(l => l.BoardId.Value == board.Id.Value && l.Name.Value == "Doing");
            if (doingList is null)
            {
                continue;
            }

            string addr = $"board-{board.Id.Value:N}@in.nexora.example";
            Result<InboundEmailAddress> inbound = InboundEmailAddress.Register(
                InboundEmailAddressId.New(),
                context.WorkspaceId,
                addr,
                doingList.Id,
                $"{board.Name.Value} intake",
                now);
            if (inbound.IsSuccess)
            {
                context.Add(inbound.Value);
                context.InboundEmailAddresses.Add(inbound.Value);
            }
        }

        // 6. OAuth apps: two demo apps so the OAuth app
        //    management page has something to show.
        User owner = context.Users[0];
        string clientSecretHash = Generators.PasswordGenerator.Sha256Hex(Generators.PasswordGenerator.RandomUrlSafeToken(32));
        Result<OAuthApp> oa = OAuthApp.Register(
            OAuthAppId.New(),
            "Cardscape CLI",
            "cli-cardscape-demo",
            clientSecretHash,
            owner.Id.Value,
            ReadWriteScopes,
            RedirectUris,
            now);
        if (oa.IsSuccess)
        {
            context.Db.OAuthApps.Add(oa.Value);
            context.OAuthApps.Add(oa.Value);

            // One auth code and one access token per OAuth
            // app, so the API has at least one of each in the
            // table to render.
            string codePlaintext = Generators.PasswordGenerator.RandomUrlSafeToken(24);
            string codeHash = Generators.PasswordGenerator.Sha256Hex(codePlaintext);
            Result<OAuthAuthorizationCode> code = OAuthAuthorizationCode.Issue(
                OAuthAuthorizationCodeId.New(),
                oa.Value.Id,
                owner.Id,
                "https://cli.nexora.example/callback",
                codeHash,
                ReadScopes,
                now.AddMinutes(5),
                now);
            if (code.IsSuccess)
            {
                context.Db.OAuthAuthorizationCodes.Add(code.Value);
                context.OAuthAuthorizationCodes.Add(code.Value);
            }

            string accessTokenPlaintext = Generators.PasswordGenerator.RandomUrlSafeToken(32);
            string accessTokenHash = Generators.PasswordGenerator.Sha256Hex(accessTokenPlaintext);
            Result<OAuthAccessToken> access = OAuthAccessToken.Issue(
                OAuthAccessTokenId.New(),
                oa.Value.Id,
                owner.Id,
                accessTokenHash,
                ReadWriteScopes,
                now.AddDays(30),
                now);
            if (access.IsSuccess)
            {
                context.Db.OAuthAccessTokens.Add(access.Value);
                context.OAuthAccessTokens.Add(access.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.SlackWorkspaces.Count} Slack workspaces ({context.SlackChannels.Count} channels), " +
            $"{context.GitHubRepoLinks.Count} GitHub repo links ({context.GitHubPullRequestLinks.Count} PR links), " +
            $"{context.GoogleCalendarConnections.Count} Google Calendar, " +
            $"{context.InboundEmailAddresses.Count} inbound email addresses, and {context.OAuthApps.Count} OAuth apps.");
        return Task.CompletedTask;
    }
}
