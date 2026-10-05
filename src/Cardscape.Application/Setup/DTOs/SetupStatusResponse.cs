namespace Cardscape.Application.Setup.DTOs;

public sealed record SetupStatusResponse(
    bool IsInitialized,
    string DatabaseProvider);
