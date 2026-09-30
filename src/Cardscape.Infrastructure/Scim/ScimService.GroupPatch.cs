using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Infrastructure.Scim;

public sealed partial class ScimService
{
    private static Result<IReadOnlyList<ScimPatchOperation>> NormalizeGroupPatch(ScimPatchRequest patch, Guid ownerId)
    {
        List<ScimPatchOperation> operations = [];
        foreach (ScimPatchOperation operation in patch.Operations)
        {
            bool writesValue = string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase);
            if (writesValue && operation.Path is null)
            {
                // RFC 7644 3.5.2.1/3: a pathless value is an attribute object,
                // never a replacement of every omitted attribute.
                if (operation.Value is not JsonElement { ValueKind: JsonValueKind.Object } attributes)
                {
                    return InvalidGroupPatch("A pathless operation requires an attribute object.");
                }

                foreach (JsonProperty attribute in attributes.EnumerateObject())
                {
                    operations.Add(new ScimPatchOperation(operation.Op, attribute.Name, attribute.Value));
                }
            }
            else
            {
                operations.Add(operation);
            }
        }

        // Validate the complete payload before changing a tracked aggregate.
        foreach (ScimPatchOperation operation in operations)
        {
            if (string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase)
                && operation.Path is not null)
            {
                var match = MemberRemovalPath().Match(operation.Path);
                if (match.Success && Guid.TryParse(match.Groups["id"].Value, out Guid targetId)
                    && targetId == ownerId)
                {
                    return Result.Failure<IReadOnlyList<ScimPatchOperation>>(DomainError.Validation(
                        "scim.mutability", "The workspace owner cannot be removed from the group."));
                }
            }

            if (!string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(operation.Path, "members", StringComparison.OrdinalIgnoreCase)
                && !IsValidMemberPatch(operation.Value))
            {
                return InvalidGroupPatch("Members must be an array of objects with non-empty user identifiers.");
            }

            if (string.Equals(operation.Path, "displayName", StringComparison.OrdinalIgnoreCase))
            {
                string? name = operation.Value as string
                    ?? (operation.Value is JsonElement { ValueKind: JsonValueKind.String } value ? value.GetString() : null);
                if (name is null)
                {
                    return InvalidGroupPatch("DisplayName must be a string.");
                }

                Result<WorkspaceName> result = WorkspaceName.Create(name);
                if (result.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<ScimPatchOperation>>(result.Error);
                }
            }
        }

        return Result.Success<IReadOnlyList<ScimPatchOperation>>(operations);
    }

    private static Result<IReadOnlyList<ScimPatchOperation>> InvalidGroupPatch(string message) =>
        Result.Failure<IReadOnlyList<ScimPatchOperation>>(DomainError.Validation("scim.invalid_value", message));

    private static bool IsValidMemberPatch(object? value)
    {
        if (value is IReadOnlyList<ScimGroupMember> members)
        {
            return members.All(member => Guid.TryParse(member.Value, out Guid id) && id != Guid.Empty);
        }

        if (value is not JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return false;
        }

        return array.EnumerateArray().All(member =>
            member.ValueKind == JsonValueKind.Object
            && member.TryGetProperty("value", out JsonElement idValue)
            && idValue.ValueKind == JsonValueKind.String
            && Guid.TryParse(idValue.GetString(), out Guid id)
            && id != Guid.Empty);
    }
}
