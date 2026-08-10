using Cardscape.Seeder;
using Cardscape.Seeder.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cardscape.Api.Pages;

/// <summary>
/// Code-behind for <c>Pages/Seeder.cshtml</c>. Builds the
/// initial model the Razor template renders on first load;
/// subsequent updates come from the JSON endpoint that the
/// page polls every two seconds.
/// </summary>
public sealed class SeederModel : PageModel
{
    private readonly SeedRunner _runner;
    private readonly ISeedReportProvider _report;

    public SeederModel(SeedRunner runner, ISeedReportProvider report)
    {
        _runner = runner;
        _report = report;
    }

    public string Status => _report.Report.Status;

    public string StatusCssClass => _report.Report.Status switch
    {
        "Succeeded" => "pill-ok",
        var s when s.StartsWith("Failed") => "pill-err",
        "Wiped" => "pill-ok",
        "Running" => "pill-running",
        _ => "pill-idle"
    };

    public bool IsRunning => _runner.IsRunning;

    public bool IsEnabled => _runner.IsEnabled;

    public IReadOnlyList<SeedLogEntry> Entries => _report.Report.Entries
        .OrderBy(e => e.At)
        .ToList();

    public IReadOnlyList<SeedTableStatus> Tables => _report.Report.TableSnapshot();

    public string TableSummary => $"{Tables.Count} tables, {Tables.Sum(t => t.RowCount)} rows total";

    public IActionResult OnGet()
    {
        if (!IsEnabled)
        {
            return NotFound();
        }
        return Page();
    }
}
