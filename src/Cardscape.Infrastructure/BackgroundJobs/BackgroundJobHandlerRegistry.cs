using Cardscape.Application.Abstractions;

namespace Cardscape.Infrastructure.BackgroundJobs;

/// <summary>
/// Immutable registry of <see cref="IBackgroundJobHandler"/>
/// instances built from dependency injection.
/// </summary>
public sealed class BackgroundJobHandlerRegistry : IBackgroundJobHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IBackgroundJobHandler> byType;

    public BackgroundJobHandlerRegistry(IEnumerable<IBackgroundJobHandler> handlers)
    {
        var registered = new Dictionary<string, IBackgroundJobHandler>(StringComparer.Ordinal);
        foreach (IBackgroundJobHandler handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Type))
            {
                throw new InvalidOperationException(
                    $"Background job handler {handler.GetType().FullName} must declare a non-empty Type.");
            }

            if (!registered.TryAdd(handler.Type, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate background job handler registration for type '{handler.Type}'.");
            }
        }

        byType = registered;
    }

    public IReadOnlyCollection<string> RegisteredTypes => byType.Keys.ToArray();

    public IBackgroundJobHandler? Resolve(string type) =>
        byType.TryGetValue(type, out IBackgroundJobHandler? handler) ? handler : null;
}
