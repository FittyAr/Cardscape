using System.Text.Json;

namespace Cardscape.Application.Abstractions;

/// <summary>
/// Marker interface for a background-job handler. Each
/// <see cref="IBackgroundJobScheduler.EnqueueAsync"/> call references a
/// <c>type</c> string; the dispatcher routes the claim to the
/// matching registered handler. The handler deserializes the
/// payload itself.
/// </summary>
public interface IBackgroundJobHandler
{
    /// <summary>The discriminator string this handler responds to.</summary>
    string Type { get; }

    /// <summary>Executes the job. Throwing marks the attempt as failed.</summary>
    Task HandleAsync(Guid jobId, JsonElement payload, CancellationToken ct);
}

/// <summary>
/// Registry that maps a job <c>type</c> string to its handler. The
/// dispatcher uses it to dispatch each claim to the right
/// implementation. Infrastructure builds the registry from the
/// handlers registered in DI.
/// </summary>
public interface IBackgroundJobHandlerRegistry
{
    IBackgroundJobHandler? Resolve(string type);
    IReadOnlyCollection<string> RegisteredTypes { get; }
}
