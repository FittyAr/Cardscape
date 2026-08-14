using Cardscape.Application.Abstractions.Import;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Import;

/// <summary>
/// REST endpoints for the import pipeline. Today only Kanban is
/// supported; the JSON schema is documented in
/// <c>docs/extensions/02-kanban-import.md</c>.
/// </summary>
public static class ImportEndpoints
{
    /// <summary>
    /// Maximum size of a Kanban boards.json upload. A real
    /// Kanban archive is well under 1 MB; 10 MB gives
    /// generous headroom for unusually large boards while
    /// keeping a single authenticated request from becoming
    /// a DoS amplifier. The same cap is also enforced inside
    /// the import service so a direct service call (e.g.
    /// from the MCP) cannot bypass the endpoint cap.
    /// </summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/imports").RequireAuthorization().WithTags("Imports");

        group.MapPost("/kanban/preview", (
            HttpRequest request,
            IImportService import,
            CancellationToken ct) => ImportKanbanAsync(request, import, previewOnly: true, ct));

        group.MapPost("/kanban/apply", (
            HttpRequest request,
            IImportService import,
            CancellationToken ct) => ImportKanbanAsync(request, import, previewOnly: false, ct));

        return app;
    }

    private static async Task<IResult> ImportKanbanAsync(
        HttpRequest request,
        IImportService import,
        bool previewOnly,
        CancellationToken ct)
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
                message = "The 'file' form part is required and must contain a Kanban boards.json payload."
            });
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.Problem(
                detail: $"Kanban boards.json exceeds the {MaxUploadBytes}-byte cap.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        await using Stream stream = file.OpenReadStream();
        Result<Domain.Import.ImportResult> result = await import.ImportKanbanJsonAsync(
            stream, workspaceId, previewOnly, ct);
        return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
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
