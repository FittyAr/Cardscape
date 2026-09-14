namespace Cardscape.Infrastructure.Persistence.Inbox;

internal sealed class ExternalMessageReceipt
{
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string MessageHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public long LeaseExpiresAtUtcTicks { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? ResourceId { get; private set; }

    private ExternalMessageReceipt() { }

    internal ExternalMessageReceipt(Guid id, string source, string messageHash, DateTimeOffset now)
    {
        Id = id;
        Source = source;
        MessageHash = messageHash;
        CreatedAt = now;
        LeaseExpiresAtUtcTicks = (now + LeaseDuration).UtcTicks;
    }
}
