using Cardscape.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Cardscape.Web.Shared;

/// <summary>
/// Base class for any page or component that uses
/// <c>@L["..."]</c> and needs the text to refresh when the
/// user switches language. Subscribes to
/// <see cref="CultureSwitcher.Changed"/> in
/// <see cref="OnInitialized"/> and calls
/// <see cref="ComponentBase.StateHasChanged"/> so the L[ ]
/// values are re-evaluated against the new dictionary.
/// <para>
/// BETA-8-I18N-#3 — see test-results/r8/A8-settings.md.
/// Blazor's layout → @Body cascade only re-invokes the
/// @Body RenderFragment when the route changes. A culture
/// change re-renders the layout (so the topbar, sidebar,
/// and profile menu re-localise) but the @Body is not
/// re-rendered unless the page itself subscribes to the
/// culture change. Without this base class every page
/// would need to wire up the subscription; with it the
/// page just <c>: base</c>s off this class.
/// </para>
/// </summary>
public abstract class CultureReactiveComponentBase : ComponentBase, IDisposable
{
    [Inject]
    private CultureSwitcher Culture { get; set; } = default!;

    protected override void OnInitialized()
    {
        Culture.Changed += OnCultureChanged;
    }

    public void Dispose()
    {
        Culture.Changed -= OnCultureChanged;
        GC.SuppressFinalize(this);
    }

    private void OnCultureChanged() => InvokeAsync(StateHasChanged);
}
