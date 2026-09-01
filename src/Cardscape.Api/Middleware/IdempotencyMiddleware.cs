using System.IO;
using System.Security.Claims;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Idempotency;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Implements the <c>Idempotency-Key</c> HTTP header contract
/// (RFC draft "The Idempotency-Key HTTP Header Field"). The
/// table is provisioned by migration
/// <c>20260729204702_IssueIdempotencyKeys</c> and the domain
/// entity is <see cref="IdempotencyKey"/>; the half-built
/// feature was wired up in v0.7 (the table + repository exist)
/// but the HTTP middleware was never landed. BETA-3-#5
/// closes that gap.
///
/// <para>
/// Behaviour:
/// </para>
/// <list type="bullet">
///   <item>The header is only honoured on state-changing methods
///         (POST / PUT / PATCH / DELETE). GET / HEAD / OPTIONS
///         pass through untouched — they're already idempotent
///         at the HTTP semantic level.</item>
///   <item>On a miss: the middleware first reserves the unique
///         (OwnerId, KeyValue) tuple, then lets only the winner flow
///         downstream and completes the reservation with the response.</item>
///   <item>On a hit with the same request hash and the row is
///         not past its <see cref="IdempotencyKey.RetentionWindow"/>:
///         the stored response is replayed verbatim (same
///         status, same body, same Content-Type).</item>
///   <item>On a hit with a different request hash: 422
///         Unprocessable Entity with
///         <c>idempotency.key.payload_mismatch</c>.</item>
///   <item>On a hit past its expiry: the row is treated as a
///         miss and the request flows downstream.</item>
///   <item>Anonymous requests (no JWT / no API token) are not
///         accepted.</item>
/// </list>
///
/// <para>
/// Scoped-service resolution: the constructor takes only the
/// <see cref="RequestDelegate"/> and an <see cref="ILoggerFactory"/>;
/// every other dependency (<see cref="IIdempotencyKeyStore"/>,
/// <see cref="ICurrentUser"/>, <see cref="IClock"/>) is
/// resolved from <c>HttpContext.RequestServices</c> inside
/// <see cref="InvokeAsync"/>. The previous constructor tried
/// to inject them as scoped services, which ASP.NET rejects
/// at startup with "Cannot resolve scoped service from root
/// provider" — the documented constraint for any middleware
/// constructed by <c>UseMiddleware&lt;T&gt;()</c>.
/// </para>
/// </summary>
public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    ILoggerFactory loggerFactory)
{
    public const string HeaderName = "Idempotency-Key";

    private const int MaxBodyBytes = 1 * 1024 * 1024;
    private static readonly TimeSpan ReservationPollInterval = TimeSpan.FromMilliseconds(20);

    private static readonly HashSet<string> MutableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context)
    {
        ILogger logger = loggerFactory.CreateLogger<IdempotencyMiddleware>();

        if (!MutableMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        IServiceProvider services = context.RequestServices;
        IClock clock = services.GetRequiredService<IClock>();
        IIdempotencyKeyStore store = services.GetRequiredService<IIdempotencyKeyStore>();

        // BETA-4-#4 — see test-results/BETA-TEST-REPORT.md.
        //
        // The original implementation read the user id from
        // ICurrentUser.Id, but ICurrentUser is populated by the
        // AuthenticationHandler which runs in the
        // UseAuthentication() step. The middleware was placed
        // BEFORE UseAuthentication so the user was always
        // "anonymous" at this point and the Idempotency-Key
        // was silently ignored for every authenticated
        // request. The fix is two-fold: (a) move the
        // middleware in the pipeline so the JWT principal is
        // already attached to HttpContext.User, and (b) read
        // the user id directly from the NameIdentifier claim
        // so the middleware doesn't depend on the order of
        // ICurrentUser's population.
        string? rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out Guid userIdGuid))
        {
            // Anonymous or unparseable principal — pass through.
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var rawKey)
            || string.IsNullOrWhiteSpace(rawKey))
        {
            await next(context);
            return;
        }

        // Validate the key shape BEFORE buffering the body
        // so an obviously-bad key doesn't cost us 1 MB of
        // allocation.
        var keyResult = IdempotencyKeyValue.Create(rawKey);
        if (keyResult.IsFailure)
        {
            await WriteProblem(
                context,
                StatusCodes.Status400BadRequest,
                keyResult.Error.Code,
                keyResult.Error.Message);
            return;
        }

        var key = keyResult.Value;
        var ownerId = new UserId(userIdGuid);

        // Buffer the request body so we can hash it AND
        // still let the downstream pipeline read it.
        context.Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: MaxBodyBytes);
        string body = await ReadBodyAsync(context, ct: context.RequestAborted);
        context.Request.Body.Position = 0;

        string requestHash = HashRequest(context.Request.Method, context.Request.Path, body);

        IdempotencyKey reservation;
        while (true)
        {
            IdempotencyKey? existing = await store.FindAsync(ownerId, key, context.RequestAborted);
            if (existing is not null)
            {
                if (!existing.MatchesRequest(requestHash))
                {
                    logger.LogWarning(
                        "Idempotency-Key {Key} from {Owner} replayed with a different payload (path={Path})",
                        key, ownerId, context.Request.Path);
                    await WriteProblem(
                        context,
                        StatusCodes.Status422UnprocessableEntity,
                        "idempotency.key.payload_mismatch",
                        "The Idempotency-Key has already been used with a different request payload. " +
                        "Use a fresh key for a new logical operation.");
                    return;
                }

                if (!existing.IsAlive(clock.UtcNow))
                {
                    await store.ReleaseAsync(existing.Id, context.RequestAborted);
                    continue;
                }

                if (!existing.IsPending)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation(
                            "Idempotency-Key {Key} from {Owner} replayed; returning stored response (path={Path})",
                            key, ownerId, context.Request.Path);
                    }
                    await ReplayAsync(context, existing, context.RequestAborted);
                    return;
                }

                await Task.Delay(ReservationPollInterval, context.RequestAborted);
                continue;
            }

            var reservationResult = IdempotencyKey.Reserve(
                ownerId, key, requestHash, clock.UtcNow);
            reservation = reservationResult.Value;
            if (await store.TryReserveAsync(reservation, context.RequestAborted)) break;
        }

        var captured = new MemoryStream();
        Stream originalBody = context.Response.Body;
        context.Response.Body = captured;

        try
        {
            await next(context);
        }
        catch
        {
            await store.ReleaseAsync(reservation.Id, CancellationToken.None);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        int status = context.Response.StatusCode;
        if (status != IdempotencyKey.ReservationStatusCode
            && (status is >= 200 and < 300 or (>= 400 and < 500)))
        {
            string responseJson = Encoding.UTF8.GetString(captured.ToArray());
            bool completed = await store.CompleteReservationAsync(
                reservation.Id,
                status,
                responseJson,
                clock.UtcNow,
                context.RequestAborted);
            if (!completed)
            {
                logger.LogError(
                    "Lost Idempotency-Key reservation {Key} from {Owner} for {Path} before completion",
                    key, ownerId, context.Request.Path);
            }
        }
        else
        {
            await store.ReleaseAsync(reservation.Id, CancellationToken.None);
        }

        captured.Position = 0;
        await captured.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static async Task<string> ReadBodyAsync(HttpContext context, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8 * 1024];
        int read;
        long total = 0;
        while ((read = await context.Request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            total += read;
            if (total > MaxBodyBytes)
            {
                throw new BadHttpRequestException(
                    $"Request body exceeds the {MaxBodyBytes}-byte Idempotency-Key middleware cap.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            ms.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string HashRequest(string method, PathString path, string body)
    {
        var sb = new StringBuilder();
        sb.Append(method.ToUpperInvariant());
        sb.Append('|');
        sb.Append(path.Value ?? string.Empty);
        sb.Append('|');
        sb.Append(body);
        return RequestHasher.Hash(sb.ToString());
    }

    private static async Task ReplayAsync(
        HttpContext context, IdempotencyKey record, CancellationToken ct)
    {
        context.Response.StatusCode = record.ResponseStatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Idempotent-Replayed"] = "true";
        byte[] bytes = Encoding.UTF8.GetBytes(record.ResponseJson ?? string.Empty);
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, ct);
    }

    private static async Task WriteProblem(
        HttpContext context, int statusCode, string code, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        string body = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "about:blank",
            title = code,
            status = statusCode,
            detail
        });
        await context.Response.WriteAsync(body);
    }
}
