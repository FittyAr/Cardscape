using Cardscape.Application.Attachments;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.Attachments;

public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards/{cardId:guid}/attachments")
            .RequireAuthorization()
            .WithTags("Attachments");

        group.MapGet("/", async (Guid cardId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyList<AttachmentDto>>>(
                new ListCardAttachmentsQuery(cardId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
        });

        // BUG-A5-002 — direct multipart upload. Bounded to
        // 30 MB at the framework level so a misbehaving client
        // can't fill the storage volume with a single request.
        group.MapPost("/", async (
            Guid cardId,
            HttpRequest request,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new
                {
                    code = "attachments.multipart_required",
                    message = "Upload must use multipart/form-data."
                });
            }

            IFormCollection form = await request.ReadFormAsync(ct);
            IFormFile? file = form.Files["file"];
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new
                {
                    code = "attachments.file_required",
                    message = "A non-empty 'file' field is required."
                });
            }

            await using Stream stream = file.OpenReadStream();
            var result = await bus.InvokeAsync<Result<AttachmentDto>>(
                new UploadAttachmentCommand(
                    cardId,
                    file.FileName,
                    file.ContentType ?? "application/octet-stream",
                    file.Length,
                    stream),
                ct);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/cards/{cardId}/attachments/{result.Value.Id}",
                    result.Value)
                : MapError(result.Error);
        }).DisableAntiforgery();

        // Per-attachment operations live under a second group so
        // the {attachmentId} route value is enforced by the URL
        // shape (avoids accidental matches on the listing URL).
        var byId = app.MapGroup("/api/cards/{cardId:guid}/attachments/{attachmentId:guid}")
            .RequireAuthorization()
            .WithTags("Attachments");

        byId.MapGet("/download", async (Guid cardId, Guid attachmentId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<AttachmentDownload>>(
                new DownloadAttachmentQuery(attachmentId), ct);
            if (result.IsFailure)
            {
                return MapError(result.Error);
            }

            return Results.File(
                result.Value.Content,
                contentType: result.Value.MimeType,
                fileDownloadName: result.Value.FileName);
        });

        byId.MapDelete("/", async (Guid cardId, Guid attachmentId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<bool>>(
                new DeleteAttachmentCommand(attachmentId), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : MapError(result.Error);
        });

        return app;
    }

    private static IResult MapError(Cardscape.Domain.Common.DomainError error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Unauthenticated => Results.Unauthorized(),
        ErrorType.Validation => Results.UnprocessableEntity(new { error.Code, error.Message }),
        _ => Results.BadRequest(new { error.Code, error.Message })
    };
}
