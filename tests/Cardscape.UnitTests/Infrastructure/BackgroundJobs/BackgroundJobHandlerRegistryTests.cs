using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Infrastructure.BackgroundJobs;

namespace Cardscape.UnitTests.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobHandlerRegistryTests
{
    [Fact]
    public void Constructor_NoHandlers_ExposesEmptyRegistry()
    {
        var sut = new BackgroundJobHandlerRegistry([]);

        sut.RegisteredTypes.Should().BeEmpty();
        sut.Resolve("missing").Should().BeNull();
    }

    [Fact]
    public void Resolve_RegisteredType_ReturnsRegisteredHandler()
    {
        var handler = new StubHandler("send-email");
        var sut = new BackgroundJobHandlerRegistry([handler]);

        IBackgroundJobHandler? resolved = sut.Resolve("send-email");

        resolved.Should().BeSameAs(handler);
        sut.RegisteredTypes.Should().Equal("send-email");
        sut.Resolve("unknown").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_HandlerTypeIsEmpty_ThrowsInvalidOperationException(string type)
    {
        Action act = () => _ = new BackgroundJobHandlerRegistry([new StubHandler(type)]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must declare a non-empty Type*");
    }

    [Fact]
    public void Constructor_HandlerTypesAreDuplicated_ThrowsInvalidOperationException()
    {
        IBackgroundJobHandler[] handlers = [new StubHandler("send-email"), new StubHandler("send-email")];

        Action act = () => _ = new BackgroundJobHandlerRegistry(handlers);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Duplicate background job handler registration for type 'send-email'.");
    }

    [Fact]
    public void Constructor_HandlerTypesDifferOnlyByCase_RegistersBothHandlers()
    {
        var lowerCaseHandler = new StubHandler("send-email");
        var upperCaseHandler = new StubHandler("SEND-EMAIL");

        var sut = new BackgroundJobHandlerRegistry([lowerCaseHandler, upperCaseHandler]);

        sut.Resolve("send-email").Should().BeSameAs(lowerCaseHandler);
        sut.Resolve("SEND-EMAIL").Should().BeSameAs(upperCaseHandler);
        sut.RegisteredTypes.Should().BeEquivalentTo("send-email", "SEND-EMAIL");
    }

    [Fact]
    public void Constructor_SourceCollectionIsMutated_KeepsImmutableSnapshot()
    {
        var originalHandler = new StubHandler("original");
        var handlers = new List<IBackgroundJobHandler> { originalHandler };
        var sut = new BackgroundJobHandlerRegistry(handlers);

        handlers.Clear();
        handlers.Add(new StubHandler("added-later"));

        sut.Resolve("original").Should().BeSameAs(originalHandler);
        sut.Resolve("added-later").Should().BeNull();
        sut.RegisteredTypes.Should().Equal("original");
    }

    [Fact]
    public void RegisteredTypes_ReturnedCollectionIsMutated_KeepsImmutableRegistry()
    {
        var handler = new StubHandler("original");
        var sut = new BackgroundJobHandlerRegistry([handler]);
        var snapshot = sut.RegisteredTypes.Should().BeAssignableTo<string[]>().Subject;

        snapshot[0] = "changed";

        sut.Resolve("original").Should().BeSameAs(handler);
        sut.Resolve("changed").Should().BeNull();
        sut.RegisteredTypes.Should().Equal("original");
    }

    private sealed class StubHandler(string type) : IBackgroundJobHandler
    {
        public string Type { get; } = type;

        public Task HandleAsync(Guid jobId, JsonElement payload, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
