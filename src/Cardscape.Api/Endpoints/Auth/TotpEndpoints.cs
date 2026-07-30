using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Cardscape.Api.Endpoints.Auth;

/// <summary>
/// REST endpoints for the 2FA / TOTP lifecycle:
/// <list type="bullet">
///   <item><c>POST /api/auth/2fa/enroll</c> — returns the
///         <c>otpauth://</c> URL to embed in a QR code, the
///         cleartext base32 secret, and the recovery codes
///         the user must save.</item>
///   <item><c>POST /api/auth/2fa/verify</c> — verifies a
///         6-digit TOTP code (or a recovery code) the user
///         submits alongside a sensitive action.</item>
///   <item><c>POST /api/auth/2fa/disable</c> — turns 2FA
///         off. Requires a valid TOTP / recovery code so a
///         stolen session cannot silently remove it.</item>
///   <item><c>GET /api/auth/2fa/status</c> — returns the
///         current enrolment state (used by the Web UI
///         settings page).</item>
/// </list>
/// </summary>
public static class TotpEndpoints
{
    public static IEndpointRouteBuilder MapTotpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/2fa").RequireAuthorization().WithTags("Auth");

        group.MapGet("/status", async (
            ITotpService totp,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            var status = await totp.GetStatusAsync(currentUser.Id, ct);
            return Results.Ok(status);
        });

        group.MapPost("/enroll", async (
            ITotpService totp,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            var result = await totp.EnrollAsync(currentUser.Id, ct);
            return result.IsSuccess
                ? Results.Ok(new TotpEnrollmentResponse(
                    result.Value.CredentialId.Value,
                    result.Value.Secret,
                    result.Value.QrCodeUrl,
                    result.Value.RecoveryCodes))
                : Results.Problem(
                    title: result.Error.Code,
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapPost("/verify", async (
            [FromBody] TotpVerifyRequest body,
            ITotpService totp,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            var codeResult = await totp.VerifyAsync(currentUser.Id, body.Code, ct);
            if (codeResult.IsSuccess)
            {
                return Results.Ok(new { valid = true });
            }

            var recoveryResult = await totp.ConsumeRecoveryCodeAsync(currentUser.Id, body.Code, ct);
            if (recoveryResult.IsSuccess)
            {
                return Results.Ok(new { valid = true, used_recovery_code = true });
            }

            return Results.Problem(
                title: codeResult.Error.Code,
                detail: codeResult.Error.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        });

        group.MapPost("/disable", async (
            [FromBody] TotpDisableRequest body,
            ITotpService totp,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            var result = await totp.DisableAsync(currentUser.Id, body.Code, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(
                    title: result.Error.Code,
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
        });

        return app;
    }
}

/// <summary>Body for <c>POST /api/auth/2fa/verify</c>.</summary>
public sealed record TotpVerifyRequest(string Code);

/// <summary>Body for <c>POST /api/auth/2fa/disable</c>.</summary>
public sealed record TotpDisableRequest(string Code);

/// <summary>Response for <c>POST /api/auth/2fa/enroll</c>.</summary>
public sealed record TotpEnrollmentResponse(
    Guid CredentialId,
    string Secret,
    string QrCodeUrl,
    IReadOnlyList<string> RecoveryCodes);
