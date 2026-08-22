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
        return await context.CardAgingSettings
            .FirstOrDefaultAsync(settings => settings.Id == cardId, ct);
    }

    public async Task AddAsync(CardAgingSettings settings, CancellationToken ct = default) =>
        await context.CardAgingSettings.AddAsync(settings, ct);

    public Task RemoveAsync(CardAgingSettings settings, CancellationToken ct = default)
    {
        context.CardAgingSettings.Remove(settings);
        return Task.CompletedTask;
    }
}
