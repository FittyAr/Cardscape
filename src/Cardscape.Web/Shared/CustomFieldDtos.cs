namespace Cardscape.Web.Shared;

// ── Custom fields (v0.7.1) ─────────────────────────────────
public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid BoardId,
    string Name,
    CustomFieldKind Kind,
    string OptionsJson,
    int Position);

public sealed record CustomFieldValueDto(
    Guid FieldDefinitionId,
    Guid CardId,
    CustomFieldKind Kind,
    string ValueJson);

public sealed record CreateCustomFieldRequestDto(
    string Name,
    CustomFieldKind Kind,
    IReadOnlyList<string>? DropdownOptions,
    int Position = 0);

public sealed record RenameCustomFieldRequestDto(string NewName);

public sealed record SetCustomFieldValueRequestDto(string? ValueJson);
