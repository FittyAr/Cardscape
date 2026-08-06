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
