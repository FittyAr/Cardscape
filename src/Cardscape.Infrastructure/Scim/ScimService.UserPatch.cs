using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;

namespace Cardscape.Infrastructure.Scim;

public sealed partial class ScimService
{
    private static Result<IReadOnlyList<bool>> NormalizeUserPatch(ScimPatchRequest request)
    {
        if (request.Operations.Count == 0)
        {
            return InvalidUserPatch("scim.invalid_syntax", "At least one operation is required.");
        }

        List<ScimPatchOperation> operations = [];
        foreach (ScimPatchOperation operation in request.Operations)
        {
            bool writesValue = string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase);
            if (!writesValue)
            {
                if (!string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidUserPatch("scim.invalid_syntax", "The operation must be add, replace or remove.");
                }
                if (operation.Path is null)
                {
                    return InvalidUserPatch("scim.no_target", "Remove requires a path.");
                }
                bool protectedAttribute = string.Equals(operation.Path, "active", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(operation.Path, "id", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(operation.Path, "meta", StringComparison.OrdinalIgnoreCase);
                return protectedAttribute
                    ? InvalidUserPatch("scim.mutability", "The attribute cannot be removed.")
                    : InvalidUserPatch("scim.invalid_path", "The removal path is not supported.");
            }

            if (operation.Path is null)
            {
                if (operation.Value is not JsonElement { ValueKind: JsonValueKind.Object } attributes
                    || !attributes.EnumerateObject().Any())
                {
                    return InvalidUserPatch("scim.invalid_value", "A pathless operation requires a non-empty attribute object.");
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

        List<bool> changes = [];
        foreach (ScimPatchOperation operation in operations)
        {
            if (string.Equals(operation.Path, "id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.Path, "meta", StringComparison.OrdinalIgnoreCase))
            {
                return InvalidUserPatch("scim.mutability", "The attribute is read-only.");
            }
            if (!string.Equals(operation.Path, "active", StringComparison.OrdinalIgnoreCase))
            {
                return InvalidUserPatch("scim.invalid_path", "Only the active attribute is supported for user PATCH.");
            }

            bool? active = operation.Value switch
            {
                bool value => value,
                JsonElement { ValueKind: JsonValueKind.True } => true,
                JsonElement { ValueKind: JsonValueKind.False } => false,
                _ => null
            };
            if (active is null)
            {
                return InvalidUserPatch("scim.invalid_value", "Active must be a boolean.");
            }
            changes.Add(active.Value);
        }

        return Result.Success<IReadOnlyList<bool>>(changes);
    }

    private static Result<IReadOnlyList<bool>> InvalidUserPatch(string code, string message) =>
        Result.Failure<IReadOnlyList<bool>>(DomainError.Validation(code, message));
}
