using Cardscape.Application.Abstractions.Import;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Import;

/// <summary>
/// REST endpoints for the import pipeline. Today only Trello is
/// supported; the JSON schema is documented in
/// <c>docs/extensions/02-trello-import.md</c>.
/// </summary>
public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").RequireAuthorization().WithTags("Imports");

        // Multipart upload of a Trello boards.json archive.
        // The endpoint reads the file part, pipes it through the
        // IImportService, and returns the IDs of the imported
        // boards / lists / cards / labels.
        group.MapPost("/trello", async (
            HttpRequest request,
            IImportService import,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new
                {
                    error = "imports.invalid_content_type",
                    message = "Expected a multipart/form-data upload with a 'file' part and a 'targetWorkspaceId' field."
                });
            }

            IFormCollection form = await request.ReadFormAsync(ct);
            string? workspaceIdRaw = form["targetWorkspaceId"];
            if (!Guid.TryParse(workspaceIdRaw, out Guid workspaceId))
            {
                return Results.BadRequest(new
                {
                    error = "imports.invalid_workspace",
                    message = "The 'targetWorkspaceId' form field is required and must be a GUID."
                });
            }

            IFormFile? file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new
                {
                    error = "imports.no_file",
                    message = "The 'file' form part is required and must contain a Trello boards.json payload."
                });
            }

            await using Stream stream = file.OpenReadStream();
            Result<Domain.Import.ImportResult> result = await import.ImportTrelloJsonAsync(stream, workspaceId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
