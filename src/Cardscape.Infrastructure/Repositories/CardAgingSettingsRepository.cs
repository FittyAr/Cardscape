using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardAgingSettingsRepository(
    CardscapeDbContext context) : ICardAgingSettingsRepository
{
    public async Task<CardAgingSettings?> GetByCardIdAsync(CardId cardId, CancellationToken ct = default)
    {
        // Strongly-typed-id filter: same AsAsyncEnumerable pattern as
        // the other repositories (HasConversion + EF Core 10 limitation).
        IAsyncEnumerable<CardAgingSettings> stream = context.CardAgingSettings
            .AsAsyncEnumerable()
            .Where(s => s.Id.Value == cardId.Value);
        await foreach (CardAgingSettings s in stream.WithCancellation(ct))
        {
            return s;
        }
        return null;
    }

    public async Task AddAsync(CardAgingSettings settings, CancellationToken ct = default) =>
        await context.CardAgingSettings.AddAsync(settings, ct);

    public Task RemoveAsync(CardAgingSettings settings, CancellationToken ct = default)
    {
        context.CardAgingSettings.Remove(settings);
        return Task.CompletedTask;
    }
}
