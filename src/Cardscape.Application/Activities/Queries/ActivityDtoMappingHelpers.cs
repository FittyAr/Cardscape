using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Activities.Queries;

internal static class ActivityDtoMappingHelpers
{
    internal static async Task<IReadOnlyList<ActivityDto>> ToDtosAsync(
        IReadOnlyList<Activity> page,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        List<UserId> actorIds = page
            .Select(activity => new UserId(activity.ActorId))
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, string> displayNames =
            (await users.ListByIdsAsync(actorIds, cancellationToken))
            .ToDictionary(user => user.Id.Value, user => user.DisplayName.Value);
        return page.Select(activity => ActivityDto.FromEntity(activity, displayNames)).ToList();
    }
}
