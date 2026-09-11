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
///   <item><c>POST /api/auth/2fa/confirm</c> — activates a
///         pending enrollment after proving the authenticator secret.</item>
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
        }).Produces<TotpStatus>();

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
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces<TotpEnrollmentResponse>();

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
                return Results.Ok(new TotpVerificationResponse(true, false));
            }

            var recoveryResult = await totp.ConsumeRecoveryCodeAsync(currentUser.Id, body.Code, ct);
            if (recoveryResult.IsSuccess)
            {
                return Results.Ok(new TotpVerificationResponse(true, true));
            }

            return DomainErrorResults.ToProblem(DomainError.Unauthenticated(
                codeResult.Error.Code,
                codeResult.Error.Message));
        }).Produces<TotpVerificationResponse>();

        group.MapPost("/confirm", async (
            [FromBody] TotpVerifyRequest body,
            ITotpService totp,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (currentUser.Id is null)
            {
                return Results.Unauthorized();
            }

            var result = await totp.ConfirmEnrollmentAsync(currentUser.Id, body.Code, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

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
                : DomainErrorResults.ToProblem(result.Error);
        }).Produces(StatusCodes.Status204NoContent);

        return app;
    }

}

/// <summary>Body for <c>POST /api/auth/2fa/verify</c>.</summary>
public sealed record TotpVerifyRequest(string Code);

/// <summary>Body for <c>POST /api/auth/2fa/disable</c>.</summary>
public sealed record TotpDisableRequest(string Code);

/// <summary>Response for a successful TOTP or recovery-code verification.</summary>
public sealed record TotpVerificationResponse(bool Valid, bool UsedRecoveryCode);

/// <summary>Response for <c>POST /api/auth/2fa/enroll</c>.</summary>
public sealed record TotpEnrollmentResponse(
    Guid CredentialId,
    string Secret,
    string QrCodeUrl,
    IReadOnlyList<string> RecoveryCodes);
