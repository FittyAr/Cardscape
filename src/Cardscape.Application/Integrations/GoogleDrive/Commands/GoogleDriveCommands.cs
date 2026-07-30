using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Integrations.GoogleDrive.DTOs;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.GoogleDrive;
using Wolverine;

namespace Cardscape.Application.Integrations.GoogleDrive.Commands;

public sealed record ConnectGoogleDriveCommand(
    Guid WorkspaceId,
    string GoogleEmail,
    string EncryptedRefreshToken) : IMessage;

public static class ConnectGoogleDriveCommandHandler
{
    public static async Task<Result<GoogleDriveConnectionDto>> Handle(
        ConnectGoogleDriveCommand command,
        IGoogleDriveConnectionRepository connections,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<GoogleDriveConnectionDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        GoogleDriveConnection? existing =
            await connections.FindForUserAsync(new Cardscape.Domain.Members.UserId(currentUser.Id.Value), ct);

        GoogleDriveConnection entity;
        if (existing is null)
        {
            var creation = GoogleDriveConnection.Connect(
                GoogleDriveConnectionId.New(),
                new Cardscape.Domain.Members.UserId(currentUser.Id.Value),
                command.GoogleEmail,
                command.EncryptedRefreshToken,
                clock.UtcNow);
            if (creation.IsFailure)
            {
                return Result.Failure<GoogleDriveConnectionDto>(creation.Error);
            }

            await connections.AddAsync(creation.Value, ct);
            entity = creation.Value;
        }
        else
        {
            existing.Activate(clock.UtcNow);
            existing.RecordUse(clock.UtcNow);
            entity = existing;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(GoogleDriveConnectionDto.FromEntity(entity));
    }
}

public sealed record GetGoogleDrivePickerUrlQuery(Guid WorkspaceId) : IMessage;

public static class GetGoogleDrivePickerUrlQueryHandler
{
    public static async Task<Result<string>> Handle(
        GetGoogleDrivePickerUrlQuery query,
        IGoogleDrivePickerService picker,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<string>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        return await picker.BuildPickerUrlAsync(
            query.WorkspaceId, currentUser.Id.Value, ct);
    }
}

public sealed record AttachGoogleDriveFileCommand(
    Guid CardId,
    string FileId,
    string? FileName) : IMessage;

public static class AttachGoogleDriveFileCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        AttachGoogleDriveFileCommand command,
        IGoogleDrivePickerService picker,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<Guid>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Result<AttachmentId> result = await picker.AttachFileAsync(
            command.CardId, command.FileId, command.FileName,
            currentUser.Id.Value, ct);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        return Result.Success(result.Value.Value);
    }
}
