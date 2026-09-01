using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions, logs them, and returns a
/// RFC 7807 Problem Details response with a 500 status code.
/// Validation errors (thrown by FluentValidation) become 400s.
/// JSON deserialisation errors (thrown when a request body
/// can't bind to a model — e.g. an invalid enum string in
/// `{"region":"us"}` against the Region enum) also become
/// 400s. Without this branch those exceptions reach the
/// generic catch and surface as 500s, hiding what is
/// actually a client-side input problem — see BETA-2-#1 in
/// test-results/BETA-TEST-REPORT.md.
///
/// Concurrency errors (DbUpdateConcurrencyException, raised
/// by EF Core when the RowVersion token on the entity
/// doesn't match the row that the UPDATE actually modified)
/// become 409 Conflicts. Every entity in the project has
/// `RowVersion` configured as a concurrency token
/// (see src/Cardscape.Infrastructure/Persistence/CardscapeDbContext.cs
/// for the convention) so this is the path any concurrent
/// modification walks — BETA-3-#1.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed for {Path}", context.Request.Path);
            await WriteProblem(context, StatusCodes.Status400BadRequest, "Validation failed", ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            // The JSON converter throws when the request body
            // can't be deserialised (wrong type, unknown
            // enum value, malformed array, etc.). Surface
            // these as 400 — they're a client-side problem,
            // not a server bug, and the rest of the API
            // contract treats bad request bodies as 400s.
            logger.LogWarning(ex, "JSON deserialisation failed for {Path}", context.Request.Path);
            await WriteProblem(
                context,
                StatusCodes.Status400BadRequest,
                "Malformed request body",
                ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            // The minimal-API binder throws this for missing
            // required query / form parameters (e.g. an
            // endpoint that expects a `?foo=` string but the
            // caller forgot to include it). Treat as 400.
            logger.LogWarning(ex, "Bad request at {Path}", context.Request.Path);
            await WriteProblem(
                context,
                StatusCodes.Status400BadRequest,
                "Bad request",
                ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // BETA-3-#1 — see test-results/BETA-TEST-REPORT.md.
            //
            // EF Core throws this when the RowVersion
            // concurrency token on the entity doesn't
            // match the row that the UPDATE actually
            // modified — i.e. another writer committed a
            // change between our SELECT and our UPDATE.
            // 409 Conflict is the semantically correct
            // response (RFC 9110 §15.5.10): the request
            // collides with the current state of the
            // resource, the client should refresh and
            // retry. The handler-level fix (catching this
            // and returning a Result.Failure) is the
            // right architectural choice long-term, but a
            // global catch here closes every existing
            // endpoint at once without touching the
            // handler surface.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ex, "Concurrency conflict at {Path}", context.Request.Path);
            }
            await WriteProblem(
                context,
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The resource was modified by another request while this one was being processed. " +
                "Reload the resource, re-apply your changes on top of the latest state, and retry.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        string body = JsonSerializer.Serialize(new
        {
            type = "about:blank",
            title,
            status = statusCode,
            detail
        });
        await context.Response.WriteAsync(body);
    }
}
