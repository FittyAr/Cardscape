using Cardscape.Domain.Notifications;
using Cardscape.Domain.Security;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants a handful of in-app notifications (assigned
/// to a card, mentioned in a comment, due soon) and five
/// demo API tokens so the Web UI's "API tokens" page has
/// something to list.</summary>
public sealed class NotificationsAndTokensSeedStep : SeedStepBase
{
    public override string Name => "Notifications + API tokens";
    public override int Order => 100;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(505);
        int totalNotifications = 0;

        foreach (User user in context.Users)
        {
            List<Card> userCards = context.Cards
                .Where(c => context.CardMembers.Any(cm => cm.CardId.Value == c.Id.Value && cm.UserId == user.Id.Value))
                .ToList();

            for (int n = 0; n < 3; n++)
            {
                NotificationKind kind = (NotificationKind)(n % 5);
                string payload = kind switch
                {
                    NotificationKind.AssignedToCard => $"{{\"cardId\":\"{(userCards.Count > 0 ? userCards[0].Id.Value : Guid.Empty):D}\",\"cardTitle\":\"Sample card\"}}",
                    NotificationKind.Mentioned => $"{{\"commentId\":\"{Guid.NewGuid():D}\",\"preview\":\"Hey @{user.DisplayName.Value}…\"}}",
                    NotificationKind.DueSoon => $"{{\"cardId\":\"{(userCards.Count > 0 ? userCards[0].Id.Value : Guid.Empty):D}\",\"hoursRemaining\":24}}",
                    NotificationKind.Overdue => $"{{\"cardId\":\"{(userCards.Count > 0 ? userCards[0].Id.Value : Guid.Empty):D}\",\"daysOverdue\":2}}",
                    _ => "{\"info\":\"You were added as a member of the workspace.\"}"
                };

                Notification notification = Notification.Create(
                    user.Id.Value,
                    kind,
                    payload,
                    now.AddMinutes(-random.Next(0, 60 * 24 * 7)));
                if (random.NextDouble() < 0.3)
                {
                    notification.MarkRead(now.AddMinutes(-random.Next(0, 60)));
                }

                context.Db.Notifications.Add(notification);
                context.Notifications.Add(notification);
                totalNotifications++;
            }

            // 5. Mint a per-user API token (read+write) and
            //    also a read-only one. The plaintext never
            //    leaves the demo; we only persist the hash.
            string readToken = Generators.PasswordGenerator.RandomUrlSafeToken(32);
            string writeToken = Generators.PasswordGenerator.RandomUrlSafeToken(32);
            string readHash = Generators.PasswordGenerator.Sha256Hex(readToken);
            string writeHash = Generators.PasswordGenerator.Sha256Hex(writeToken);
            string readPrefix = Generators.PasswordGenerator.Prefix(readToken, ApiToken.SecretPrefixLength);
            string writePrefix = Generators.PasswordGenerator.Prefix(writeToken, ApiToken.SecretPrefixLength);

            Result<ApiToken> readApiToken = ApiToken.Create(
                user.Id,
                ApiTokenName.Create($"{user.DisplayName.Value} read token").Value,
                readHash, readPrefix,
                ApiTokenScopes.Create(new[] { "read" }).Value,
                expiresAt: null,
                at: now);
            if (readApiToken.IsSuccess)
            {
                context.Db.ApiTokens.Add(readApiToken.Value);
                context.ApiTokens.Add(readApiToken.Value);
            }

            Result<ApiToken> writeApiToken = ApiToken.Create(
                user.Id,
                ApiTokenName.Create($"{user.DisplayName.Value} write token").Value,
                writeHash, writePrefix,
                ApiTokenScopes.Create(new[] { "read", "write" }).Value,
                expiresAt: now.AddMonths(6),
                at: now);
            if (writeApiToken.IsSuccess)
            {
                context.Db.ApiTokens.Add(writeApiToken.Value);
                context.ApiTokens.Add(writeApiToken.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {totalNotifications} notifications and {context.ApiTokens.Count} API tokens.");
        return Task.CompletedTask;
    }
}
