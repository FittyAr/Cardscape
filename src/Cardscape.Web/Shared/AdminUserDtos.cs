namespace Cardscape.Web.Shared;

// ── Instance users (admin) ──────────────────────────────
public enum UserStatusFilter
{
    All = 0,
    Active = 1,
    Deactivated = 2,
    Deleted = 3
}

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsAdmin,
    bool IsActive,
    bool IsDeleted,
    bool IsAnonymised,
    bool IsRestricted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsEmailVerified = true);

public sealed record AdminUserPageDto(IReadOnlyList<AdminUserDto> Items, int Total);

/// <summary>Mirror of the API's AdminAccountResult (create user / reset password).
/// <see cref="TemporaryPassword"/> is set only when the user could not be emailed a link.</summary>
public sealed record AdminAccountResultDto(
    Guid UserId,
    string Email,
    Cardscape.Contracts.Email.EmailDeliveryStatus EmailStatus,
    string? TemporaryPassword);
