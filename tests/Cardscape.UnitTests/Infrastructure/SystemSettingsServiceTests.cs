using Cardscape.Contracts.Settings;
using Cardscape.Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Infrastructure;

public sealed class SystemSettingsServiceTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), $"cardscape-settings-{Guid.NewGuid():N}");
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    private string SettingsFile => Path.Combine(_dataRoot, "system_settings.json");

    [Fact]
    public async Task GetAsync_WithoutFile_DerivesDefaultsFromStartupConfiguration()
    {
        using SystemSettingsService service = CreateService(new()
        {
            ["Ai:Endpoint"] = "https://ai.internal/",
            ["Ai:Model"] = "gpt-4o-mini",
            ["Cardscape:Seeder:Enabled"] = "true",
        });

        SystemSettings settings = await service.GetAsync(TestContext.Current.CancellationToken);

        settings.General.InstanceTitle.Should().Be("Cardscape");
        settings.Access.AllowPublicRegistration.Should().BeTrue();
        settings.Ai.Endpoint.Should().Be("https://ai.internal/");
        settings.Ai.Model.Should().Be("gpt-4o-mini");
        settings.Seeder.Enabled.Should().BeTrue();
        File.Exists(SettingsFile).Should().BeFalse("reading must not create the file");
    }

    [Fact]
    public async Task UpdateAsync_PersistsAcrossInstances_AndNormalisesText()
    {
        using (SystemSettingsService service = CreateService())
        {
            SystemSettings settings = new();
            settings.General.InstanceTitle = "  Nexora  ";
            settings.General.WelcomeMessage = "   ";
            settings.Notices.AnnouncementEnabled = true;
            settings.Notices.AnnouncementMessage = "Release on Friday";
            settings.Limits.MaxBoardsPerWorkspace = 25;

            var result = await service.UpdateAsync(settings, "ada@nexora.example", TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
        }

        using SystemSettingsService reloaded = CreateService();
        SystemSettings stored = await reloaded.GetAsync(TestContext.Current.CancellationToken);
        stored.General.InstanceTitle.Should().Be("Nexora");
        stored.General.WelcomeMessage.Should().BeNull();
        stored.Notices.AnnouncementMessage.Should().Be("Release on Friday");
        stored.Limits.MaxBoardsPerWorkspace.Should().Be(25);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidValues_FailsAndKeepsStoredSettings()
    {
        using SystemSettingsService service = CreateService();
        SystemSettings invalid = new();
        invalid.General.InstanceTitle = string.Empty;
        invalid.General.SupportEmail = "not-an-email";
        invalid.Limits.InvitationLifetimeDays = 0;

        var result = await service.UpdateAsync(invalid, "admin", TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("settings.invalid");
        result.Error.Message.Should().Contain("General.InstanceTitle").And.Contain("Limits.InvitationLifetimeDays");
        (await service.GetAsync(TestContext.Current.CancellationToken)).General.InstanceTitle.Should().Be("Cardscape");
    }

    [Fact]
    public async Task AiApiKey_IsEncryptedAtRest_NeverReturned_AndFollowsKeepReplaceClearSemantics()
    {
        using SystemSettingsService service = CreateService();
        CancellationToken ct = TestContext.Current.CancellationToken;
        SystemSettings settings = await service.GetAsync(ct);

        settings.Ai.ApiKey = "sk-secret-123";
        SystemSettings saved = (await service.UpdateAsync(settings, "admin", ct)).Value;
        saved.Ai.ApiKey.Should().BeNull();
        saved.Ai.HasApiKey.Should().BeTrue();
        (await File.ReadAllTextAsync(SettingsFile, ct)).Should().NotContain("sk-secret-123");
        (await service.GetAiApiKeyAsync(ct)).Should().Be("sk-secret-123");

        saved.Ai.Model = "other-model";
        await service.UpdateAsync(saved, "admin", ct); // ApiKey null → keep
        (await service.GetAiApiKeyAsync(ct)).Should().Be("sk-secret-123");

        saved.Ai.ApiKey = string.Empty; // empty → clear
        SystemSettings cleared = (await service.UpdateAsync(saved, "admin", ct)).Value;
        cleared.Ai.HasApiKey.Should().BeFalse();
        (await service.GetAiApiKeyAsync(ct)).Should().BeNull();
    }

    [Fact]
    public async Task GetAiApiKeyAsync_WithoutStoredKey_FallsBackToStartupConfiguration()
    {
        using SystemSettingsService service = CreateService(new() { ["Ai:ApiKey"] = "from-env" });

        (await service.GetAiApiKeyAsync(TestContext.Current.CancellationToken)).Should().Be("from-env");
    }

    [Fact]
    public async Task SmtpPassword_IsEncryptedAtRest_NeverReturned_AndSurvivesAReload()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using (SystemSettingsService service = CreateService())
        {
            SystemSettings settings = await service.GetAsync(ct);
            settings.Email.Enabled = true;
            settings.Email.Host = " smtp.example.test ";
            settings.Email.FromAddress = "boards@example.test";
            settings.Email.Password = " pass phrase ";

            SystemSettings saved = (await service.UpdateAsync(settings, "admin", ct)).Value;

            saved.Email.Host.Should().Be("smtp.example.test");
            saved.Email.Password.Should().BeNull();
            saved.Email.HasPassword.Should().BeTrue();
            (await File.ReadAllTextAsync(SettingsFile, ct)).Should().NotContain("pass phrase");
        }

        using SystemSettingsService reloaded = CreateService();
        (await reloaded.GetAsync(ct)).Email.HasPassword.Should().BeTrue();
        (await reloaded.GetSmtpPasswordAsync(ct)).Should().Be(" pass phrase ", "passwords are not trimmed");
    }

    [Fact]
    public async Task EmailDefaults_ComeFromTheSmtpStartupConfiguration()
    {
        using SystemSettingsService service = CreateService(new()
        {
            ["Smtp:Host"] = "mail.internal",
            ["Smtp:Port"] = "2525",
            ["Smtp:Username"] = "mailer",
            ["Smtp:Password"] = "from-env",
            ["Smtp:From"] = "noreply@example.test",
        });
        CancellationToken ct = TestContext.Current.CancellationToken;

        EmailSettings email = (await service.GetAsync(ct)).Email;

        email.Should().BeEquivalentTo(new
        {
            Enabled = true,
            Host = "mail.internal",
            Port = 2525,
            Username = "mailer",
            FromAddress = "noreply@example.test",
            HasPassword = false,
        });
        (await service.GetSmtpPasswordAsync(ct)).Should().Be("from-env");
    }

    [Fact]
    public async Task ResetAsync_RestoresDefaults_AndDropsTheKey()
    {
        using SystemSettingsService service = CreateService();
        CancellationToken ct = TestContext.Current.CancellationToken;
        SystemSettings settings = await service.GetAsync(ct);
        settings.General.InstanceTitle = "Custom";
        settings.Ai.ApiKey = "sk";
        await service.UpdateAsync(settings, "admin", ct);

        SystemSettings reset = await service.ResetAsync("admin", ct);

        reset.General.InstanceTitle.Should().Be("Cardscape");
        reset.Ai.HasApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task LegacyFlatFile_IsMigrated_ClampedAndRewrittenWithoutPlaintextSecrets()
    {
        Directory.CreateDirectory(_dataRoot);
        await File.WriteAllTextAsync(SettingsFile, """
            {
              "instanceTitle": "Legacy HQ",
              "allowPublicRegistration": false,
              "defaultTheme": "dark",
              "enableGoogleAuth": true,
              "enableGitHubAuth": true,
              "maintenanceModeEnabled": true,
              "maintenanceModeMessage": "Back at 18:00",
              "systemAnnouncementType": "Error",
              "maxBoardsPerWorkspace": 99999,
              "invitationExpirationDays": 14,
              "aiApiKey": "sk-legacy-plaintext",
              "allowSeederExecution": true,
              "jwtAccessTokenMinutes": 60
            }
            """, TestContext.Current.CancellationToken);
        using SystemSettingsService service = CreateService();

        SystemSettings migrated = await service.GetAsync(TestContext.Current.CancellationToken);

        migrated.General.InstanceTitle.Should().Be("Legacy HQ");
        migrated.General.DefaultTheme.Should().Be(ThemeNames.CardscapeClassic, "'dark' was a mode, not a theme");
        migrated.Access.AllowPublicRegistration.Should().BeFalse();
        migrated.Access.GoogleSignInEnabled.Should().BeTrue();
        migrated.Notices.MaintenanceEnabled.Should().BeTrue();
        migrated.Notices.MaintenanceMessage.Should().Be("Back at 18:00");
        migrated.Notices.AnnouncementSeverity.Should().Be(NoticeSeverity.Danger);
        migrated.Limits.MaxBoardsPerWorkspace.Should().Be(10_000);
        migrated.Limits.InvitationLifetimeDays.Should().Be(14);
        migrated.Seeder.Enabled.Should().BeTrue();
        migrated.Ai.HasApiKey.Should().BeTrue();
        migrated.Validate().Should().BeEmpty();
        (await service.GetAiApiKeyAsync(TestContext.Current.CancellationToken)).Should().Be("sk-legacy-plaintext");

        string rewritten = await File.ReadAllTextAsync(SettingsFile, TestContext.Current.CancellationToken);
        rewritten.Should().Contain("\"schemaVersion\": 2").And.NotContain("sk-legacy-plaintext").And.NotContain("jwtAccessTokenMinutes");
    }

    [Fact]
    public async Task CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dataRoot);
        await File.WriteAllTextAsync(SettingsFile, "{ not json", TestContext.Current.CancellationToken);
        using SystemSettingsService service = CreateService();

        SystemSettings settings = await service.GetAsync(TestContext.Current.CancellationToken);

        settings.Should().Be(service.Defaults());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    private SystemSettingsService CreateService(Dictionary<string, string?>? values = null)
    {
        Dictionary<string, string?> configuration = new(values ?? []) { ["Cardscape:DataRoot"] = _dataRoot };
        return new SystemSettingsService(
            new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(),
            _dataProtection,
            NullLogger<SystemSettingsService>.Instance);
    }
}
