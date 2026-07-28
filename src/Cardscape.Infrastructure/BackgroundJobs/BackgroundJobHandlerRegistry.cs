using System.Collections.Concurrent;
using Cardscape.Application.Abstractions;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>
/// Thread-safe registry of <see cref="IBackgroundJobHandler"/>
/// instances. Populated at startup from
/// <c>IServiceProvider.GetServices&lt;IBackgroundJobHandler&gt;()</c>.
/// </summary>
public sealed class BackgroundJobHandlerRegistry : IBackgroundJobHandlerRegistry
{
    private readonly ConcurrentDictionary<string, IBackgroundJobHandler> byType = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> RegisteredTypes => byType.Keys.ToArray();

    public IBackgroundJobHandler? Resolve(string type) =>
        byType.TryGetValue(type, out IBackgroundJobHandler? handler) ? handler : null;

    public void Register(IBackgroundJobHandler handler)
    {
        if (string.IsNullOrWhiteSpace(handler.Type))
        {
            throw new ArgumentException(
                $"Background job handler {handler.GetType().FullName} must declare a non-empty Type.",
                nameof(handler));
        }

        if (!byType.TryAdd(handler.Type, handler))
        {
            throw new InvalidOperationException(
                $"Duplicate background job handler registration for type '{handler.Type}'.");
        }
    }
}
