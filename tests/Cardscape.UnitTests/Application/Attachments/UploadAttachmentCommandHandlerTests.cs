using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Attachments;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fakes;
using Moq;

namespace Cardscape.UnitTests.Application.Attachments;

public sealed class UploadAttachmentCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ValidUpload_PersistsBlobAndMetadataWithSanitizedName()
    {
        HandlerFixture fixture = CreateFixture();
        using var content = new MemoryStream([1, 2, 3]);
        var command = new UploadAttachmentCommand(
            fixture.Card.Id.Value, "../reports/quarter:one.pdf", " Application/PDF ", 3, content);

        var result = await fixture.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("quarterone.pdf");
        result.Value.MimeType.Should().Be("application/pdf");
        result.Value.SizeBytes.Should().Be(3);
        result.Value.UploaderId.Should().Be(fixture.UserId);
        result.Value.CreatedAt.Should().Be(Now);
        fixture.SavedKey.Should().EndWith("/quarterone.pdf");
        fixture.AddedAttachment.Should().NotBeNull();
        fixture.AddedAttachment!.StorageKey.Should().Be(fixture.SavedKey);
        fixture.AddedAttachment.FileName.Should().Be(result.Value.FileName);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Storage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMetadataSaveFails_DeletesStoredBlobWithoutCancellationAndRethrows()
    {
        var expected = new InvalidOperationException("database unavailable");
        HandlerFixture fixture = CreateFixture(saveFailure: expected);
        using var content = new MemoryStream([4, 5]);
        var command = new UploadAttachmentCommand(
            fixture.Card.Id.Value, "evidence.txt", "text/plain", 2, content);

        Func<Task> act = async () => await fixture.HandleAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expected.Message);
        fixture.AddedAttachment.Should().NotBeNull();
        fixture.SavedKey.Should().Be(fixture.AddedAttachment!.StorageKey);
        fixture.Storage.Verify(
            x => x.DeleteAsync(fixture.SavedKey!, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_BlockedMimeType_DoesNotTouchStorageMetadataOrUnitOfWork()
    {
        HandlerFixture fixture = CreateFixture();
        using var content = new MemoryStream([6]);
        var command = new UploadAttachmentCommand(
            fixture.Card.Id.Value, "payload.exe", " APPLICATION/X-MSDOWNLOAD ", 1, content);

        var result = await fixture.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("attachments.mime_blocked");
        fixture.Storage.VerifyNoOtherCalls();
        fixture.Attachments.VerifyNoOtherCalls();
        fixture.UnitOfWork.VerifyNoOtherCalls();
    }

    private static HandlerFixture CreateFixture(Exception? saveFailure = null)
    {
        Guid userId = Guid.NewGuid();
        var listId = BoardListId.New();
        var boardId = BoardId.New();
        Card card = Card.Create(
            CardId.New(), listId, CardTitle.Create("Upload target").Value,
            CardDescription.Create(string.Empty).Value, Position.Start(), userId, Now).Value;
        Board board = Board.Create(
            boardId, WorkspaceId.New(), BoardName.Create("Uploads").Value,
            BoardDescription.Create(string.Empty).Value, BoardVisibility.Private, userId, Now).Value;

        var attachments = new Mock<IAttachmentRepository>(MockBehavior.Strict);
        Attachment? addedAttachment = null;
        attachments.Setup(x => x.AddAsync(It.IsAny<Attachment>(), It.IsAny<CancellationToken>()))
            .Callback<Attachment, CancellationToken>((attachment, _) => addedAttachment = attachment)
            .Returns(Task.CompletedTask);

        var cards = new Mock<ICardRepository>(MockBehavior.Strict);
        cards.Setup(x => x.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        var lists = new Mock<IBoardListRepository>(MockBehavior.Strict);
        lists.Setup(x => x.GetBoardIdAsync(listId, It.IsAny<CancellationToken>())).ReturnsAsync(boardId);
        var boards = new Mock<IBoardRepository>(MockBehavior.Strict);
        boards.Setup(x => x.GetByIdAsync(boardId, It.IsAny<CancellationToken>())).ReturnsAsync(board);

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        string? savedKey = null;
        storage.Setup(x => x.SaveAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((key, _, _, _) => savedKey = key)
            .ReturnsAsync((string key, Stream _, string _, CancellationToken _) => key);
        storage.Setup(x => x.DeleteAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var save = unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
        if (saveFailure is null)
        {
            save.ReturnsAsync(1);
        }
        else
        {
            save.ThrowsAsync(saveFailure);
        }

        var currentUser = new Mock<ICurrentUser>(MockBehavior.Strict);
        currentUser.SetupGet(x => x.Id).Returns(new UserId(userId));

        return new HandlerFixture(
            attachments, cards, lists, boards, unitOfWork, storage,
            new FakeClock(Now), currentUser.Object, card, userId,
            () => addedAttachment, () => savedKey);
    }

    private sealed record HandlerFixture(
        Mock<IAttachmentRepository> Attachments,
        Mock<ICardRepository> Cards,
        Mock<IBoardListRepository> Lists,
        Mock<IBoardRepository> Boards,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<IStorageService> Storage,
        IClock Clock,
        ICurrentUser CurrentUser,
        Card Card,
        Guid UserId,
        Func<Attachment?> AddedAttachmentAccessor,
        Func<string?> SavedKeyAccessor)
    {
        public Attachment? AddedAttachment => AddedAttachmentAccessor();
        public string? SavedKey => SavedKeyAccessor();

        public Task<Result<AttachmentDto>> HandleAsync(UploadAttachmentCommand command) =>
            UploadAttachmentCommandHandler.Handle(
                command, Attachments.Object, Cards.Object, Lists.Object, Boards.Object,
                UnitOfWork.Object, Storage.Object, Clock, CurrentUser,
                TestContext.Current.CancellationToken);
    }
}
