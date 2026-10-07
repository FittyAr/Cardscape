using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Infrastructure.Scim;

public sealed partial class ScimService
{
    private static string BuildGroupId(Guid workspaceId) =>
        ScimGroupIdPrefix + workspaceId.ToString("D");

    private static bool TryParseGroupId(string groupId, out Guid workspaceId)
    {
        workspaceId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(groupId)
            && groupId.StartsWith(ScimGroupIdPrefix, StringComparison.Ordinal)
            && Guid.TryParse(groupId[ScimGroupIdPrefix.Length..], out workspaceId);
    }

    private async Task<IReadOnlyList<ScimGroupMember>> BuildMembersAsync(
        Workspace workspace,
        CancellationToken ct)
    {
        IReadOnlyList<User> members = await userRepository.ListByIdsAsync(
            workspace.Members.Select(member => new UserId(member.UserId)).ToList(),
            ct);
        Dictionary<Guid, User> usersById = members.ToDictionary(user => user.Id.Value);

        return workspace.Members
            .Select(member => new ScimGroupMember(
                member.UserId.ToString("D"),
                usersById.GetValueOrDefault(member.UserId)?.DisplayName.Value))
            .ToList();
    }

    private async Task ReplaceMembersAsync(
        Workspace workspace,
        IReadOnlyList<ScimGroupMember> desired,
        CancellationToken ct)
    {
        HashSet<Guid> desiredIds = desired
            .Select(member => Guid.TryParse(member.Value, out Guid userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .ToHashSet();

        List<Guid> toRemove = workspace.Members
            .Where(member => !desiredIds.Contains(member.UserId)
                && member.UserId != workspace.OwnerId)
            .Select(member => member.UserId)
            .ToList();
        foreach (Guid userId in toRemove)
        {
            workspace.RemoveMember(userId, clock.UtcNow);
        }

        List<UserId> missingIds = desiredIds
            .Where(userId => !workspace.HasMember(userId))
            .Select(userId => new UserId(userId))
            .ToList();
        IReadOnlyList<User> usersToAdd = await userRepository.ListByIdsAsync(missingIds, ct);
        foreach (var user in usersToAdd)
        {
            workspace.AddMember(user.Id.Value, WorkspaceRole.Member, clock.UtcNow);
        }
    }

    private async Task<IReadOnlyList<User>> LoadValidUsersAsync(
        IReadOnlyList<ScimGroupMember> members,
        CancellationToken ct)
    {
        List<UserId> ids = members
            .Select(member => Guid.TryParse(member.Value, out Guid userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .Select(userId => new UserId(userId))
            .ToList();

        return await userRepository.ListByIdsAsync(ids, ct);
    }

    private static IReadOnlyList<ScimGroupMember> ExtractMembers(object? value)
    {
        if (value is IReadOnlyList<ScimGroupMember> typedMembers)
        {
            return typedMembers;
        }

        if (value is not JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            return [];
        }

        List<ScimGroupMember> members = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("value", out JsonElement valueElement))
            {
                continue;
            }

            string? memberValue = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString()
                : valueElement.GetRawText().Trim('"');
            string? memberDisplay = item.TryGetProperty("display", out JsonElement displayElement)
                && displayElement.ValueKind == JsonValueKind.String
                    ? displayElement.GetString()
                    : null;
            if (!string.IsNullOrWhiteSpace(memberValue))
            {
                members.Add(new ScimGroupMember(memberValue, memberDisplay));
            }
        }

        return members;
    }
}
