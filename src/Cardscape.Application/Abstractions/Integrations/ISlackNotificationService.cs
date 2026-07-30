using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;

namespace Cardscape.Application.Abstractions.Integrations;

/// <summary>
/// Sends a notification to a single Slack channel. The default
/// implementation is an HTTP client that POSTs to
/// <c>https://slack.com/api/chat.postMessage</c> with a bot token
/// sourced from configuration
/// (<c>Integrations:Slack:BotToken</c>). Other implementations can
/// route to a queue, a no-op logger, or a fake in tests.
/// </summary>
public interface ISlackNotificationService
{
    /// <summary>
    /// Posts <paramref name="message"/> to <paramref name="channelId"/>
    /// using the bot token of <paramref name="workspace"/>. Returns
    /// <see cref="Result.Success"/> on a 2xx response with
    /// <c>ok: true</c> in the body, or a domain error wrapping
    /// the underlying failure otherwise.
    /// </summary>
    Task<Result> SendAsync(
        SlackWorkspace workspace,
        string channelId,
        string message,
        CancellationToken ct = default);
}
