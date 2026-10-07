namespace Cardscape.Seeder.Company;

/// <summary>
/// The fictional software studio the seeder plants in the
/// database. Everything else (board names, card titles, comment
/// bodies, automation rules, webhook payloads) hangs off this
/// single source of truth so a re-seed produces a coherent
/// dataset instead of a random one.
/// </summary>
public static class NexoraStudios
{
    public const string CompanyName = "Nexora Studios";
    public const string Slug = "nexora";
    public const string DemoEmailDomain = "nexora.example";

    public const string DemoAdminEmail = "ada.lovelace@nexora.example";
    public const string DemoAdminPassword = "Nexora!Demo-2026";

    /// <summary>The single demo workspace the seed plants. Re-seeding
    /// is idempotent within a single run; cross-run idempotency is
    /// the operator's responsibility (use the wipe flag).</summary>
    public const string WorkspaceName = "Nexora Studios HQ";

    /// <summary>Second, smaller workspace so the workspace switcher and
    /// per-workspace scoping have something to show.</summary>
    public const string LabsWorkspaceName = "Nexora Labs";

    /// <summary>One board per department, plus an archived one. Teams
    /// (persona keys) decide board membership and who works the cards.
    /// Ada (the demo login) is deliberately absent from Marketing so the
    /// read-only, workspace-visible experience can be explored too.</summary>
    public static readonly IReadOnlyList<BoardBlueprint> Boards =
    [
        new("Engineering", "Sprint board for the platform team — features, infra, bugs.",
            BoardVisibility.Private, Color.Palette.Blue, "ada.lovelace",
            ["linus.pauling", "grace.brewster", "tobias.reyes", "hedy.lamarr"],
            BoardFeatures.CustomFields | BoardFeatures.Voting | BoardFeatures.Aging),
        new("Product Discovery", "Research, user interviews, prototypes, and quarterly bets.",
            BoardVisibility.Workspace, Color.Palette.Purple, "hedy.lamarr",
            ["ada.lovelace", "maya.angelou", "frida.castillo", "eva.peron"],
            BoardFeatures.Voting | BoardFeatures.CustomFields),
        new("Design System", "Tokens, components, and design ops for the Cardscape shell.",
            BoardVisibility.Workspace, Color.Palette.Pink, "maya.angelou",
            ["frida.castillo", "grace.brewster", "ada.lovelace"],
            BoardFeatures.CustomFields),
        new("Marketing", "Campaigns, content calendar, and growth experiments.",
            BoardVisibility.Workspace, Color.Palette.Orange, "diego.velazquez",
            ["rosa.luxembourg", "hedy.lamarr", "eva.peron"],
            BoardFeatures.Voting),
        new("Operations", "HR, finance, legal, and the boring-but-critical glue.",
            BoardVisibility.Private, Color.Palette.Green, "pedro.infante",
            ["ada.lovelace", "tobias.reyes"],
            BoardFeatures.Repeater | BoardFeatures.CustomFields),
        new("Customer Support", "Tickets, escalations, and the public help-centre backlog.",
            BoardVisibility.Workspace, Color.Palette.Red, "selena.quintero",
            ["eva.peron", "ada.lovelace", "grace.brewster"],
            BoardFeatures.Aging | BoardFeatures.CustomFields | BoardFeatures.Repeater),
    ];

    /// <summary>Last year's offsite board, kept archived so the archive views are populated.</summary>
    public static readonly BoardBlueprint ArchivedBoard = new(
        "Offsite 2025", "Logistics for the 2025 team offsite in Lisbon.",
        BoardVisibility.Workspace, Color.Palette.Yellow, "pedro.infante",
        ["ada.lovelace", "hedy.lamarr"], BoardFeatures.None);

    /// <summary>The single board of the Labs workspace.</summary>
    public static readonly BoardBlueprint LabsBoard = new(
        "Hackathon 2026", "Weekend prototypes that might become features.",
        BoardVisibility.Workspace, Color.Palette.Lime, "hedy.lamarr",
        ["ada.lovelace", "linus.pauling", "frida.castillo"], BoardFeatures.Voting);

    /// <summary>The kanban flow every seeded board uses, left to right.</summary>
    public static readonly IReadOnlyList<string> Workflow = ["Backlog", "Doing", "Review", "Done"];

