using System.ComponentModel.DataAnnotations;

namespace Cardscape.Contracts.Settings;

/// <summary>
/// Instance-wide settings an administrator can change at runtime. Every
/// value here is honoured by the running application; configuration that
/// only takes effect at startup (database, JWT, CORS, Redis, telemetry…)
/// stays in appsettings / environment variables and is reported read-only
/// through <see cref="RuntimeConfigurationEntry"/>.
/// <para>
/// The sections are records so the admin UI can detect unsaved changes by
/// value equality, and carry DataAnnotations so the same rules validate
/// the form in the browser and the request on the server.
/// </para>
/// </summary>
public sealed record SystemSettings
{
    public GeneralSettings General { get; set; } = new();

    public AccessSettings Access { get; set; } = new();

    public NoticeSettings Notices { get; set; } = new();

    public LimitSettings Limits { get; set; } = new();

    public AiSettings Ai { get; set; } = new();

    public SeederSettings Seeder { get; set; } = new();

    /// <summary>Deep copy, so an editor can change a draft without touching the saved snapshot.</summary>
    public SystemSettings DeepCopy() => this with
    {
        General = General with { },
        Access = Access with { },
        Notices = Notices with { },
        Limits = Limits with { },
        Ai = Ai with { },
        Seeder = Seeder with { },
    };

    /// <summary>Validates every section; an empty list means the settings are valid.</summary>
    public IReadOnlyList<ValidationResult> Validate()
    {
        List<ValidationResult> results = [];
        foreach ((string section, object value) in Sections())
        {
            List<ValidationResult> sectionResults = [];
            Validator.TryValidateObject(value, new ValidationContext(value), sectionResults, validateAllProperties: true);
            results.AddRange(sectionResults.Select(result => new ValidationResult(
                result.ErrorMessage,
                [.. result.MemberNames.Select(member => $"{section}.{member}")])));
        }

        return results;
    }

    private IEnumerable<(string Name, object Value)> Sections() =>
    [
        (nameof(General), General),
        (nameof(Access), Access),
        (nameof(Notices), Notices),
        (nameof(Limits), Limits),
        (nameof(Ai), Ai),
        (nameof(Seeder), Seeder),
    ];
}

/// <summary>Branding and the defaults new users start with.</summary>
public sealed record GeneralSettings
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string InstanceTitle { get; set; } = "Cardscape";

    [EmailAddress, StringLength(254)]
    public string? SupportEmail { get; set; }

    /// <summary>Shown on the signed-in home page.</summary>
    [StringLength(280)]
    public string? WelcomeMessage { get; set; }

    /// <summary>Absolute URL of an image that replaces the Cardscape mark.</summary>
    [Url, StringLength(2048)]
    public string? LogoUrl { get; set; }

    /// <summary>UI language for visitors who have not chosen one yet.</summary>
    [Required, AllowedValues("es", "en")]
    public string DefaultLanguage { get; set; } = "es";

    /// <summary>Theme family for users who have not picked one (see <see cref="ThemeNames"/>).</summary>
    [Required, AllowedValues(ThemeNames.CardscapeClassic, ThemeNames.Default, ThemeNames.Humanistic,
        ThemeNames.Material, ThemeNames.Software, ThemeNames.Standard)]
    public string DefaultTheme { get; set; } = ThemeNames.CardscapeClassic;
}

/// <summary>Who may get in, and how.</summary>
public sealed record AccessSettings
{
    public bool AllowPublicRegistration { get; set; } = true;

    /// <summary>Only effective when the provider's credentials are configured on the server.</summary>
    public bool GoogleSignInEnabled { get; set; }

    public bool MicrosoftSignInEnabled { get; set; }

    public bool AppleSignInEnabled { get; set; }

    public bool SamlSignInEnabled { get; set; }
}

/// <summary>Messages broadcast to every user, and the maintenance switch.</summary>
public sealed record NoticeSettings
{
    public bool AnnouncementEnabled { get; set; }

    [StringLength(500)]
    public string? AnnouncementMessage { get; set; }

    public NoticeSeverity AnnouncementSeverity { get; set; } = NoticeSeverity.Info;

    /// <summary>While on, the API answers 503 to everyone except administrators.</summary>
    public bool MaintenanceEnabled { get; set; }

    [StringLength(500)]
    public string? MaintenanceMessage { get; set; } = "Cardscape is down for scheduled maintenance. We'll be back shortly.";
}

/// <summary>Quotas enforced when content is created. Zero means unlimited.</summary>
public sealed record LimitSettings
{
    [Range(0, 1_000)]
    public int MaxWorkspacesPerUser { get; set; }

    [Range(0, 10_000)]
    public int MaxBoardsPerWorkspace { get; set; }

    [Range(1, 90)]
    public int InvitationLifetimeDays { get; set; } = 7;
}

/// <summary>The OpenAI-compatible endpoint behind the card assistant features.</summary>
public sealed record AiSettings
{
    public bool Enabled { get; set; } = true;

    [Required, Url, StringLength(2048)]
    public string Endpoint { get; set; } = "http://localhost:11434/";

    [Required, StringLength(200, MinimumLength = 1)]
    public string Model { get; set; } = "llama3.2";

    /// <summary>
    /// Write-only. The server never returns the key: <c>null</c> keeps the
    /// stored key, an empty string removes it, anything else replaces it.
    /// </summary>
    [StringLength(1024)]
    public string? ApiKey { get; set; }

    /// <summary>Read-only. Whether a key is stored on the server.</summary>
    public bool HasApiKey { get; set; }

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    [Range(64, 32_768)]
    public int MaxTokens { get; set; } = 1024;
}

/// <summary>Development-only demo data loader (see the Seeder page).</summary>
public sealed record SeederSettings
{
    public bool Enabled { get; set; }

    /// <summary>Default for runs that do not say otherwise.</summary>
    public bool WipeBeforeSeed { get; set; }
}

public enum NoticeSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Danger = 3,
}
