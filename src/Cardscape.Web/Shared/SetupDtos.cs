namespace Cardscape.Web.Shared;

public sealed record SetupStatusDto(
    bool IsInitialized,
    string DatabaseProvider);

public sealed record InitializeSystemRequestDto(
    string AdminDisplayName,
    string AdminEmail,
    string AdminPassword,
    string? InstanceTitle,
    string? InitialWorkspaceName);
