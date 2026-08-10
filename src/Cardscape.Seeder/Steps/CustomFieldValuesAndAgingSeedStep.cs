using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Seeder.Persistence;
using Cardscape.Seeder.Reporting;

namespace Cardscape.Seeder.Steps;

/// <summary>Populates a <c>CustomFieldValue</c> row per card per
/// definition (3 values per card on average — the values are
/// randomly picked to match the field's kind) and a per-card
/// aging setting on a handful of cards so the fade effect has
/// something to operate on.</summary>
public sealed class CustomFieldValuesAndAgingSeedStep : SeedStepBase
{
    public override string Name => "Custom field values + card aging";
    public override int Order => 90;

    public override Task ExecuteAsync(SeedContext context, SeedReport log, CancellationToken cancellationToken)
    {
        DateTimeOffset now = context.Now;
        var random = new Random(404);
        int totalValues = 0;

        foreach (Card card in context.Cards)
        {
            // 1. Look up the definitions belonging to this card's
            //    board. The card's ListId is the foreign key to
            //    a list, which has a BoardId; the field
            //    definitions are board-scoped.
            BoardList? list = context.Lists.FirstOrDefault(l => l.Id.Value == card.ListId.Value);
            if (list is null)
            {
                continue;
            }

            List<CustomFieldDefinition> defs = context.CustomFieldDefinitions
                .Where(d => d.BoardId.Value == list.BoardId.Value)
                .ToList();
            if (defs.Count == 0)
            {
                continue;
            }

            // 2. Plant a value for two of the three definitions
            //    (so some cards intentionally leave the third
            //    blank, which is realistic).
            for (int i = 0; i < defs.Count - 1; i++)
            {
                CustomFieldDefinition def = defs[i];
                string json = def.Kind switch
                {
                    CustomFieldKind.Dropdown => PickDropdown(def, random),
                    CustomFieldKind.Number => JsonSerializerFor($"{(random.Next(1, 14))}"),
                    CustomFieldKind.Date => JsonSerializerFor(now.AddDays(random.Next(-30, 30)).UtcDateTime.ToString("o")),
                    CustomFieldKind.Checkbox => random.NextDouble() < 0.5 ? "true" : "false",
                    _ => JsonSerializerFor("Lorem ipsum dolor sit amet.")
                };

                Result<CustomFieldValue> created = CustomFieldValue.Create(
                    def.Id, card.Id, json, now.AddDays(-1));
                if (created.IsFailure)
                {
                    continue;
                }

                context.Db.CustomFieldValues.Add(created.Value);
                context.CustomFieldValues.Add(created.Value);
                totalValues++;
            }

            // 3. Card aging — 30% of cards opt in to the
            //    ByActivity mode with a 14-day stale window.
            if (random.NextDouble() < 0.30)
            {
                Result<CardAgingSettings> aging = CardAgingSettings.Create(
                    card.Id, CardAgingMode.ByActivity, 14, now.AddDays(-1));
                if (aging.IsSuccess)
                {
                    context.Db.CardAgingSettings.Add(aging.Value);
                    context.CardAgingSettings.Add(aging.Value);
                }
            }
        }

        Log(log, SeedLogLevel.Success,
            $"Inserted {totalValues} custom field values and {context.CardAgingSettings.Count} card aging settings.");
        return Task.CompletedTask;
    }

    private static string PickDropdown(CustomFieldDefinition def, Random random)
    {
        // The options are stored as a JSON array on the
        // definition. Deserialize, pick one, re-serialise.
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(def.OptionsJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return JsonSerializerFor("Medium");
            }

            int count = doc.RootElement.GetArrayLength();
            if (count == 0)
            {
                return JsonSerializerFor("Medium");
            }

            string choice = doc.RootElement[random.Next(0, count)].GetString() ?? "Medium";
            return JsonSerializerFor(choice);
        }
        catch
        {
            return JsonSerializerFor("Medium");
        }
    }

    private static string JsonSerializerFor(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value, System.Text.Json.JsonSerializerOptions.Default);
}
