using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Users.Queries;

/// <summary>
/// Instance-admin directory of every user. <paramref name="Search"/>
/// matches a case-insensitive substring of the display name or
/// email; paging is zero-based.
/// </summary>
public sealed record ListUsersForAdminQuery(
    string? Search,
    UserStatusFilter Status = UserStatusFilter.All,
    int Page = 0,
    int PageSize = 25);

/// <summary>One row of the instance users page.</summary>
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

/// <summary>A page of <see cref="AdminUserDto"/> rows plus the filtered total.</summary>
public sealed record AdminUserPageDto(IReadOnlyList<AdminUserDto> Items, int Total);

public static class ListUsersForAdminQueryHandler
{
    public const int MaxPageSize = 100;

    public static async Task<AdminUserPageDto> HandleAsync(
        ListUsersForAdminQuery query,
        IUserRepository users,
        CancellationToken cancellation)
    {
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        int page = Math.Max(0, query.Page);
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        IReadOnlyList<User> rows = await users.ListForAdministrationAsync(query.Status, cancellation);
        List<User> filtered = rows
            .Where(user => search is null
                || user.DisplayName.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
                || user.Email.Value.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(user => user.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Email.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<AdminUserDto> items = filtered
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToList();

        return new AdminUserPageDto(items, filtered.Count);
    }

    private static AdminUserDto ToDto(User user) => new(
        user.Id.Value,
        user.Email.Value,
        user.DisplayName.Value,
        user.IsAdmin,
        user.IsActive,
        user.IsDeleted,
        user.IsAnonymised,
        user.IsRestricted,
        user.CreatedAt,
        user.LastLoginAt);
}
