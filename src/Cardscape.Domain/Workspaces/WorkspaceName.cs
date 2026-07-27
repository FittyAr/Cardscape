using Cardscape.Domain.Common;

namespace Cardscape.Domain.Workspaces;

/// <summary>A non-empty workspace name.</summary>
public sealed record WorkspaceName : IValueObject
{
    public const int MinLength = 1;
    public const int MaxLength = 100;

    public string Value { get; }

    private WorkspaceName(string value) => Value = value;

    public static Result<WorkspaceName> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<WorkspaceName>(DomainError.Validation(
                "workspaces.name.required",
                "Workspace name is required."));
        }

        var trimmed = input.Trim();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return Result.Failure<WorkspaceName>(DomainError.Validation(
                "workspaces.name.length",
                $"Workspace name must be between {MinLength} and {MaxLength} characters."));
        }

        return Result.Success(new WorkspaceName(trimmed));
    }

    public override string ToString() => Value;
}
