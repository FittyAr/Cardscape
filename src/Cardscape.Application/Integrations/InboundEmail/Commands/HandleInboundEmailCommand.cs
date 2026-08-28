using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Integrations.InboundEmail.Commands;

public sealed record HandleInboundEmailCommand(
    string Provider,
    string RawBody,
    IDictionary<string, string> Headers) : IMessage;

public static class HandleInboundEmailCommandHandler
{
    public static Task<Result<Guid>> Handle(
        HandleInboundEmailCommand command,
        IInboundEmailService service,
        CancellationToken ct) =>
        service.HandleAsync(command.Provider, command.RawBody, command.Headers, ct);
}
