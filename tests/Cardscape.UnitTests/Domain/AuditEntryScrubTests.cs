using Cardscape.Domain.Audit;
using Cardscape.Domain.Members;

namespace Cardscape.UnitTests.Domain;

/// <summary>GDPR: anonymising a person replaces their name snapshots in the audit log.</summary>
public sealed class AuditEntryScrubTests
{
    private const string Placeholder = User.AnonymisedDisplayName;
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Scrub_ReplacesTheActorAndTargetNames_OfThatUserOnly()
    {
        Guid ada = Guid.NewGuid();
        Guid linus = Guid.NewGuid();
        AuditEntry byAda = Entry(ada, "Ada", AuditTargetTypes.User, linus, "Linus");
        AuditEntry aboutAda = Entry(linus, "Linus", AuditTargetTypes.User, ada, "Ada");

        byAda.ScrubUser(ada, Placeholder, null).Should().BeTrue();
        aboutAda.ScrubUser(ada, Placeholder, null).Should().BeTrue();

        byAda.ActorName.Should().Be(Placeholder);
        byAda.TargetName.Should().Be("Linus");
        aboutAda.ActorName.Should().Be("Linus");
        aboutAda.TargetName.Should().Be(Placeholder);
        byAda.ActorUserId.Should().Be(ada, "ids stay so the entry still links to the (anonymised) account");
    }

    [Fact]
    public void Scrub_ReplacesInvitationsAddressedToTheFormerEmail()
    {
        AuditEntry invitation = Entry(Guid.NewGuid(), "Owner", AuditTargetTypes.Invitation, Guid.NewGuid(), "ada@example.com");
        AuditEntry other = Entry(Guid.NewGuid(), "Owner", AuditTargetTypes.Invitation, Guid.NewGuid(), "linus@example.com");

        invitation.ScrubUser(Guid.NewGuid(), Placeholder, "Ada@Example.com").Should().BeTrue();
        other.ScrubUser(Guid.NewGuid(), Placeholder, "ada@example.com").Should().BeFalse();

        invitation.TargetName.Should().Be(Placeholder);
        other.TargetName.Should().Be("linus@example.com");
    }

    [Fact]
    public void Scrub_ReplacesNamesPairedWithTheUserIdInDetails()
    {
        Guid previousOwner = Guid.NewGuid();
        AuditEntry entry = Entry(Guid.NewGuid(), "Admin", AuditTargetTypes.User, Guid.NewGuid(), "New owner",
            $$"""{"previousOwnerId":"{{previousOwner}}","previousOwnerName":"Ada"}""");

        entry.ScrubUser(previousOwner, Placeholder, null).Should().BeTrue();

        entry.Details.Should().Contain(Placeholder).And.NotContain("Ada").And.Contain(previousOwner.ToString());
    }

    [Fact]
    public void Scrub_IsANoOp_ForUnrelatedEntries()
    {
        AuditEntry entry = Entry(Guid.NewGuid(), "Linus", AuditTargetTypes.User, Guid.NewGuid(), "Grace", """{"role":"Admin"}""");

        entry.ScrubUser(Guid.NewGuid(), Placeholder, "ada@example.com").Should().BeFalse();

        entry.ActorName.Should().Be("Linus");
        entry.TargetName.Should().Be("Grace");
        entry.Details.Should().Be("""{"role":"Admin"}""");
    }

    private static AuditEntry Entry(
        Guid actor, string actorName, string targetType, Guid target, string targetName, string? details = null) =>
        AuditEntry.Create(At, actor, actorName, AuditActions.WorkspaceMemberAdded, targetType, target, targetName,
            details: details);
}
