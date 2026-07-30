using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.CustomFields;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for per-board custom fields. Kind enum:
/// 0 = Text, 1 = Number, 2 = Date, 3 = Dropdown, 4 = Checkbox.
/// <c>valueJson</c> shape depends on the field's kind (see
/// <c>CustomFieldValue.ValidateShape</c> in the Domain layer).
/// </summary>
[McpServerToolType]
public sealed class CustomFieldsTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "custom_fields_list_definitions")]
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> ListDefinitions(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_list_definitions");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CustomFieldDefinitionDto>>>(
            new ListCustomFieldDefinitionsQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "custom_fields_create_definition")]
    public async Task<CustomFieldDefinitionDto> CreateDefinition(
        Guid boardId,
        string name,
        int kind,
        IReadOnlyList<string>? dropdownOptions,
        int position,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_create_definition");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
            new CreateCustomFieldDefinitionCommand(boardId, name, kind, dropdownOptions, position), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "custom_fields_rename_definition")]
    public async Task<CustomFieldDefinitionDto> RenameDefinition(
        Guid fieldId,
        string newName,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_rename_definition");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(
            new RenameCustomFieldDefinitionCommand(fieldId, newName), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "custom_fields_delete_definition")]
    public async Task<string> DeleteDefinition(Guid fieldId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_delete_definition");
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(
            new DeleteCustomFieldDefinitionCommand(fieldId), ct);
        Ensure(result);
        return "deleted";
    }

    [McpServerTool(Name = "custom_fields_list_values_for_card")]
    public async Task<IReadOnlyList<CustomFieldValueDto>> ListValues(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_list_values_for_card");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CustomFieldValueDto>>>(
            new ListCustomFieldValuesForCardQuery(cardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "custom_fields_set_value")]
    public async Task<CustomFieldValueDto> SetValue(
        Guid cardId,
        Guid fieldId,
        string? valueJson,
        CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("custom_fields_set_value");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CustomFieldValueDto>>(
            new SetCustomFieldValueCommand(cardId, fieldId, valueJson), ct);
        return Ensure(result);
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT or API token in the Authorization header.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
        }
        return result.Value!;
    }

    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
        }
    }
}

