using System.Reflection;
using Cardscape.Application.Lists;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Tools;
using ModelContextProtocol.Server;
using Moq;
using Wolverine;

namespace Cardscape.UnitTests.Mcp;

public sealed class McpToolReturnTypeTests
{
    [Fact]
    public void McpTools_DoNotReturnRawResults()
    {
        string[] offenders = typeof(V110Tools).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Where(method => IsResult(Unwrap(method.ReturnType)))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "Result has no wire shape; tools must unwrap it with OrThrow so failures become tool errors");
    }

    [Fact]
    public async Task FailedCommand_SurfacesAsToolError()
    {
        Mock<IMessageBus> bus = new();
        bus.Setup(b => b.InvokeAsync<Result>(
                It.IsAny<SetListLimitCommand>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(Result.Failure(DomainError.NotFound("lists.not_found", "List not found.")));

        Func<Task> act = () => new V110Tools().SetListLimitAsync(
            Guid.NewGuid(), 3, soft: false, bus.Object, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("lists.not_found: List not found.");
    }

    private static Type Unwrap(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? type.GetGenericArguments()[0]
            : type;

    private static bool IsResult(Type type) =>
        type == typeof(Result) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>));
}
