using Cardscape.Contracts.Settings;
using Radzen;

namespace Cardscape.Web.Shared;

/// <summary>Maps instance notice severities to Radzen alert styles.</summary>
public static class NoticeStyles
{
    public static AlertStyle For(NoticeSeverity severity) => severity switch
    {
        NoticeSeverity.Success => AlertStyle.Success,
        NoticeSeverity.Warning => AlertStyle.Warning,
        NoticeSeverity.Danger => AlertStyle.Danger,
        _ => AlertStyle.Info,
    };
}
