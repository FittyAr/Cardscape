using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Infrastructure.Persistence.Audit;

/// <summary>
/// Marks every board activity and audit entry written by a request made with
/// an AI agent key as "the owner's AI" (ADR 0015). The actor stays the key's
/// owner, so permissions, mentions and history keep working; the agent's id
/// and name snapshot sit beside it. Runs inside <c>SavingChangesAsync</c>
/// (see <c>DomainEventsInterceptor</c>), after the audit trail has added its
/// entries for the same save, so the commands that record activities need no
/// change.
/// </summary>
internal sealed class AgentAttributionStamper(IServiceProvider services)
{
    public void Stamp(DbContext context)
    {
        if (services.GetService<ICurrentUser>()?.ActingAgent() is not { } agent)
        {
            return;
        }

        foreach (Activity activity in context.ChangeTracker.Entries<Activity>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.AgentId is null)
            .Select(entry => entry.Entity))
        {
            activity.AttributeToAgent(agent.Id, agent.Name);
        }

        foreach (AuditEntry auditEntry in context.ChangeTracker.Entries<AuditEntry>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.ActorAgentId is null)
            .Select(entry => entry.Entity))
        {
            auditEntry.AttributeToAgent(agent.Id, agent.Name);
        }
    }
}
