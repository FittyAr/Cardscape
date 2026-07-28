using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.CustomFields;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fakes;

namespace Cardscape.UnitTests.Application.Handlers;

public class CustomFieldCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_With_Text_Kind_Persists_Definition()
    {
        SutState state = await BuildSutAsync(seedBoard: true);
        Board board = state.Board!;

        var result = await CreateCustomFieldDefinitionCommandHandler.Handle(
            new CreateCustomFieldDefinitionCommand(
                board.Id.Value, "Priority", 0, null, Position: 0),
            state.Definitions, state.Boards, state.UnitOfWork, state.CurrentUser, state.Clock, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Priority");
        result.Value.Kind.Should().Be(0);
    }

    [Fact]
    public async Task Create_With_Unknown_Kind_Returns_Validation_Error()
    {
        SutState state = await BuildSutAsync(seedBoard: true);
        Board board = state.Board!;

        var result = await CreateCustomFieldDefinitionCommandHandler.Handle(
            new CreateCustomFieldDefinitionCommand(
                board.Id.Value, "Foo", 99, null, Position: 0),
            state.Definitions, state.Boards, state.UnitOfWork, state.CurrentUser, state.Clock, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.kind_unknown");
    }

    [Fact]
    public async Task Create_By_Non_Member_Returns_Forbidden()
    {
        // The board is owned by `ownerId`; the current user is a different `memberId`,
        // so the IsMember check inside the handler must fail.
        SutState state = await BuildSutAsync(seedBoard: true);
        Board board = state.Board!;
        state.CurrentUser.Id = new UserId(Guid.NewGuid());

        var result = await CreateCustomFieldDefinitionCommandHandler.Handle(
            new CreateCustomFieldDefinitionCommand(
                board.Id.Value, "Priority", 0, null, Position: 0),
            state.Definitions, state.Boards, state.UnitOfWork, state.CurrentUser, state.Clock, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Delete_Cascades_To_Values()
    {
        SutState state = await BuildSutAsync(seedBoard: true, seedCard: true);
        Board board = state.Board!;
        Card card = state.Card!;

        CustomFieldDefinition field = CustomFieldDefinition.Create(
            board.Id, "Priority", CustomFieldKind.Text, null, 0, Now).Value;
        await state.Definitions.AddAsync(field, default);
        await state.UnitOfWork.SaveChangesAsync(default);

        CustomFieldValue value = CustomFieldValue.Create(
            field.Id, card.Id, "\"high\"", Now).Value;
        await state.Values.AddAsync(value, default);
        await state.UnitOfWork.SaveChangesAsync(default);

        var delete = await DeleteCustomFieldDefinitionCommandHandler.Handle(
            new DeleteCustomFieldDefinitionCommand(field.Id.Value),
            state.Definitions, state.Values, state.Boards, state.UnitOfWork, state.CurrentUser, default);

        delete.IsSuccess.Should().BeTrue();
        IReadOnlyList<CustomFieldValue> remaining = await state.Values.ListForCardAsync(card.Id);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task SetValue_With_Empty_String_Removes_Existing_Value()
    {
        SutState state = await BuildSutAsync(seedBoard: true, seedCard: true);
        Board board = state.Board!;
        Card card = state.Card!;

        CustomFieldDefinition field = CustomFieldDefinition.Create(
            board.Id, "Priority", CustomFieldKind.Text, null, 0, Now).Value;
        await state.Definitions.AddAsync(field, default);
        await state.UnitOfWork.SaveChangesAsync(default);

        await SetCustomFieldValueCommandHandler.Handle(
            new SetCustomFieldValueCommand(card.Id.Value, field.Id.Value, "\"high\""),
            state.Values, state.Definitions, state.Cards, state.Boards, state.Lists,
            state.UnitOfWork, state.CurrentUser, state.Clock, default);

        var clear = await SetCustomFieldValueCommandHandler.Handle(
            new SetCustomFieldValueCommand(card.Id.Value, field.Id.Value, null),
            state.Values, state.Definitions, state.Cards, state.Boards, state.Lists,
            state.UnitOfWork, state.CurrentUser, state.Clock, default);

        clear.IsSuccess.Should().BeTrue();
        IReadOnlyList<CustomFieldValue> remaining = await state.Values.ListForCardAsync(card.Id);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task SetValue_With_Dropdown_Unknown_Option_Returns_Validation()
    {
        SutState state = await BuildSutAsync(seedBoard: true, seedCard: true);
        Board board = state.Board!;
        Card card = state.Card!;

        CustomFieldDefinition field = CustomFieldDefinition.Create(
            board.Id, "Severity", CustomFieldKind.Dropdown,
            new[] { "Low", "High" }, 0, Now).Value;
        await state.Definitions.AddAsync(field, default);
        await state.UnitOfWork.SaveChangesAsync(default);

        var result = await SetCustomFieldValueCommandHandler.Handle(
            new SetCustomFieldValueCommand(card.Id.Value, field.Id.Value, "\"Critical\""),
            state.Values, state.Definitions, state.Cards, state.Boards, state.Lists,
            state.UnitOfWork, state.CurrentUser, state.Clock, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("custom_fields.dropdown_value_unknown");
    }

    [Fact]
    public async Task ListForBoard_Returns_Only_That_Boards_Fields()
    {
        SutState state = await BuildSutAsync(seedBoard: true, seedOtherBoard: true);
        Board board = state.Board!;
        Board otherBoard = state.OtherBoard!;

        CustomFieldDefinition a = CustomFieldDefinition.Create(
            board.Id, "Priority", CustomFieldKind.Text, null, 0, Now).Value;
        CustomFieldDefinition b = CustomFieldDefinition.Create(
            otherBoard.Id, "Severity", CustomFieldKind.Text, null, 0, Now).Value;
        await state.Definitions.AddAsync(a, default);
        await state.Definitions.AddAsync(b, default);
        await state.UnitOfWork.SaveChangesAsync(default);

        var result = await ListCustomFieldDefinitionsQueryHandler.Handle(
            new ListCustomFieldDefinitionsQuery(board.Id.Value),
            state.Definitions, state.Boards, state.CurrentUser, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Priority");
    }

    // ── scaffolding ──────────────────────────────────────────
    private static async Task<SutState> BuildSutAsync(
        bool seedBoard = false,
        bool seedCard = false,
        bool seedOtherBoard = false)
    {
        Guid ownerId = Guid.NewGuid();
        var clock = new FakeClock(Now);
        var uow = new FakeUnitOfWork();
        var user = new FakeCurrentUser { IsAuthenticated = true, Id = new UserId(ownerId) };
        var boards = new InMemoryBoardRepository();
        var lists = new InMemoryBoardListRepository();
        var cards = new InMemoryCardRepository();
        var defs = new InMemoryCustomFieldDefinitionRepository();
        var values = new InMemoryCustomFieldValueRepository(defs);

        Board? board = null;
        if (seedBoard)
        {
            var workspace = Workspace.Create(
                WorkspaceId.New(),
                WorkspaceName.Create("WS").Value,
                ownerId,
                clock.UtcNow).Value;
            var created = Board.Create(
                BoardId.New(),
                workspace.Id,
                BoardName.Create("Board").Value,
                BoardDescription.Create("Test board").Value,
                BoardVisibility.Private,
                ownerId,
                clock.UtcNow).Value;
            await boards.AddAsync(created, default);
            board = (await boards.ListForWorkspaceAsync(workspace.Id, default))[0];
        }

        Card? card = null;
        if (seedCard && board is not null)
        {
            var list = BoardList.Create(
                BoardListId.New(),
                board.Id,
                ListName.Create("Todo").Value,
                Position.Start(),
                ownerId,
                clock.UtcNow).Value;
            await lists.AddAsync(list, default);
            card = Card.Create(
                CardId.New(),
                list.Id,
                CardTitle.Create("Card").Value,
                CardDescription.Create("").Value,
                Position.Start(),
                ownerId,
                clock.UtcNow).Value;
            await cards.AddAsync(card, default);
        }

        Board? otherBoard = null;
        if (seedOtherBoard)
        {
            var workspace2 = Workspace.Create(
                WorkspaceId.New(),
                WorkspaceName.Create("WS2").Value,
                ownerId,
                clock.UtcNow).Value;
            var created = Board.Create(
                BoardId.New(),
                workspace2.Id,
                BoardName.Create("Other").Value,
                BoardDescription.Create("Other board").Value,
                BoardVisibility.Private,
                ownerId,
                clock.UtcNow).Value;
            await boards.AddAsync(created, default);
            otherBoard = (await boards.ListForWorkspaceAsync(workspace2.Id, default))[0];
        }

        await uow.SaveChangesAsync(default);

        return new SutState(defs, values, boards, lists, cards, uow, user, clock, board, card, otherBoard);
    }

    private sealed class SutState
    {
        public SutState(
            InMemoryCustomFieldDefinitionRepository definitions,
            InMemoryCustomFieldValueRepository values,
            IBoardRepository boards,
            IBoardListRepository lists,
            ICardRepository cards,
            IUnitOfWork unitOfWork,
            FakeCurrentUser currentUser,
            FakeClock clock,
            Board? board,
            Card? card,
            Board? otherBoard)
        {
            Definitions = definitions;
            Values = values;
            Boards = boards;
            Lists = lists;
            Cards = cards;
            UnitOfWork = unitOfWork;
            CurrentUser = currentUser;
            Clock = clock;
            Board = board;
            Card = card;
            OtherBoard = otherBoard;
        }

        public InMemoryCustomFieldDefinitionRepository Definitions { get; }
        public InMemoryCustomFieldValueRepository Values { get; }
        public IBoardRepository Boards { get; }
        public IBoardListRepository Lists { get; }
        public ICardRepository Cards { get; }
        public IUnitOfWork UnitOfWork { get; }
        public FakeCurrentUser CurrentUser { get; }
        public FakeClock Clock { get; }
        public Board? Board { get; }
        public Card? Card { get; }
        public Board? OtherBoard { get; }
    }
}
