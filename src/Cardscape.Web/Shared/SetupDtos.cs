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

/// <summary>Progress of "start with demo data" (GET /api/setup/demo/status).
/// The demo sign-in is filled once the run succeeded.</summary>
public sealed record DemoSetupStatusDto(
    bool Running,
    string Status,
    int CurrentStep,
    int TotalSteps,
    string? CurrentStepName,
    string? DemoEmail,
    string? DemoPassword);
