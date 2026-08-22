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

    /// <summary>One board per department. Names are short and
    /// recognisable so the Web UI's sidebar / planner is
    /// immediately legible.</summary>
    public static readonly IReadOnlyList<BoardDefinition> Boards = new List<BoardDefinition>
    {
        new("Engineering", "Sprint board for the platform team — features, infra, bugs.", "Private"),
        new("Product Discovery", "Research, user interviews, prototypes, and quarterly bets.", "Workspace"),
        new("Design System", "Tokens, components, and design ops for the Cardscape shell.", "Workspace"),
        new("Marketing", "Campaigns, content calendar, and growth experiments.", "Workspace"),
        new("Operations", "HR, finance, legal, and the boring-but-critical glue.", "Private"),
        new("Customer Support", "Tickets, escalations, and the public help-centre backlog.", "Workspace"),
    };

    /// <summary>Personas for the demo users. Display names, roles
    /// (mapped to <c>WorkspaceRole</c>), and board memberships are
    /// baked in so the comments and assignments read like a real
    /// team talking to each other.</summary>
    public static readonly IReadOnlyList<Persona> Personas = new List<Persona>
    {
        new("Ada Lovelace", "ada.lovelace", "Engineering Lead", 0, "Admin"),
        new("Linus Pauling", "linus.pauling", "Staff Engineer", 0, "Admin"),
        new("Grace Brewster", "grace.brewster", "Senior Engineer", 0, "Member"),
        new("Hedy Lamarr", "hedy.lamarr", "Product Manager", 1, "Admin"),
        new("Maya Angelou", "maya.angelou", "Design Lead", 2, "Admin"),
        new("Frida Castillo", "frida.castillo", "Product Designer", 2, "Member"),
        new("Diego Velázquez", "diego.velazquez", "Marketing Manager", 3, "Admin"),
        new("Rosa Luxembourg", "rosa.luxembourg", "Content Strategist", 3, "Member"),
        new("Pedro Infante", "pedro.infante", "Operations Lead", 4, "Admin"),
        new("Selena Quintero", "selena.quintero", "Support Specialist", 5, "Admin"),
        new("Tobías Reyes", "tobias.reyes", "DevOps Engineer", 0, "Member"),
        new("Eva Perón", "eva.peron", "Customer Success Manager", 5, "Member"),
    };

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

/// <summary>Static description of a board the seed plants in the
/// demo workspace.</summary>
public sealed record BoardDefinition(string Name, string Description, string Visibility);

/// <summary>Static description of a persona the seed plants in
/// the demo workspace.</summary>
public sealed record Persona(string DisplayName, string EmailLocalPart, string JobTitle, int PrimaryBoardIndex, string WorkspaceRole);
