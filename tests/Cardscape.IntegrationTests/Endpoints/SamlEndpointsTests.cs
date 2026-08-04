using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Xml.Linq;
using Cardscape.Application.Authentication.DTOs;
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
        SamlConfigResult setup = await ConfigureSamlConnection(
            ownerClient, slug, spEntityId, "https://idp.test/metadata");

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

        // Sustainsys.Saml2 fetches the IdP metadata over
        // HTTP (or file://). We stage the metadata in a
        // temp file and hand the handler a file:// URL.
        string metadataFile = Path.Combine(
            Path.GetTempPath(), $"cardscape-saml-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(metadataFile,
            BuildIdpMetadataXml(idpEntityId, ssoLocation), TestContext.Current.CancellationToken);

        try
        {
            string idpMetadataUrl = new Uri(metadataFile).AbsoluteUri;

            // Create the workspace first; the SAML endpoint
            // is scoped under /api/workspaces/{workspaceId}/saml,
            // not /api/workspaces/{userId}/saml.
            string workspaceSlug = $"ws-{Guid.NewGuid():N}";
            HttpResponseMessage createWorkspace = await ownerClient.PostAsJsonAsync(
                "api/workspaces/",
                new CreateWorkspaceRequest($"SAML Workspace {workspaceSlug}"), TestContext.Current.CancellationToken);
            createWorkspace.IsSuccessStatusCode.Should().BeTrue();
            WorkspaceDto workspace = (await createWorkspace.Content.ReadFromJsonAsync<WorkspaceDto>(TestContext.Current.CancellationToken))!;

            HttpResponseMessage configure = await ownerClient.PostAsJsonAsync(
                $"api/workspaces/{workspace.Id}/saml/",
                new
                {
                    slug,
                    displayName = "SAML Test IdP",
                    idpEntityId,
                    idpMetadataUrl,
                    idpMetadataXml = (string?)null,
                    spEntityId
                }, TestContext.Current.CancellationToken);
            configure.IsSuccessStatusCode.Should().BeTrue(
                $"ConfigureSamlConnection must succeed; body was {await configure.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

            HttpResponseMessage response = await ownerClient.GetAsync(
                $"saml/{slug}/login", TestContext.Current.CancellationToken);

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
        finally
        {
            if (File.Exists(metadataFile))
            {
                File.Delete(metadataFile);
            }
        }
    }

    [Fact]
    public async Task Login_ForUnknownSlug_Returns404()
    {
        HttpClient client = _factory.CreateApiClient();
        string slug = $"missing-{Guid.NewGuid():N}";

        HttpResponseMessage response = await client.GetAsync($"saml/{slug}/login", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ───────────────────────────────────────────

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"saml-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "SAML Owner", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
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
            new CreateWorkspaceRequest($"SAML Workspace {workspaceSlug}"));
        createWorkspace.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWorkspace.Content.ReadFromJsonAsync<WorkspaceDto>())!;

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

    private sealed record SamlConfigResult(string Slug, Guid WorkspaceId);
}
