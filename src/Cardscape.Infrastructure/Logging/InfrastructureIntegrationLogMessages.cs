using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Logging;

internal static partial class InfrastructureIntegrationLogMessages
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Warning, Message = "OpenAI-compatible provider returned HTTP {Status}")]
    internal static partial void AiProviderReturnedFailure(this ILogger logger, int status);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Error, Message = "OpenAI-compatible provider call failed.")]
    internal static partial void AiProviderCallFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2210, Level = LogLevel.Warning, Message = "Pending-2FA token {TokenPrefix} held a non-Guid value; ignoring.")]
    internal static partial void PendingTotpTokenValueInvalid(this ILogger logger, string tokenPrefix);

    [LoggerMessage(EventId = 2211, Level = LogLevel.Warning, Message = "Redis Consume failed for pending-2FA token; refusing the request.")]
    internal static partial void PendingTotpConsumeFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2220, Level = LogLevel.Warning, Message = "Redis Configure failed for token {TokenId}; in-memory state for this instance is unaffected.")]
    internal static partial void RedisRateLimitConfigureFailed(this ILogger logger, Exception exception, Guid tokenId);

    [LoggerMessage(EventId = 2221, Level = LogLevel.Warning, Message = "Redis GetStatus failed for token {TokenId}; returning null.")]
    internal static partial void RedisRateLimitStatusFailed(this ILogger logger, Exception exception, Guid tokenId);

    [LoggerMessage(EventId = 2222, Level = LogLevel.Warning, Message = "Redis script returned null for token {TokenId}; failing open.")]
    internal static partial void RedisRateLimitScriptReturnedNull(this ILogger logger, Guid tokenId);

    [LoggerMessage(EventId = 2223, Level = LogLevel.Warning, Message = "Redis script returned unexpected shape for token {TokenId}; failing open.")]
    internal static partial void RedisRateLimitScriptShapeInvalid(this ILogger logger, Guid tokenId);

    [LoggerMessage(EventId = 2224, Level = LogLevel.Warning, Message = "Redis TryAcquire failed for token {TokenId}; allowing the request.")]
    internal static partial void RedisRateLimitAcquireFailed(this ILogger logger, Exception exception, Guid tokenId);

    [LoggerMessage(EventId = 2230, Level = LogLevel.Warning, Message = "OAuth app registration rejected: {Code} {Message}")]
    internal static partial void OAuthAppRegistrationRejected(this ILogger logger, string code, string message);

    [LoggerMessage(EventId = 2231, Level = LogLevel.Warning, Message = "OAuth app {ClientId} presented an invalid client secret.")]
    internal static partial void OAuthClientSecretInvalid(this ILogger logger, string clientId);

    [LoggerMessage(EventId = 2232, Level = LogLevel.Warning, Message = "OAuth revoke for unknown client {ClientId}.")]
    internal static partial void OAuthRevokeClientUnknown(this ILogger logger, string clientId);

    [LoggerMessage(EventId = 2233, Level = LogLevel.Warning, Message = "OAuth revoke for client {ClientId} presented an invalid secret.")]
    internal static partial void OAuthRevokeSecretInvalid(this ILogger logger, string clientId);

    [LoggerMessage(EventId = 2234, Level = LogLevel.Warning, Message = "OAuth revoke rejected: client {ClientId} presented token owned by a different app.")]
    internal static partial void OAuthRevokeTokenOwnerMismatch(this ILogger logger, string clientId);
}
