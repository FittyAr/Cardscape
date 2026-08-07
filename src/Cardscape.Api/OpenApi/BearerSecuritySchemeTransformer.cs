using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cardscape.Api.OpenApi;

/// <summary>
/// Adds the JWT <c>Bearer</c> security scheme to the OpenAPI
/// document emitted by <c>Microsoft.AspNetCore.OpenApi</c>. The
/// scheme is the same one the API actually accepts on every
/// protected endpoint (<c>Authorization: Bearer &lt;jwt&gt;</c>).
/// Endpoints that go through <c>RequireAuthorization()</c> are
/// automatically annotated with a <c>security: [{ Bearer: [] }]</c>
/// requirement by the framework, so once the scheme is registered
/// here Scalar renders the "Authorize" button and the padlock on
/// every locked endpoint without any further wiring.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT bearer token issued by POST /api/auth/login. " +
                          "Send as `Authorization: Bearer <token>`."
        };

        return Task.CompletedTask;
    }
}
