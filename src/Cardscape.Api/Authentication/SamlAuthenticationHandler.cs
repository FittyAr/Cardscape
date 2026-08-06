using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saml2CommandResult = Sustainsys.Saml2.WebSso.CommandResult;
using Saml2ConfigurationOptions = Sustainsys.Saml2.Configuration.Options;
using Saml2EntityId = Sustainsys.Saml2.Metadata.EntityId;
using Saml2HttpRequestData = Sustainsys.Saml2.WebSso.HttpRequestData;
using Saml2IdentityProvider = Sustainsys.Saml2.IdentityProvider;
using Saml2Options = Sustainsys.Saml2.AspNetCore2.Saml2Options;
using SPOptions = Sustainsys.Saml2.Configuration.SPOptions;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Per-workspace SAML 2.0 SSO handler. The 4 IdP-facing
/// endpoints under <c>/saml/{workspaceSlug}/{login,login-init,acs,metadata}</c>
/// are routed by the <see cref="HandleRequestAsync"/> hook
/// (which runs before endpoint dispatch in the ASP.NET Core
/// authentication pipeline). The handler looks up the
/// <c>SamlConnection</c> by slug, builds a Sustainsys
/// <see cref="Saml2ConfigurationOptions"/> on the fly, and
/// delegates the protocol work to the Sustainsys
/// <c>SignInCommand</c>, <c>AcsCommand</c>, and
/// <c>MetadataCommand</c> types.
///
/// On successful ACS the SAML NameID is mapped to a Cardscape
/// <c>User</c> via <see cref="IExternalLoginService"/> (the
/// <c>ExternalProvider.Saml</c> provider) and a JWT is
/// returned via a redirect URL fragment that mirrors the
/// OAuth external-login contract.
/// </summary>
public sealed class SamlAuthenticationHandler
    : AuthenticationHandler<Saml2Options>, IAuthenticationRequestHandler
{
    public const string SchemeName = "Saml";
    public const string SamlCallbackPath = "/saml/callback";

    private readonly ISamlConnectionRepository _connections;
    private readonly IExternalLoginService _externalLogins;
    private readonly ITokenService _tokens;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;

    public SamlAuthenticationHandler(
        IOptionsMonitor<Saml2Options> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISamlConnectionRepository connections,
        IExternalLoginService externalLogins,
        ITokenService tokens,
        IUserRepository users,
        IClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _connections = connections;
        _externalLogins = externalLogins;
        _tokens = tokens;
        _users = users;
        _clock = clock;
        _configuration = configuration;
    }

    public async Task<bool> HandleRequestAsync()
    {
        string path = Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/saml/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return false;
        }

        string slug = segments[1];
        string action = segments[2].ToLowerInvariant();

        Domain.Authentication.Saml.SamlConnection? connection =
            await _connections.FindBySlugAsync(slug, Context.RequestAborted);
        if (connection is null || !connection.IsActive)
        {
            // BETA-2-#12 — see test-results/BETA-TEST-REPORT.md.
            //
            // The original WriteNotConfigured() helper wrote a
            // 404. That hides the failure mode: the operator
            // dashboard surfaces the URL space as "endpoint
            // missing" and spends the next hour wondering why
            // the static `/saml/{slug}/login` fallback never
            // runs. The truthful status is 501 — the SAML
            // handler IS registered (so the fallback endpoint
            // is correctly bypassed), but no IdP is configured
            // for this workspace, so the request cannot be
            // processed. The 501 makes the failure mode
            // self-explanatory in the operator log.
            await WriteProblem(
                StatusCodes.Status501NotImplemented,
                "saml.not_configured",
                $"No active SAML connection is configured for workspace slug '{slug}'. " +
                "Configure one via the workspace SAML admin endpoint " +
                $"(POST /api/workspaces/{{workspaceId}}/saml) or remove the /saml/{slug}/* routes from your reverse proxy.");
            return true;
        }

        try
        {
            return action switch
            {
                "login" or "login-init" => await HandleLogin(connection, slug),
                "acs" => await HandleAcs(connection, slug),
                "metadata" => await HandleMetadata(connection, slug),
                _ => await WriteNotFound(slug, action)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SAML handler error for {Slug}/{Action}", slug, action);
            await WriteProblem(StatusCodes.Status500InternalServerError,
                "saml.handler_error", "SAML handler error.");
            return true;
        }
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;

    private async Task<bool> HandleLogin(
        Domain.Authentication.Saml.SamlConnection connection, string slug)
    {
        (Saml2ConfigurationOptions options, Saml2IdentityProvider idp) =
            BuildSustainsysOptions(connection, slug);

        Saml2HttpRequestData requestData = BuildRequestData();
        string returnPath = $"/saml/{slug}/login-init";

        Saml2CommandResult result = Sustainsys.Saml2.WebSso.SignInCommand.Run(
            idp.EntityId,
            returnPath,
            requestData,
            options,
            new Dictionary<string, string> { ["workspace_slug"] = slug });

        if (result.Location is null)
        {
            Logger.LogWarning("SAML SignIn returned no Location for {Slug}.", slug);
            await WriteProblem(StatusCodes.Status502BadGateway, "saml.signin_no_location",
                "Identity provider did not return a redirect URL.");
            return true;
        }

        Response.StatusCode = (int)HttpStatusCode.Redirect;
        Response.Headers.Location = result.Location.OriginalString;
        return true;
    }

    private async Task<bool> HandleAcs(
        Domain.Authentication.Saml.SamlConnection connection, string slug)
    {
        (Saml2ConfigurationOptions options, _) =
            BuildSustainsysOptions(connection, slug);

        Saml2HttpRequestData requestData = BuildRequestData();
        Saml2CommandResult result;
        try
        {
            result = (Saml2CommandResult)Sustainsys.Saml2.WebSso.CommandFactory
                .GetCommand(Sustainsys.Saml2.WebSso.CommandFactory.AcsCommandName)
                .Run(requestData, options);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "SAML ACS processing failed for {Slug}.", slug);
            await WriteProblem(StatusCodes.Status400BadRequest, "saml.acs_failed",
                "SAML assertion processing failed.");
            return true;
        }

        if (result.Principal is null
            || result.Principal.Identity is null
            || !result.Principal.Identity.IsAuthenticated)
        {
            await WriteProblem(StatusCodes.Status401Unauthorized, "saml.no_principal",
                "SAML response did not contain an authenticated principal.");
            return true;
        }

        string? nameId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? result.Principal.FindFirstValue("sub")
            ?? result.Principal.FindFirstValue(Saml2ClaimTypes.NameId);
        if (string.IsNullOrWhiteSpace(nameId))
        {
            await WriteProblem(StatusCodes.Status400BadRequest, "saml.no_name_id",
                "SAML response did not contain a NameID.");
            return true;
        }

        var subjectResult = SubjectId.Create(nameId);
        if (subjectResult.IsFailure)
        {
            await WriteProblem(StatusCodes.Status400BadRequest,
                subjectResult.Error.Code, subjectResult.Error.Message);
            return true;
        }

        string email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
        string displayName = result.Principal.FindFirstValue(ClaimTypes.Name)
            ?? result.Principal.FindFirstValue("displayName")
            ?? email;

        DateTimeOffset at = _clock.UtcNow;
        Result<ExternalLoginResolution> resolved = await _externalLogins.ResolveAsync(
            ExternalProvider.Saml, subjectResult.Value, email, displayName, at,
            Context.RequestAborted);
        if (resolved.IsFailure)
        {
            await WriteProblem(StatusCodes.Status400BadRequest,
                resolved.Error.Code, resolved.Error.Message);
            return true;
        }

        Domain.Members.User user = await _users.GetByIdAsync(resolved.Value.UserId, Context.RequestAborted)
            ?? throw new InvalidOperationException(
                $"External login {resolved.Value.LoginId.Value} resolved to a missing user.");

        string access = _tokens.IssueAccessToken(user, new[] { "user" });
        RefreshToken refresh = _tokens.IssueRefreshToken();

        string redirect = _configuration["Cardscape:Web:ExternalLoginRedirectUrl"]
            ?? _configuration["Web:ExternalLoginRedirectUrl"]
            ?? "/saml/callback";
        string fragment =
            $"access_token={Uri.EscapeDataString(access)}"
            + $"&refresh_token={Uri.EscapeDataString(refresh.Token)}"
            + $"&expires_at={Uri.EscapeDataString(at.AddHours(1).ToString("O"))}"
            + $"&user_id={Uri.EscapeDataString(user.Id.Value.ToString())}"
            + $"&user_email={Uri.EscapeDataString(user.Email.Value)}"
            + $"&user_name={Uri.EscapeDataString(user.DisplayName.Value)}";

        Response.StatusCode = (int)HttpStatusCode.Redirect;
        Response.Headers.Location = $"{redirect}#{fragment}";
        return true;
    }

    private async Task<bool> HandleMetadata(
        Domain.Authentication.Saml.SamlConnection connection, string slug)
    {
        (Saml2ConfigurationOptions options, _) =
            BuildSustainsysOptions(connection, slug);

        Saml2HttpRequestData requestData = BuildRequestData();
        Saml2CommandResult result = (Saml2CommandResult)Sustainsys.Saml2.WebSso.CommandFactory
            .GetCommand(Sustainsys.Saml2.WebSso.CommandFactory.MetadataCommand)
            .Run(requestData, options);

        Response.StatusCode = result.HttpStatusCode == HttpStatusCode.OK || result.HttpStatusCode == 0
            ? StatusCodes.Status200OK
            : (int)result.HttpStatusCode;
        Response.ContentType = result.ContentType ?? "application/samlmetadata+xml";
        byte[] body = string.IsNullOrEmpty(result.Content)
            ? []
            : Encoding.UTF8.GetBytes(result.Content);
        await Response.Body.WriteAsync(body, Context.RequestAborted);
        return true;
    }

    private (Saml2ConfigurationOptions, Saml2IdentityProvider) BuildSustainsysOptions(
        Domain.Authentication.Saml.SamlConnection connection, string slug)
    {
        var spOptions = new SPOptions
        {
            EntityId = new Saml2EntityId(connection.SpEntityId),
            ModulePath = $"/saml/{slug}"
        };

        var idpEntityId = new Saml2EntityId(connection.IdpEntityId);
        var idp = new Saml2IdentityProvider(idpEntityId, spOptions)
        {
            AllowUnsolicitedAuthnResponse = true
        };

        if (!string.IsNullOrWhiteSpace(connection.IdpMetadataXml))
        {
            // The full EntityDescriptor parser lives in a
            // separate Sustainsys.Saml2.Metadata assembly
            // that the AspNetCore2 package does not bring
            // in. We pull the SingleSignOnService URL out
            // of the metadata XML with a small XmlReader
            // loop and hand it to the IdP directly. The
            // AuthnRequest generated by Sustainsys targets
            // this URL; signature validation is not
            // exercised by the metadata endpoint.
            TryApplyInlineMetadata(idp, connection.IdpMetadataXml);
        }
        else if (!string.IsNullOrWhiteSpace(connection.IdpMetadataUrl))
        {
            // Sustainsys.Saml2.IdentityProvider.LoadMetadata
            // requires a signing certificate pre-configured
            // on the IdP, which the admin UI does not
            // collect today. Rather than push the operator
            // through that contract, we fetch the URL
            // ourselves and run the same inline parser.
            // HttpClient does not support file://, so we
            // branch on the scheme: file:// is read
            // directly; everything else goes through
            // HttpClient. This is the MVP path: future work
            // should switch to LoadMetadata once the
            // admin UI exposes the signing cert upload.
            try
            {
                string metadataXml = ReadMetadataFromLocation(connection.IdpMetadataUrl);
                TryApplyInlineMetadata(idp, metadataXml);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to fetch IdP metadata for {Slug}.", slug);
            }
        }

        var options = new Saml2ConfigurationOptions(spOptions);
        options.IdentityProviders.Add(idp);

        return (options, idp);
    }

    private void TryApplyInlineMetadata(
        Saml2IdentityProvider idp, string metadataXml)
    {
        try
        {
            var doc = new System.Xml.XmlDocument { XmlResolver = null };
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (var reader = System.Xml.XmlReader.Create(
                new System.IO.StringReader(metadataXml), settings))
            {
                doc.Load(reader);
            }

            System.Xml.XmlNamespaceManager nsmgr = new(doc.NameTable);
            nsmgr.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
            System.Xml.XmlElement? sso = doc.SelectSingleNode(
                "md:EntityDescriptor/md:IDPSSODescriptor/md:SingleSignOnService",
                nsmgr) as System.Xml.XmlElement;
            string? location = sso?.GetAttribute("Location");
            string? binding = sso?.GetAttribute("Binding");
            if (!string.IsNullOrWhiteSpace(location))
            {
                idp.SingleSignOnServiceUrl = new Uri(location);
            }
            if (!string.IsNullOrWhiteSpace(binding))
            {
                idp.Binding = binding.EndsWith("HTTP-POST", StringComparison.OrdinalIgnoreCase)
                    ? Sustainsys.Saml2.WebSso.Saml2BindingType.HttpPost
                    : Sustainsys.Saml2.WebSso.Saml2BindingType.HttpRedirect;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse inline IdP metadata.");
        }
    }

    private static string ReadMetadataFromLocation(string location)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeFile)
        {
            return System.IO.File.ReadAllText(uri.LocalPath);
        }

        using var http = new HttpClient();
        return http.GetStringAsync(location).GetAwaiter().GetResult();
    }

    private Saml2HttpRequestData BuildRequestData()
    {
        List<KeyValuePair<string, IEnumerable<string>>> form = [];
        if (Request.HasFormContentType)
        {
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kvp in Request.Form)
            {
                List<string> values = [.. kvp.Value.Where(v => v is not null).Select(v => v!)];
                form.Add(new KeyValuePair<string, IEnumerable<string>>(kvp.Key, values));
            }
        }

        List<KeyValuePair<string, string>> cookies = [.. Request.Cookies
            .Select(c => new KeyValuePair<string, string>(c.Key, c.Value))];

        Uri url = new($"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");

        return new Saml2HttpRequestData(
            Request.Method,
            url,
            Request.QueryString.Value ?? string.Empty,
            form,
            cookies,
            _ => []);
    }

    private async Task<bool> WriteNotConfigured(string slug)
    {
        Logger.LogInformation("SAML config not found for slug {Slug}.", slug);
        await WriteProblem(StatusCodes.Status404NotFound, "saml.not_configured",
            $"No active SAML connection for workspace slug '{slug}'.");
        return true;
    }

    private async Task<bool> WriteNotFound(string slug, string action)
    {
        await WriteProblem(StatusCodes.Status404NotFound, "saml.unknown_action",
            $"Unknown SAML action '{action}' for workspace slug '{slug}'.");
        return true;
    }

    private async Task WriteProblem(int statusCode, string code, string message)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "application/json";
        string json =
            $"{{\"error\":{{\"code\":\"{code}\",\"message\":\"{Escape(message)}\"}}}}";
        await Response.Body.WriteAsync(
            Encoding.UTF8.GetBytes(json), Context.RequestAborted);
    }

    private static string Escape(string s) => s
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r");
}

public static class Saml2ClaimTypes
{
    public const string NameId = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
}
