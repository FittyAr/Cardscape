using Cardscape.Web.Resources;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Cardscape.Web.Shared;

/// <summary>
/// Template for pages that page through card due dates one month at a
/// time (calendar, planner). Owns the month cursor, the API round-trip and
/// the loading/error state; derived pages only decide how to render
/// <see cref="Entries"/>.
/// </summary>
public abstract class DueDatesPageBase : CultureReactiveComponentBase
{
    [Inject] private ICardsApiClient Cards { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] protected IStringLocalizer<SharedResource> L { get; set; } = default!;

    protected CalendarMonth Month { get; private set; } = CalendarMonth.Current;

    protected IReadOnlyList<CalendarEntryDto> Entries { get; private set; } = [];

    protected bool IsLoading { get; private set; }

    protected string? Error { get; private set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    protected async Task ShowMonthAsync(CalendarMonth month)
    {
        Month = month;
        await LoadAsync();
    }

    protected void OpenCard(Guid cardId) => Navigation.NavigateTo($"cards/{cardId}");

    /// <summary>Hook for pages that derive view state (groupings, projections) from <see cref="Entries"/>.</summary>
    protected virtual void OnEntriesLoaded()
    {
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            ApiResult<IReadOnlyList<CalendarEntryDto>> result = await Cards.CalendarAsync(Month.Start, Month.End);
            (Entries, Error) = result is { IsSuccess: true, Value: { } value }
                ? (value, (string?)null)
                : ([], result.Error ?? L["CalendarLoadFailed"]);
        }
        catch (HttpRequestException ex)
        {
            (Entries, Error) = ([], ex.Message);
        }
        finally
        {
            IsLoading = false;
        }

        OnEntriesLoaded();
    }
}
