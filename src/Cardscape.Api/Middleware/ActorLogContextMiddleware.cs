using Cardscape.Application.Abstractions.Security;
using Serilog.Context;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Tags every log event of an authenticated request with who made it:
/// <c>ActorKind</c> is <c>agent</c> when the caller used an AI agent key and
/// <c>user</c> otherwise, and an agent's request also carries
/// <c>AgentId</c> and <c>AgentName</c>, so the server logs tell "Ivan" and
/// "Ivan's AI" apart just as board activity and the audit log do (ADR 0015).
/// </summary>
public sealed class ActorLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
        {
            await next(context);
            return;
        }

        ActingAgent? agent = currentUser.ActingAgent();
        using (LogContext.PushProperty("ActorKind", agent is null ? "user" : "agent"))
        using (LogContext.PushProperty("AgentId", agent?.Id))
        using (LogContext.PushProperty("AgentName", agent?.Name))
        {
            await next(context);
        }
    }
}
