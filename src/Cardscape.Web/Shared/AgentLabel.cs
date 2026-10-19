using Cardscape.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Cardscape.Web.Shared;

/// <summary>
/// Plain-text form of <see cref="ActorLabel"/>: "Ivan" for the person,
/// "Ivan · AI (Claude Desktop)" for a change made by Ivan's AI agent
/// (ADR 0015). Used where markup cannot go: exports, aria labels.
/// </summary>
public static class AgentLabel
{
    public static string Text(string name, string? agentName, IStringLocalizer<SharedResource> L)
    {
        ArgumentNullException.ThrowIfNull(L);
        return string.IsNullOrWhiteSpace(agentName)
            ? name
            : $"{name} · {L["AgentActorSuffix"]} ({agentName})";
    }
}
