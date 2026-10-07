using System.Text.Json;
using Cardscape.Contracts.Settings;

namespace Cardscape.Infrastructure.Settings;

/// <summary>
/// Upgrades the pre-v2 <c>system_settings.json</c> (one flat object with
/// ~260 keys, most of which nothing read) to <see cref="SystemSettings"/>.
/// Only the values the application honours are carried over; numbers are
/// clamped to the new validation ranges so the result is always valid.
/// </summary>
internal static class LegacySettingsMigrator
{
    public static (SystemSettings Settings, string? AiApiKey) Migrate(JsonElement legacy, SystemSettings defaults)
    {
        SystemSettings settings = defaults.DeepCopy();
        var source = new LegacyDocument(legacy);

        settings.General.InstanceTitle = source.String("instanceTitle") ?? settings.General.InstanceTitle;
        settings.General.SupportEmail = source.String("supportEmail");
        settings.General.WelcomeMessage = source.String("welcomeMessage");
        settings.General.LogoUrl = source.String("customLogoUrl");
        settings.General.DefaultLanguage = source.String("defaultLanguage") is "en" ? "en" : "es";
        settings.General.DefaultTheme = source.String("defaultTheme") is { } theme && ThemeNames.All.Contains(theme)
            ? theme
            : settings.General.DefaultTheme;

        settings.Access.AllowPublicRegistration = source.Bool("allowPublicRegistration") ?? settings.Access.AllowPublicRegistration;
        settings.Access.GoogleSignInEnabled = source.Bool("enableGoogleAuth") ?? false;
        settings.Access.MicrosoftSignInEnabled = source.Bool("enableMicrosoftAuth") ?? false;
        settings.Access.AppleSignInEnabled = source.Bool("enableAppleAuth") ?? false;
        settings.Access.SamlSignInEnabled = source.Bool("samlSsoEnabled") ?? false;

        settings.Notices.AnnouncementEnabled = source.Bool("systemAnnouncementEnabled") ?? false;
        settings.Notices.AnnouncementMessage = source.String("systemAnnouncementMessage");
        settings.Notices.AnnouncementSeverity = source.String("systemAnnouncementType")?.ToLowerInvariant() switch
        {
            "success" => NoticeSeverity.Success,
            "warning" => NoticeSeverity.Warning,
            "error" or "danger" => NoticeSeverity.Danger,
            _ => NoticeSeverity.Info,
        };
        settings.Notices.MaintenanceEnabled = source.Bool("maintenanceModeEnabled") ?? false;
        settings.Notices.MaintenanceMessage = source.String("maintenanceModeMessage") ?? settings.Notices.MaintenanceMessage;

        settings.Limits.MaxWorkspacesPerUser = source.Int("maxWorkspacesPerUser", 0, 1_000) ?? 0;
        settings.Limits.MaxBoardsPerWorkspace = source.Int("maxBoardsPerWorkspace", 0, 10_000) ?? 0;
        settings.Limits.InvitationLifetimeDays = source.Int("invitationExpirationDays", 1, 90) ?? settings.Limits.InvitationLifetimeDays;

        settings.Ai.Enabled = source.Bool("aiEnabled") ?? settings.Ai.Enabled;
        settings.Ai.Endpoint = source.String("aiEndpoint") ?? settings.Ai.Endpoint;
        settings.Ai.Model = source.String("aiModel") ?? settings.Ai.Model;
        settings.Ai.TimeoutSeconds = source.Int("aiTimeoutSeconds", 5, 300) ?? settings.Ai.TimeoutSeconds;
        settings.Ai.MaxTokens = source.Int("aiMaxTokens", 64, 32_768) ?? settings.Ai.MaxTokens;

        settings.Seeder.Enabled = (source.Bool("seederEnabled") ?? false) || (source.Bool("allowSeederExecution") ?? false) || settings.Seeder.Enabled;
        settings.Seeder.WipeBeforeSeed = source.Bool("seederWipeBeforeSeed") ?? settings.Seeder.WipeBeforeSeed;

        return (settings, source.String("aiApiKey"));
    }

    /// <summary>Tolerant, case-insensitive reader over the legacy flat object.</summary>
    private readonly struct LegacyDocument(JsonElement root)
    {
        public string? String(string name) =>
            Find(name) is { ValueKind: JsonValueKind.String } value && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : null;

        public bool? Bool(string name) => Find(name)?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        public int? Int(string name, int min, int max) =>
            Find(name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number)
                ? Math.Clamp(number, min, max)
                : null;

        private JsonElement? Find(string name)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.NameEquals(name) || string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            return null;
        }
    }
}
