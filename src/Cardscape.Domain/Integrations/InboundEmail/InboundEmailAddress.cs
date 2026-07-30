using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Domain.Integrations.InboundEmail;

/// <summary>
/// A workspace-scoped inbound email address. When a webhook from
/// the configured email provider (SendGrid / Mailgun / Postmark)
/// hits <c>/api/integrations/email/inbound</c> with the
/// destination address, the application resolves the address to
/// an <see cref="InboundEmailAddress"/>, then creates a card on
/// the configured list using the email's subject as the title
/// and the body as the description.
/// </summary>
public sealed class InboundEmailAddress : AggregateRoot<InboundEmailAddressId>
{
    public WorkspaceId WorkspaceId { get; private set; } = null!;

    /// <summary>The fully-qualified destination email address
    /// (e.g. <c>board-1-in@cardscape.example</c>).</summary>
    public string EmailAddress { get; private set; } = string.Empty;

    public BoardListId TargetListId { get; private set; } = null!;

    /// <summary>Display label shown in the workspace settings.</summary>
    public string Label { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    // EF Core.
    private InboundEmailAddress() { }

    private InboundEmailAddress(
        InboundEmailAddressId id,
        WorkspaceId workspaceId,
        string emailAddress,
        BoardListId targetListId,
        string label,
        DateTimeOffset at)
    {
        Id = id;
        WorkspaceId = workspaceId;
        EmailAddress = emailAddress;
        TargetListId = targetListId;
        Label = label;
        Active = true;
        CreatedAt = at;
    }

    public static Result<InboundEmailAddress> Register(
        InboundEmailAddressId id,
        WorkspaceId workspaceId,
        string emailAddress,
        BoardListId targetListId,
        string label,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "inbound_email.address_required",
                "Inbound email address is required."));
        }

        if (emailAddress.Length > 320)
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "inbound_email.address_too_long",
                "Inbound email address must be 320 characters or fewer."));
        }

        if (!emailAddress.Contains('@'))
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "inbound_email.address_malformed",
                "Inbound email address must contain an '@'."));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "inbound_email.label_required", "Label is required."));
        }

        if (label.Length > 200)
        {
            return Result.Failure<InboundEmailAddress>(DomainError.Validation(
                "inbound_email.label_too_long",
                "Label must be 200 characters or fewer."));
        }

        return Result.Success(new InboundEmailAddress(
            id, workspaceId, emailAddress.Trim().ToLowerInvariant(),
            targetListId, label.Trim(), at));
    }

    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }
}
