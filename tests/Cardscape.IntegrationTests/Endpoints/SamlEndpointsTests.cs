using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Xml.Linq;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Saml;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the SAML 2.0 SSO handler
/// added in the v1.1.0 roadmap execution (P4.2). The
/// <c>SamlAuthenticationHandler</c> intercepts the
/// <c>/saml/{slug}/{login,login-init,acs,metadata}</c>
/// routes before endpoint dispatch. These tests assert
/// the two surfaces an IdP needs to consume: the SP
/// metadata (so the IdP can import the SP's signing
/// keys + ACS URL) and the login challenge (which 302s
/// to the IdP's SingleSignOnService URL).
/// </summary>
[Collection(CardscapeApi.Name)]
public class SamlEndpointsTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public SamlEndpointsTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Metadata_ForWorkspaceWithSamlConfig_Returns200_WithValidXml()
    {
        HttpClient ownerClient = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        string slug = $"saml-md-{Guid.NewGuid():N}";
        string spEntityId = $"https://cardscape.local/saml/{slug}";
        SamlConfigResult setup = await ConfigureSamlConnectionWithInlineMetadata(
            ownerClient, slug, spEntityId,
            idpEntityId: "https://idp.test/metadata",
            ssoLocation: "https://idp.test/sso");

        HttpResponseMessage response = await ownerClient.GetAsync(
            $"saml/{setup.Slug}/metadata", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string xml = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        XDocument.Parse(xml);  // throws if not well-formed

        XDocument doc = XDocument.Parse(xml);
        XNamespace md = "urn:oasis:names:tc:SAML:2.0:metadata";
        doc.Root!.Name.LocalName.Should().Be("EntityDescriptor");
        doc.Root!.Attribute("entityID")?.Value.Should().Be(spEntityId);

        XElement? spDescriptor = doc.Root!.Element(md + "SPSSODescriptor");
        spDescriptor.Should().NotBeNull("Sustainsys.Saml2 should produce an SP descriptor.");
        XElement? acs = spDescriptor!
            .Elements(md + "AssertionConsumerService")
            .FirstOrDefault(e => e.Attribute("Binding")?.Value
                == "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
        acs.Should().NotBeNull("Sustainsys.Saml2 should advertise the HTTP-POST ACS binding.");
        acs!.Attribute("Location")?.Value
            .Should().Contain($"/saml/{setup.Slug}/");
    }

    [Fact]
    public async Task Login_ForWorkspaceWithSamlConfig_ReturnsChallenge()
    {
        // The default WebApplicationFactory client follows
        // redirects — which would point at a real DNS name
        // (idp.test) the test environment can't reach. We
        // build a client with redirects disabled so we can
        // assert the 302 to the IdP's SingleSignOnService
        // URL directly.
        HttpClient ownerClient = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        string slug = $"saml-login-{Guid.NewGuid():N}";
        string spEntityId = $"https://cardscape.local/saml/{slug}";
        string idpEntityId = "https://idp.test/metadata";
        string ssoLocation = "https://idp.test/sso";

        // The v1.2.0 audit (pass 12) added a SSRF guard on
        // the metadata URL (the previous incarnation used a
        // file:// URL via a temp file — the validator
        // rejects empty / loopback hosts now). Inline XML
        // exercises the same handler code without the
        // network round trip.
        SamlConfigResult setup = await ConfigureSamlConnectionWithInlineMetadata(
            ownerClient, slug, spEntityId, idpEntityId, ssoLocation);

        HttpResponseMessage response = await ownerClient.GetAsync(
            $"saml/{setup.Slug}/login", TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The handler issues a 302 redirect to the IdP's
        // SingleSignOnService URL.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            $"body was: {body}");
        Uri? location = response.Headers.Location;
        location.Should().NotBeNull();
        location!.Host.Should().Be("idp.test");
        location.AbsolutePath.Should().Be("/sso");
    }

    [Fact]
    public async Task Login_ForUnknownSlug_Returns501()
    {
        // BETA-2-#12 — see SamlAuthenticationHandler.cs. The
        // handler owns the /saml/{slug} protocol surface, but
        // the per-workspace IdP config is missing, so the
        // request cannot be processed. The truthful status is
        // 501, not 404 — see the BETA-2-#12 comment for why
        // 404 would hide the failure mode from operators.
        HttpClient client = _factory.CreateApiClient();
        string slug = $"missing-{Guid.NewGuid():N}";

        HttpResponseMessage response = await client.GetAsync($"saml/{slug}/login", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task AdminGet_ForOutsider_ReturnsForbiddenWithoutDisclosingMetadata()
    {
        HttpClient owner = _factory.CreateApiClient();
        AuthResponse ownerAuth = await RegisterAndLogin(owner);
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);

        string slug = $"saml-private-{Guid.NewGuid():N}";
        string secretMarker = $"private-idp-{Guid.NewGuid():N}";
        SamlConfigResult setup = await ConfigureSamlConnectionWithInlineMetadata(
            owner,
            slug,
            $"https://cardscape.local/saml/{slug}",
            $"https://{secretMarker}.test/metadata",
            $"https://{secretMarker}.test/sso");

        HttpClient outsider = _factory.CreateApiClient();
        AuthResponse outsiderAuth = await RegisterAndLogin(outsider);
        outsider.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", outsiderAuth.AccessToken);

        HttpResponseMessage response = await outsider.GetAsync(
            $"api/workspaces/{setup.WorkspaceId}/saml/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain(secretMarker);
        body.Should().NotContain(slug);
    }

    [Fact]
    public async Task AdminGet_ForOwner_ReturnsOwnConfiguration()
    {
        HttpClient owner = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(owner);
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        string slug = $"saml-owner-{Guid.NewGuid():N}";
        string idpEntityId = $"https://idp-{Guid.NewGuid():N}.test/metadata";
        SamlConfigResult setup = await ConfigureSamlConnectionWithInlineMetadata(
            owner,
            slug,
            $"https://cardscape.local/saml/{slug}",
            idpEntityId,
            "https://idp.test/sso");

        HttpResponseMessage response = await owner.GetAsync(
            $"api/workspaces/{setup.WorkspaceId}/saml/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SamlConnectionDto body = (await response.Content.ReadFromJsonAsync<SamlConnectionDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        body.WorkspaceId.Should().Be(setup.WorkspaceId);
        body.Slug.Should().Be(slug);
        body.IdpEntityId.Should().Be(idpEntityId);
        body.IdpMetadataXml.Should().Contain(idpEntityId);
    }

    [Fact]
    public async Task AdminGet_WithoutAuthentication_ReturnsUnauthorized()
    {
        HttpClient owner = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(owner);
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        SamlConfigResult setup = await ConfigureSamlConnectionWithInlineMetadata(
            owner,
            $"saml-anon-{Guid.NewGuid():N}",
            $"https://cardscape.local/saml/{Guid.NewGuid():N}",
            "https://idp.test/metadata",
            "https://idp.test/sso");

        HttpClient anonymous = _factory.CreateApiClient();
        HttpResponseMessage response = await anonymous.GetAsync(
            $"api/workspaces/{setup.WorkspaceId}/saml/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminGet_ForOwnerWithoutConfiguration_ReturnsNoContent()
    {
        HttpClient owner = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(owner);
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        WorkspaceDto workspace = await CreateWorkspace(owner, "SAML Empty Workspace");

        HttpResponseMessage response = await owner.GetAsync(
            $"api/workspaces/{workspace.Id}/saml/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task AdminGet_ForMissingWorkspace_ReturnsNotFound()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{Guid.NewGuid()}/saml/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ───────────────────────────────────────────

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"saml-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "SAML Owner", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    private static async Task<WorkspaceDto> CreateWorkspace(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest(name), TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
    }

    private static async Task<SamlConfigResult> ConfigureSamlConnection(
        HttpClient client,
        string slug,
        string spEntityId,
        string idpMetadataUrl)
    {
        string workspaceSlug = $"ws-{Guid.NewGuid():N}";
        HttpResponseMessage createWorkspace = await client.PostAsJsonAsync(
            "api/workspaces/",
            new CreateWorkspaceRequest($"SAML Workspace {workspaceSlug}"),
            TestJson.Options);
        createWorkspace.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWorkspace.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;

        HttpResponseMessage configure = await client.PostAsJsonAsync(
            $"api/workspaces/{workspace.Id}/saml/",
            new
            {
                slug,
                displayName = "Test IdP",
                idpEntityId = "https://idp.test/metadata",
                idpMetadataUrl,
                idpMetadataXml = (string?)null,
                spEntityId
            });
        configure.IsSuccessStatusCode.Should().BeTrue(
            $"ConfigureSamlConnection must succeed; body was {await configure.Content.ReadAsStringAsync()}");

        return new SamlConfigResult(slug, workspace.Id);
    }

    /// <summary>
    /// Hand-rolled SAML 2.0 IdP metadata document. The
    /// <c>SingleSignOnService</c> location is the URL the
    /// Sustainsys handler will redirect the browser to on
    /// <c>/saml/{slug}/login</c>. The
    /// <see cref="Login_ForWorkspaceWithSamlConfig_ReturnsChallenge"/>
    /// test stages this XML in a temp file and hands the
    /// <c>SamlConnection</c> a <c>file://</c> URL so the
    /// Sustainsys <c>MetadataLoader</c> (which uses
    /// <see cref="System.Net.WebClient"/>) can fetch it
    /// without a network round trip.
    /// </summary>
    private static string BuildIdpMetadataXml(string idpEntityId, string ssoLocation) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<EntityDescriptor xmlns=\"urn:oasis:names:tc:SAML:2.0:metadata\"" +
        $"              entityID=\"{idpEntityId}\">" +
        $"  <IDPSSODescriptor protocolSupportEnumeration=\"urn:oasis:names:tc:SAML:2.0:protocol\">" +
        $"    <SingleSignOnService Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect\"" +
        $"                      Location=\"{ssoLocation}\" />" +
        $"  </IDPSSODescriptor>" +
        $"</EntityDescriptor>";

    /// <summary>
    /// Configures a workspace SAML connection with inline
    /// metadata. The v1.2.0 audit (pass 12) added a
    /// <see cref="Cardscape.Domain.Webhooks.WebhookUrlValidator"/>
    /// SSRF guard on the metadata URL; the previous
    /// <c>file://</c>-based test setup is no longer
    /// accepted (the validator rejects empty / loopback
    /// hosts). Inline XML is the supported
    /// development-mode path and exercises the same
    /// code in <see cref="Cardscape.Api.Authentication.SamlAuthenticationHandler"/>
    /// without the network round trip.
    /// </summary>
    private async Task<SamlConfigResult> ConfigureSamlConnectionWithInlineMetadata(
        HttpClient client,
        string slug,
        string spEntityId,
        string idpEntityId,
        string ssoLocation)
    {
        string workspaceSlug = $"ws-{Guid.NewGuid():N}";
        HttpResponseMessage createWorkspace = await client.PostAsJsonAsync(
            "api/workspaces/",
            new CreateWorkspaceRequest($"SAML Workspace {workspaceSlug}"),
            TestJson.Options);
        createWorkspace.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWorkspace.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;

        HttpResponseMessage configure = await client.PostAsJsonAsync(
            $"api/workspaces/{workspace.Id}/saml/",
            new
            {
                slug,
                displayName = "SAML Test IdP",
                idpEntityId,
                idpMetadataUrl = string.Empty,
                idpMetadataXml = BuildIdpMetadataXml(idpEntityId, ssoLocation),
                spEntityId
            });
        configure.IsSuccessStatusCode.Should().BeTrue(
            $"ConfigureSamlConnection must succeed; body was {await configure.Content.ReadAsStringAsync()}");

        return new SamlConfigResult(slug, workspace.Id);
    }

    private sealed record SamlConfigResult(string Slug, Guid WorkspaceId);
}