    /// <summary>Team members. The first persona owns the HQ workspace and is the demo admin.</summary>
    public static readonly IReadOnlyList<Persona> Personas =
    [
        new("Ada Lovelace", "ada.lovelace", "Engineering Lead", WorkspaceRole.Admin, "cardscape-classic", AppearanceMode.Dark),
        new("Linus Pauling", "linus.pauling", "Staff Engineer", WorkspaceRole.Admin, "software", AppearanceMode.Dark),
        new("Grace Brewster", "grace.brewster", "Senior Engineer", WorkspaceRole.Member, "standard", AppearanceMode.Light),
        new("Hedy Lamarr", "hedy.lamarr", "Product Manager", WorkspaceRole.Admin, "material", AppearanceMode.Light),
        new("Maya Angelou", "maya.angelou", "Design Lead", WorkspaceRole.Admin, "humanistic", AppearanceMode.Light),
        new("Frida Castillo", "frida.castillo", "Product Designer", WorkspaceRole.Member, "humanistic", AppearanceMode.Dark),
        new("Diego Velázquez", "diego.velazquez", "Marketing Manager", WorkspaceRole.Admin, "default", AppearanceMode.Light),
        new("Rosa Luxembourg", "rosa.luxembourg", "Content Strategist", WorkspaceRole.Member, "cardscape-classic", AppearanceMode.Light),
        new("Pedro Infante", "pedro.infante", "Operations Lead", WorkspaceRole.Admin, "standard", AppearanceMode.Light),
        new("Selena Quintero", "selena.quintero", "Support Specialist", WorkspaceRole.Admin, "software", AppearanceMode.Light),
        new("Tobías Reyes", "tobias.reyes", "DevOps Engineer", WorkspaceRole.Member, "cardscape-classic", AppearanceMode.Dark),
        new("Eva Perón", "eva.peron", "Customer Success Manager", WorkspaceRole.Observer, "material", AppearanceMode.Light),
    ];

    /// <summary>A former employee: deactivated, still referenced by old cards and comments.</summary>
    public static readonly Persona FormerEmployee =
        new("Charles Babbage", "charles.babbage", "Former Staff Engineer", WorkspaceRole.Member, "default", AppearanceMode.Light);

    /// <summary>An external contractor with a restricted account who joined through an invitation.</summary>
    public static readonly Persona Contractor =
        new("Katherine Johnson", "katherine.johnson", "Contract Data Analyst", WorkspaceRole.Member, "default", AppearanceMode.Light);

