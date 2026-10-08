namespace Cardscape.Contracts.Email;

/// <summary>What happened to the email that accompanies an action (e.g. an invitation).</summary>
public enum EmailDeliveryStatus
{
    /// <summary>Outbound email is off or incomplete in System settings; nothing was attempted.</summary>
    NotConfigured = 0,

    /// <summary>The SMTP server accepted the message.</summary>
    Sent = 1,

    /// <summary>Delivery was attempted and failed; the link must be shared by hand.</summary>
    Failed = 2,
}
