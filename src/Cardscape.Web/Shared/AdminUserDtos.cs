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
    DateTimeOffset? LastLoginAt);

public sealed record AdminUserPageDto(IReadOnlyList<AdminUserDto> Items, int Total);
