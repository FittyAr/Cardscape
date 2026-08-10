using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Authentication.RevokedTokens;
using Cardscape.Domain.Authentication.Saml;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.Members;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Plants SCIM, SAML, TOTP, ExternalLogin, and
/// PasswordReset / RevokedToken rows so the admin pages and
/// auth tables have something to display. The SCIM and
/// password-reset tokens are generated randomly; the
/// cleartexts are dropped after the SHA-256 is computed.</summary>
public sealed class EnterpriseAuthSeedStep : SeedStepBase
{
    public override string Name => "Enterprise auth (SCIM, SAML, TOTP, external logins)";
    public override int Order => 120;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(606);

        // 1. SCIM: two tokens, one for production / one for
        //    the staging tenant that the QA team uses.
        (ScimToken prod, _) = ScimToken.Issue(ScimTokenId.New(), context.WorkspaceId, "Production IdP", now);
        context.Db.ScimTokens.Add(prod);
        context.ScimTokens.Add(prod);

        (ScimToken stg, _) = ScimToken.Issue(ScimTokenId.New(), context.WorkspaceId, "Staging IdP", now);
        stg.RecordUse(now.AddDays(-3));
        context.Db.ScimTokens.Add(stg);
        context.ScimTokens.Add(stg);

        // 2. SAML connection.
        Result<SamlConnection> saml = SamlConnection.Configure(
            SamlConnectionId.New(),
            context.WorkspaceId,
            "nexora-okta",
            "Okta (production)",
            "https://nexora.okta.com",
            "https://nexora.okta.com/app/exk1q.../sso/saml/metadata",
            null,
            "https://cardscape.nexora.example/saml",
            now);
        if (saml.IsSuccess)
        {
            context.Db.SamlConnections.Add(saml.Value);
            context.SamlConnections.Add(saml.Value);
        }

        // 3. TOTP: enrolled for half the personas, with a
        //    dummy encrypted secret (real secrets come from
        //    the application layer at enrollment time).
        int enrolled = 0;
        foreach (User user in context.Users.Take(context.Users.Count / 2))
        {
            string encryptedSecret = Generators.PasswordGenerator.RandomUrlSafeToken(32);
            string recoveryCodesHash = Generators.PasswordGenerator.Sha256Hex("seed-recovery-codes");
            Result<TotpCredential> cred = TotpCredential.Enroll(
                user.Id, encryptedSecret, recoveryCodesHash, now);
            if (cred.IsSuccess)
            {
                context.Db.TotpCredentials.Add(cred.Value);
                context.TotpCredentials.Add(cred.Value);
                enrolled++;
            }
        }

        // 4. External logins: every persona has at least one
        //    external provider (Google). The admin has both
        //    Google and Microsoft.
        foreach (User user in context.Users)
        {
            SubjectId subject = SubjectId.Create($"sub-{user.Id.Value:N}").Value;
            Result<ExternalLogin> google = ExternalLogin.Link(
                user.Id, ExternalProvider.Google, subject, user.Email.Value, user.DisplayName.Value, now);
            if (google.IsSuccess)
            {
                context.Db.ExternalLogins.Add(google.Value);
                context.ExternalLogins.Add(google.Value);
            }

            if (user.Id.Value == context.WorkspaceOwnerId)
            {
                SubjectId msSubject = SubjectId.Create($"ms-{user.Id.Value:N}").Value;
                Result<ExternalLogin> ms = ExternalLogin.Link(
                    user.Id, ExternalProvider.Microsoft, msSubject, user.Email.Value, user.DisplayName.Value, now);
                if (ms.IsSuccess)
                {
                    context.Db.ExternalLogins.Add(ms.Value);
                    context.ExternalLogins.Add(ms.Value);
                }
            }
        }

        // 5. Password reset: one pending reset token for the
        //    first user (so the admin table can show
        //    outstanding resets).
        User first = context.Users[0];
        string tokenHash = Generators.PasswordGenerator.Sha256Hex(Generators.PasswordGenerator.RandomUrlSafeToken(24));
        Result<PasswordReset> reset = PasswordReset.Issue(
            first.Id, tokenHash, now.AddMinutes(-30), TimeSpan.FromHours(2),
            "127.0.0.1");
        if (reset.IsSuccess)
        {
            context.Db.PasswordResets.Add(reset.Value);
            context.PasswordResets.Add(reset.Value);
        }

        // 6. Revoked JWT: two rows, one freshly-revoked, one
        //    due to expire in the next minute.
        foreach (User user in context.Users.Take(2))
        {
            string jti = Guid.NewGuid().ToString("N");
            DateTimeOffset expiresAt = now.AddHours(random.Next(0, 24));
            if (expiresAt <= now)
            {
                expiresAt = now.AddHours(1);
            }

            Result<RevokedToken> revoked = RevokedToken.Revoke(
                jti, user.Id, now.AddMinutes(-5), expiresAt, "Lost device");
            if (revoked.IsSuccess)
            {
                context.Db.RevokedTokens.Add(revoked.Value);
                context.RevokedTokens.Add(revoked.Value);
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {context.ScimTokens.Count} SCIM tokens, {context.SamlConnections.Count} SAML connection, " +
            $"{enrolled} TOTP credentials, {context.ExternalLogins.Count} external logins, " +
            $"{context.PasswordResets.Count} password reset, and {context.RevokedTokens.Count} revoked tokens.");
        return Task.CompletedTask;
    }
}