    /// <summary>Card titles per board. Index 0 is the Engineering
    /// board, etc. Titles are short enough to fit a column
    /// without truncation in the standard card list view.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CardTitlesByBoard =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["Engineering"] = new List<string>
            {
                "Migrate auth pipeline to OAuth refresh tokens",
                "Investigate EF Core slow query on /api/cards",
                "Add OpenTelemetry traces to BackgroundJobDispatcher",
                "Roll out the new pagination on /api/boards",
                "Cache WebhookDelivery rows in Redis",
                "Reduce cards list payload by 40%",
                "Add Polly retry around the SMTP transport",
                "Profile SignalR hub under 1k concurrent clients",
                "Replace the legacy JSON config with cardscape.yaml",
                "Bump .NET runtime to 10.0.4",
                "Document the new /api/internal/translate endpoint",
                "Triage P1 from last week's release",
            },
            ["Product Discovery"] = new List<string>
            {
                "User interviews: Kanban switchers (round 4)",
                "Pricing experiment: free tier up to 5 seats",
                "Prototype: AI auto-summarise card thread",
                "Validate SCIM for enterprise tier",
                "Map the onboarding flow for solo creators",
                "Quarterly OKR draft: Activation",
                "Card mirror across boards: a deeper look",
                "User research: dark mode priority",
                "Competitor teardown: Linear / Height",
                "Roadmap workshop with Customer Council",
            },
            ["Design System"] = new List<string>
            {
                "Audit the Radzen theme tokens",
                "Design tokens: spacing scale revamp",
                "Component: empty-state pattern",
                "Icon set: replace Heroicons with Phosphor",
                "Figma library v0.4 publish",
                "Component: accessible dialog with focus trap",
                "Migrate the old corporate palette to Radzen standard",
                "Design QA: home page hero section",
                "Component spec: data grid column resizing",
            },
            ["Marketing"] = new List<string>
            {
                "Q3 campaign: 'Switch from Kanban in a weekend'",
                "Blog post: building a kanban MCP for AI agents",
                "Newsletter: open source retrospective",
                "Landing page: enterprise tier",
                "Webinar planning: SCIM and SSO",
                "Co-marketing with GitHub for the MCP launch",
                "Case study: Cardscape at Nexora",
                "SEO: long-tail keywords round 2",
            },
            ["Operations"] = new List<string>
            {
                "Renew the AWS contract for FY26",
                "Annual privacy policy review",
                "Onboard the new DevOps contractor",
                "Insurance renewal: cyber liability",
                "Update the employee handbook for hybrid work",
                "Quarterly tax filing prep",
                "Vendor security review: Sentry",
                "Office lease renewal decision",
            },
            ["Offsite 2025"] = new List<string>
            {
                "Book the Lisbon venue",
                "Flights and visas for the team",
                "Agenda: strategy day",
                "Retro on the offsite budget",
            },
            ["Hackathon 2026"] = new List<string>
            {
                "Voice notes on cards",
                "Board heatmap of stale cards",
                "Slash commands in the card composer",
                "Offline mode for the mobile web",
                "Auto-tagging with embeddings",
            },
            ["Customer Support"] = new List<string>
            {
                "Escalation: SSO loop for Okta + SAML",
                "Help-centre article: Webhook signing",
                "Top-10 tickets of the month",
                "Onboarding call follow-up template",
                "Outage postmortem: 2026-08-02",
                "Triage new SCIM token rotation flow",
                "Refresh the FAQ for the new AI features",
                "Pilot: in-app chat for paying tier",
            },
        };

    /// <summary>Comment templates per card-index-in-board. Used so
    /// the seeded comment threads look like the team is
    /// actually talking.</summary>
    /// <summary>Comments that @mention a teammate; <c>{0}</c> is the
    /// mentioned display name. Each one yields a Mentioned notification.</summary>
    public static readonly IReadOnlyList<string> MentionBodies =
    [
        "@{0} can you take a look at this before standup?",
        "Looping in @{0} — you know this area better than anyone.",
        "@{0} I think this is blocked on your review, no rush.",
        "Thanks @{0}! That fixed it on my side too.",
    ];

    /// <summary>Checklist templates; seeded cards pick one by index.</summary>
    public static readonly IReadOnlyList<(string Title, IReadOnlyList<string> Items)> Checklists =
    [
        ("Definition of done", ["Spec reviewed", "Implementation merged", "Tests green on CI", "Docs updated", "Released behind a flag"]),
        ("Acceptance criteria", ["Happy path works end to end", "Empty state handled", "Error state handled", "Keyboard accessible"]),
        ("Launch checklist", ["Copy approved", "Assets ready", "Stakeholders notified", "Metrics dashboard live"]),
    ];

    public static readonly IReadOnlyList<string> CommentBodies = new List<string>
    {
        "Picked this up — I think we can use the same helper we wrote for the boards endpoint. Let me draft a PR by EOD.",
        "Heads up: there's an existing ticket (#188) that touches the same code path. Might be worth merging first.",
        "Loving the scope here. Can we add a quick test for the empty-state path before merging?",
        "I tried this against a 10k-card workspace on staging and it cut p95 in half. Numbers in the PR.",
        "Pulled the design tokens from the Figma file — attached. Should we also update the docs site?",
        "Out of curiosity, has anyone run this against PostgreSQL yet? I want to make sure the migration is portable.",
        "Looped in @legal because this touches a GDPR surface. They'll get back to us tomorrow.",
        "Bumping priority. We have two enterprise prospects waiting on this exact feature.",
        "Pairing on this with the new hire tomorrow morning. Will sync back to the board afterwards.",
        "Closing as duplicate — see the older thread. The fix shipped in 1.0.0.",
    };
}

/// <summary>Optional board extensions a blueprint switches on.</summary>
[Flags]
public enum BoardFeatures
{
    None = 0,
    CustomFields = 1,
    Voting = 2,
    Repeater = 4,
    Aging = 8,
}

/// <summary>Static description of a board the seed plants.</summary>
public sealed record BoardBlueprint(
    string Name,
    string Description,
    BoardVisibility Visibility,
    Color Color,
    string OwnerKey,
    IReadOnlyList<string> TeamKeys,
    BoardFeatures Features)
{
    /// <summary>Owner first, then the team.</summary>
    public IEnumerable<string> MemberKeys => [OwnerKey, .. TeamKeys];

    public bool Has(BoardFeatures feature) => Features.HasFlag(feature);
}

/// <summary>A demo user. <see cref="Key"/> is the e-mail local part.</summary>
public sealed record Persona(
    string DisplayName,
    string Key,
    string JobTitle,
    WorkspaceRole WorkspaceRole,
    string Theme,
    AppearanceMode Mode)
{
    public string Email => $"{Key}@{NexoraStudios.DemoEmailDomain}";
}
