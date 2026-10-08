using Cardscape.Web.Shared;

namespace Cardscape.Web.Components.Dashboard;

/// <summary>Mutable form model behind <see cref="DashcardFilterFields"/>.</summary>
public sealed class DashcardFilterModel
{
    public Guid? ListId { get; set; }

    public Guid? LabelId { get; set; }

    public Guid? MemberId { get; set; }

    public static DashcardFilterModel From(DashcardFilter filter) =>
        new() { ListId = filter.ListId, LabelId = filter.LabelId, MemberId = filter.MemberId };

    public DashcardFilter ToFilter() => new(ListId, LabelId, MemberId);

    public void Clear() => (ListId, LabelId, MemberId) = (null, null, null);
}
