using Cardscape.Web.Resources;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Cardscape.Web.Shared;

/// <summary>
/// Turns audit-log entries into localized sentences such as "Ada changed
/// Linus's role from Member to Admin in Nexora Labs". The resource key of
/// an action code is <c>Audit</c> + its PascalCase form
/// (<c>workspace.member_role_changed</c> → <c>AuditWorkspaceMemberRoleChanged</c>);
/// every sentence takes the same placeholders:
/// {0} actor, {1} target, {2} workspace, {3} board, {4} previous role,
/// {5} role (new or granted), {6} previous owner, {7} the action code,
/// {8} previous name (workspace renames), {9} the new value (region, two-factor).
/// Unknown codes and roles fall back to readable raw values, so entries
/// written by a newer server still render.
/// </summary>
public static class AuditPresentation
{
    public const string ActionPrefixUsers = "user.";
    public const string ActionPrefixWorkspaces = "workspace.";
    public const string ActionPrefixBoards = "board.";

    public static string SentenceKey(string action) =>
        "Audit" + string.Concat(action
            .Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    public static string Actor(AuditEntryDto entry, IStringLocalizer<SharedResource> L) =>
        entry.ActorUserId is not null
            ? entry.ActorName
            : string.Equals(entry.ActorName, "scim", StringComparison.OrdinalIgnoreCase)
                ? L["AuditActorScim"]
                : L["AuditActorSystem"];

    public static string Sentence(AuditEntryDto entry, IStringLocalizer<SharedResource> L)
    {
        string unknown = L["CommonUnknown"];
        string target = string.IsNullOrWhiteSpace(entry.TargetName) ? unknown : entry.TargetName;
        string workspace = string.IsNullOrWhiteSpace(entry.WorkspaceName) ? unknown : entry.WorkspaceName;
        string board = string.IsNullOrWhiteSpace(entry.BoardName) ? unknown : entry.BoardName;
        bool onBoard = entry.Action.StartsWith(ActionPrefixBoards, StringComparison.Ordinal);
        string previousRole = Role(Detail(entry, "from") ?? Detail(entry, "role"), onBoard, L);
        string role = Role(Detail(entry, "to") ?? Detail(entry, "role"), onBoard, L);
        string previousOwner = Detail(entry, "previousOwnerName") is { Length: > 0 } owner ? owner : unknown;
        string previousName = Detail(entry, "previousName") is { Length: > 0 } name ? name : unknown;
        string value = Value(entry, L);
        object[] arguments =
            [Actor(entry, L), target, workspace, board, previousRole, role, previousOwner, entry.Action, previousName, value];

        LocalizedString sentence = L[SentenceKey(entry.Action), arguments];
        return sentence.ResourceNotFound ? L["AuditUnknownAction", arguments] : sentence;
    }

    public static BadgeStyle Style(string action) => action switch
    {
        _ when action.StartsWith(ActionPrefixUsers, StringComparison.Ordinal) => BadgeStyle.Info,
        _ when action.StartsWith(ActionPrefixWorkspaces, StringComparison.Ordinal) => BadgeStyle.Primary,
        _ when action.StartsWith(ActionPrefixBoards, StringComparison.Ordinal) => BadgeStyle.Secondary,
        _ => BadgeStyle.Light
    };

    public static string AreaKey(string action) => action switch
    {
        _ when action.StartsWith(ActionPrefixUsers, StringComparison.Ordinal) => "AuditAreaUsers",
        _ when action.StartsWith(ActionPrefixWorkspaces, StringComparison.Ordinal) => "AuditAreaWorkspaces",
        _ when action.StartsWith(ActionPrefixBoards, StringComparison.Ordinal) => "AuditAreaBoards",
        _ => "CommonUnknown"
    };

    // Role names arrive as the server's enum names (Admin, Member, ...).
    // A role this client does not know yet is shown as sent.
    private static string Role(string? name, bool onBoard, IStringLocalizer<SharedResource> L)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return L["CommonUnknown"];
        }

        string pascal = char.ToUpperInvariant(name[0]) + name[1..];
        LocalizedString label = L[(onBoard ? "BoardRole" : "WorkspaceRole") + pascal];
        return label.ResourceNotFound ? pascal : label;
    }

    // The two-factor switch reads as a phrase; anything else as sent.
    private static string Value(AuditEntryDto entry, IStringLocalizer<SharedResource> L) =>
        Detail(entry, "value") switch
        {
            "on" => L["AuditTwoFactorOn"],
            "off" => L["AuditTwoFactorOff"],
            { Length: > 0 } raw => raw,
            _ => L["CommonUnknown"]
        };

    private static string? Detail(AuditEntryDto entry, string key) =>
        entry.Details is not null && entry.Details.TryGetValue(key, out string? value) ? value : null;
}
