using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cardscape.Api.Logging;

internal static partial class ApiLogMessages
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "BackgroundJobDispatcherService starting: poll={Interval}, batch={Batch}")]
    internal static partial void BackgroundJobDispatcherStarting(this ILogger logger, TimeSpan interval, int batch);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug, Message = "Dispatched {Count} background jobs")]
    internal static partial void BackgroundJobsDispatched(this ILogger logger, int count);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "BackgroundJobDispatcherService loop failed; will retry")]
    internal static partial void BackgroundJobDispatcherLoopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "BackgroundJobDispatcherService stopping")]
    internal static partial void BackgroundJobDispatcherStopping(this ILogger logger);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Information, Message = "RateLimitBucketEvictionService starting: interval={Interval}, cutoff={Cutoff}")]
    internal static partial void RateLimitEvictionStarting(this ILogger logger, TimeSpan interval, TimeSpan cutoff);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Information, Message = "Rate-limit bucket eviction removed {Count} idle buckets (cutoff={Cutoff}).")]
    internal static partial void RateLimitBucketsEvicted(this ILogger logger, int count, DateTimeOffset cutoff);

    [LoggerMessage(EventId = 4012, Level = LogLevel.Error, Message = "Rate-limit bucket eviction sweep failed.")]
    internal static partial void RateLimitEvictionFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4013, Level = LogLevel.Information, Message = "RateLimitBucketEvictionService stopping.")]
    internal static partial void RateLimitEvictionStopping(this ILogger logger);

    [LoggerMessage(EventId = 4020, Level = LogLevel.Warning, Message = "AdminOnly requirement evaluated with claim caching enabled but without an is_admin claim. Returning failed (403).")]
    internal static partial void AdminClaimMissing(this ILogger logger);

    [LoggerMessage(EventId = 4021, Level = LogLevel.Warning, Message = "AdminOnly requirement evaluated for an authenticated principal without a parseable NameIdentifier claim. Returning failed (403).")]
    internal static partial void AdminUserIdentifierInvalid(this ILogger logger);

    [LoggerMessage(EventId = 4022, Level = LogLevel.Warning, Message = "AdminOnly requirement evaluated for user id {UserId} that has no row in the users table. Returning failed (403).")]
    internal static partial void AdminUserNotFound(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4030, Level = LogLevel.Information, Message = "Rejected API token: {ErrorCode}")]
    internal static partial void ApiTokenRejected(this ILogger logger, string errorCode);

    [LoggerMessage(EventId = 4031, Level = LogLevel.Warning, Message = "Failed to persist SCIM token LastUsedAt for token prefix {Prefix}.")]
    internal static partial void ScimTokenLastUsedPersistenceFailed(this ILogger logger, Exception exception, string prefix);

    [LoggerMessage(EventId = 4032, Level = LogLevel.Information, Message = "Rejecting revoked JWT (jti={Jti})")]
    internal static partial void RevokedJwtRejected(this ILogger logger, string jti);

    [LoggerMessage(EventId = 4033, Level = LogLevel.Error, Message = "Failed to look up revocation for jti={Jti}; failing closed.")]
    internal static partial void JwtRevocationLookupFailed(this ILogger logger, Exception exception, string jti);

    [LoggerMessage(EventId = 4040, Level = LogLevel.Error, Message = "SAML handler error for {Slug}/{Action}")]
    internal static partial void SamlHandlerFailed(this ILogger logger, Exception exception, string slug, string action);

    [LoggerMessage(EventId = 4041, Level = LogLevel.Warning, Message = "SAML SignIn returned no Location for {Slug}.")]
    internal static partial void SamlSignInLocationMissing(this ILogger logger, string slug);

    [LoggerMessage(EventId = 4042, Level = LogLevel.Warning, Message = "SAML ACS processing failed for {Slug}.")]
    internal static partial void SamlAcsProcessingFailed(this ILogger logger, Exception exception, string slug);

    [LoggerMessage(EventId = 4043, Level = LogLevel.Warning, Message = "Failed to fetch IdP metadata for {Slug}.")]
    internal static partial void SamlMetadataFetchFailed(this ILogger logger, Exception exception, string slug);

    [LoggerMessage(EventId = 4044, Level = LogLevel.Warning, Message = "Failed to parse inline IdP metadata.")]
    internal static partial void SamlInlineMetadataParsingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4045, Level = LogLevel.Information, Message = "SAML config not found for slug {Slug}.")]
    internal static partial void SamlConfigurationNotFound(this ILogger logger, string slug);

    [LoggerMessage(EventId = 4050, Level = LogLevel.Information, Message = "Rate limit exceeded for API token {TokenId} on {Path}; Retry-After={RetryAfter}s")]
    internal static partial void ApiTokenRateLimitExceeded(this ILogger logger, Guid tokenId, PathString path, int retryAfter);

    [LoggerMessage(EventId = 4051, Level = LogLevel.Warning, Message = "Idempotency-Key {Key} from {Owner} replayed with a different payload (path={Path})")]
    internal static partial void IdempotencyPayloadMismatch(this ILogger logger, IdempotencyKeyValue key, UserId owner, PathString path);

    [LoggerMessage(EventId = 4052, Level = LogLevel.Information, Message = "Idempotency-Key {Key} from {Owner} replayed; returning stored response (path={Path})")]
    internal static partial void IdempotencyResponseReplayed(this ILogger logger, IdempotencyKeyValue key, UserId owner, PathString path);

    [LoggerMessage(EventId = 4053, Level = LogLevel.Error, Message = "Lost Idempotency-Key reservation {Key} from {Owner} for {Path} before completion")]
    internal static partial void IdempotencyReservationLost(this ILogger logger, IdempotencyKeyValue key, UserId owner, PathString path);

    [LoggerMessage(EventId = 4060, Level = LogLevel.Warning, Message = "Validation failed for {Path}")]
    internal static partial void RequestValidationFailed(this ILogger logger, Exception exception, PathString path);

    [LoggerMessage(EventId = 4061, Level = LogLevel.Warning, Message = "JSON deserialisation failed for {Path}")]
    internal static partial void RequestJsonDeserializationFailed(this ILogger logger, Exception exception, PathString path);

    [LoggerMessage(EventId = 4062, Level = LogLevel.Warning, Message = "Bad request at {Path}")]
    internal static partial void BadRequestReceived(this ILogger logger, Exception exception, PathString path);

    [LoggerMessage(EventId = 4063, Level = LogLevel.Information, Message = "Concurrency conflict at {Path}")]
    internal static partial void RequestConcurrencyConflict(this ILogger logger, Exception exception, PathString path);

    [LoggerMessage(EventId = 4064, Level = LogLevel.Error, Message = "Unhandled exception at {Path}")]
    internal static partial void RequestUnhandledException(this ILogger logger, Exception exception, PathString path);

    [LoggerMessage(EventId = 4070, Level = LogLevel.Warning, Message = "Rejected JoinBoard for board {BoardId}: user {UserId} is not a member.")]
    internal static partial void BoardHubJoinRejected(this ILogger logger, Guid boardId, Guid userId);

    [LoggerMessage(EventId = 4080, Level = LogLevel.Debug, Message = "Cardscape:Mcp:BaseUrl is not set on the API. MCP resource subscription fan-out is disabled.")]
    internal static partial void McpResourceBaseUrlMissing(this ILogger logger);

    [LoggerMessage(EventId = 4081, Level = LogLevel.Debug, Message = "Internal:Secret is not set on the API. MCP resource subscription fan-out is disabled.")]
    internal static partial void McpResourceSecretMissing(this ILogger logger);

    [LoggerMessage(EventId = 4082, Level = LogLevel.Warning, Message = "API->MCP board-event for {BoardId} threw")]
    internal static partial void McpBoardEventNotificationFailed(this ILogger logger, Exception exception, Guid boardId);

    [LoggerMessage(EventId = 4083, Level = LogLevel.Warning, Message = "Cardscape:Mcp:BaseUrl is not set on the API. MCP subscriptions snapshot is unavailable.")]
    internal static partial void McpSubscriptionsBaseUrlMissing(this ILogger logger);

    [LoggerMessage(EventId = 4084, Level = LogLevel.Warning, Message = "Internal:Secret is not set on the API. MCP subscriptions snapshot is unavailable.")]
    internal static partial void McpSubscriptionsSecretMissing(this ILogger logger);

    [LoggerMessage(EventId = 4085, Level = LogLevel.Warning, Message = "MCP subscriptions snapshot returned {Status} {Reason}")]
    internal static partial void McpSubscriptionsSnapshotUnsuccessful(this ILogger logger, int status, string? reason);

    [LoggerMessage(EventId = 4086, Level = LogLevel.Warning, Message = "MCP subscriptions snapshot threw")]
    internal static partial void McpSubscriptionsSnapshotFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4090, Level = LogLevel.Information, Message = "WebhookEventsSchemaTransformer running")]
    internal static partial void WebhookEventsSchemaTransformerRunning(this ILogger logger);
}
