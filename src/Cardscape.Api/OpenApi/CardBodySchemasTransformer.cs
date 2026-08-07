using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cardscape.Api.OpenApi;

/// <summary>
/// Patches a handful of card request body schemas that the
/// default <c>Microsoft.AspNetCore.OpenApi</c> document
/// generator mis-renders for the project (records whose
/// primary-constructor parameters are <c>PascalCase</c>
/// GUIDs/doubles end up with the wrong property names in
/// the JSON schema). The runtime contract is unchanged:
/// we only fix the documentation so external integrators
/// that read /openapi/v1.json can call the endpoints.
///
/// The two affected schemas are:
///   - <c>MoveBody</c>  → 4 properties (listId, position,
///     newListId, newPosition), required = newListId +
///     newPosition; the generator only emits the two
///     `<c>double</c>` ones.
///   - <c>RenameBody</c>  → 2 properties (title, newTitle)
///     both nullable; the generator emits them under the
///     wrong names (name, newName).
///
/// BETA-8-API-#2 / BETA-8-API-#2 — see
/// <c>test-results/r8/r8-report.md</c>.
/// </summary>
internal sealed class CardBodySchemasTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        if (document.Components.Schemas.TryGetValue("MoveBody", out IOpenApiSchema? existingMove))
        {
            document.Components.Schemas["MoveBody"] = BuildMoveBodySchema();
        }

        if (document.Components.Schemas.TryGetValue("RenameBody", out IOpenApiSchema? existingRename))
        {
            document.Components.Schemas["RenameBody"] = BuildRenameBodySchema();
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema BuildMoveBodySchema() => new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "newListId", "newPosition" },
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["listId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Format = "uuid"
            },
            ["position"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Number | JsonSchemaType.Null,
                Format = "double"
            },
            ["newListId"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "uuid"
            },
            ["newPosition"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Number,
                Format = "double"
            }
        }
    };

    private static OpenApiSchema BuildRenameBodySchema() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["title"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null
            },
            ["newTitle"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null
            }
        }
    };
}
