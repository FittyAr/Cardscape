using System.Globalization;
using System.Text;
using Cardscape.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Cardscape.Web.Shared;

/// <summary>
/// CSV export of audit-log entries, as the user reads them: local dates,
/// localized descriptions, plus the stable action code for tooling.
/// Fields are RFC 4180 quoted, and a leading = + - @ is neutralised so a
/// name cannot run as a spreadsheet formula (CSV injection).
/// </summary>
public static class AuditCsv
{
    /// <summary>The export stops here; narrow the filters for more.</summary>
    public const int MaxRows = 10_000;

    public static string Build(IEnumerable<AuditEntryDto> entries, IStringLocalizer<SharedResource> L)
    {
        StringBuilder csv = new();
        AppendRow(csv,
        [
            L["AuditCsvDate"], L["AuditColumnWho"], L["AuditCsvArea"], L["AuditColumnWhat"],
            L["AuditCsvAction"], L["AuditCsvTarget"], L["AuditCsvWorkspace"], L["AuditCsvBoard"],
        ]);
        foreach (AuditEntryDto entry in entries)
        {
            AppendRow(csv,
            [
                entry.OccurredAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                AuditPresentation.Actor(entry, L),
                L[AuditPresentation.AreaKey(entry.Action)],
                AuditPresentation.Sentence(entry, L),
                entry.Action,
                entry.TargetName,
                entry.WorkspaceName ?? string.Empty,
                entry.BoardName ?? string.Empty,
            ]);
        }

        return csv.ToString();
    }

    public static string Escape(string? value)
    {
        string text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0
            ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : text;
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string?> fields)
    {
        csv.AppendJoin(',', fields.Select(Escape));
        csv.Append("\r\n");
    }
}
