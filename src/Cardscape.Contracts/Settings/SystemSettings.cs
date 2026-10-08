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

    public EmailSettings Email { get; set; } = new();

    public SeederSettings Seeder { get; set; } = new();

    /// <summary>Deep copy, so an editor can change a draft without touching the saved snapshot.</summary>
    public SystemSettings DeepCopy() => this with
    {
        General = General with { },
        Access = Access with { },
        Notices = Notices with { },
        Limits = Limits with { },
        Ai = Ai with { },
        Email = Email with { },
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
        (nameof(Email), Email),
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

    /// <summary>
    /// Email domains allowed to sign up on their own (public registration
    /// and first sign-in with an external provider), one per line or comma
    /// separated, e.g. <c>nexora.example</c>; subdomains match too. Empty
    /// allows every domain. Invitations and administrators bypass it: an
    /// explicit invitation is a decision, not a self-service sign-up.
    /// Stored as text so the settings record keeps value equality.
    /// </summary>
    [MaxLength(2000)]
    public string AllowedEmailDomains { get; set; } = string.Empty;

    /// <summary>The normalized entries of <see cref="AllowedEmailDomains"/>.</summary>
    public IReadOnlyList<string> AllowedDomainList() =>
        AllowedEmailDomains
            .Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(domain => domain.TrimStart('@', '.').ToLowerInvariant())
            .Where(domain => domain.Contains('.', StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>True when the list is empty, or the address's domain is (a subdomain of) a listed one.</summary>
    public bool IsEmailDomainAllowed(string email)
    {
        IReadOnlyList<string> allowed = AllowedDomainList();
        if (allowed.Count == 0)
        {
            return true;
        }

        int at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
        {
            return false;
        }

        string domain = email[(at + 1)..].Trim().ToLowerInvariant();
        return allowed.Any(entry => domain == entry || domain.EndsWith("." + entry, StringComparison.Ordinal));
    }

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

/// <summary>
/// Outbound SMTP server for invitation and password-reset emails. While it
/// is off (or incomplete) nothing is sent and invitation links are only
/// shown to the inviter, who delivers them by hand.
/// </summary>
public sealed record EmailSettings : IValidatableObject
{
    public bool Enabled { get; set; }

    [StringLength(253)]
    public string? Host { get; set; }

    [Range(1, 65_535)]
    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Optional; leave empty for relays that accept mail without signing in.</summary>
    [StringLength(254)]
    public string? Username { get; set; }

    /// <summary>
    /// Write-only, like <see cref="AiSettings.ApiKey"/>: <c>null</c> keeps the
    /// stored password, an empty string removes it, anything else replaces it.
    /// </summary>
    [StringLength(1024)]
    public string? Password { get; set; }

    /// <summary>Read-only. Whether a password is stored on the server.</summary>
    public bool HasPassword { get; set; }

    [EmailAddress, StringLength(254)]
    public string? FromAddress { get; set; }

    /// <summary>Display name of the sender; the instance title when empty.</summary>
    [StringLength(120)]
    public string? FromName { get; set; }

    /// <summary>
    /// Public address of this instance, used to build the links inside
    /// emails (e.g. <c>https://boards.example.com</c>). When empty the
    /// address the request arrived on is used, which is wrong behind a
    /// proxy that rewrites the host.
    /// </summary>
    [Url, StringLength(2048)]
    public string? PublicBaseUrl { get; set; }

    /// <summary>Whether enough is set to attempt delivery.</summary>
    public bool CanSend() =>
        Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            yield return new ValidationResult("The SMTP host is required to send email.", [nameof(Host)]);
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            yield return new ValidationResult("The sender address is required to send email.", [nameof(FromAddress)]);
        }
    }
}

/// <summary>How the SMTP connection is secured.</summary>
public enum SmtpSecurity
{
    /// <summary>Plain connection (local relays only).</summary>
    None = 0,

    /// <summary>Upgrade with STARTTLS, usually on port 587.</summary>
    StartTls = 1,

    /// <summary>TLS from the first byte, usually on port 465.</summary>
    SslOnConnect = 2,
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
