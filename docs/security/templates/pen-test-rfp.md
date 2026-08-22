# Penetration test — Request for Proposal (RFP) template

> **TEMPLATE** the deployer (the data
> controller) sends to one or more
> accredited penetration-testing firms
> when commissioning an external
> security assessment. The project
> does not self-certify; an independent
> third-party firm issues the report
> and the deployer uses it as the
> auditor-facing evidence for SOC 2
> CC4.1, CC7.1, and the equivalent
> ISO 27001 / GDPR Art. 32 controls.

## How to use this template

1. **Copy the template** to your
   own document (do not edit in
   place; the project ships the
   template as a reference).
2. **Fill in the placeholders**
   (`<organisation>`, `<environment>`,
   etc.) with your deployment's
   details.
3. **Attach** the documents listed
   in §0 (the project's threat model,
   the SOC 2 readiness doc, the
   architecture diagram, etc.) — they
   are the firm-facing system
   description the firm needs to
   scope the engagement.
4. **Send the package** to 2-3
   accredited firms (CREST, OSCP /
   OSCE, GIAC, or a local equivalent
   in your jurisdiction). The
   accreditation matters because the
   auditor will ask "was the firm
   accredited" — an unaccredited firm
   is a weak control.
5. **Keep the responses** in your
   records. The auditor may ask for
   the firms' responses, the
   selection criteria, and the
   selection decision.

---

## 0. Documents to attach

The RFP body assumes the firm has
read the following project documents.
Attach them as a single zip:

- `01-threat-model.md` (STRIDE
  per-asset assessment)
- `02-secure-coding-checklist.md`
  (the OWASP-mapped developer
  checklist)
- `04-soc2-readiness.md` (control
  mapping; §9 is the
  system-description input the
  firm needs)
- `05-vulnerability-disclosure.md`
  (the coordinated disclosure
  policy; the firm will need a
  secure channel for findings)
- The architecture diagram for the
  deployer's deployment (the
  project ships a reference diagram;
  the deployer annotates the
  differences)
- The top 5 user flows in the
  deployer's deployment (the
  project ships the canonical flows;
  the deployer picks the ones that
  are exposed in their deployment)
- A list of any custom integrations
  the deployer has added (the
  firm should know what is
  in-scope vs. third-party)

## 1. Organisation background

`<organisation>` is the data
controller for a self-hosted
Cardscape deployment. Cardscape is
an open-source project management
platform. The deployment hosts
`<user count>` active users across
`<workspace count>` workspaces and
serves `<data residency region>` as
the data-residency region.

The data processed is project
management metadata: boards, cards,
comments, attachments, audit trail,
and authentication artefacts. The
deployment does **not** process
payment-card data, special-category
data under GDPR Art. 9, or
health/financial data — the
firm can scope the assessment
accordingly.

## 2. Scope

### 2.1 In-scope

- The Cardscape API process
  (ASP.NET Core 10, listens on
  `<port>`)
- The Cardscape MCP process
  (ASP.NET Core 10, listens on
  `<port>`, exposed to AI clients
  over the MCP protocol)
- The Cardscape Web process
  (Blazor Server, listens on
  `<port>`)
- The reverse proxy in front of
  the API / MCP / Web (e.g.
  Caddy, nginx, Cloudflare —
  whichever the deployer uses)
- The SQLite database
  backing the API
- The object-storage adapter (S3,
  MinIO, local disk — whichever
  the deployer uses)
- The WebAuthn / TOTP / OAuth
  flows exposed by the API
- The WebSocket / SignalR
  real-time channel
- The OAuth 2.0 surface (client
  credentials + authorisation
  code grants, the firm should
  test the authorisation server
  surface specifically)
- The MCP server's resource
  subscription + tool invocation
  surface
- The retention sweeper (the
  background service that
  anonymises + purges data — the
  firm should verify the soft-delete
  contract and the hard-delete
  schedule)
- The audit log / Serilog pipeline
  (the firm should verify log
  integrity, log retention, and
  log redaction)

### 2.2 Out of scope

- The cardscape.dev hosted
  service (the project runs a
  hosted offering; that is
  scoped to a separate engagement
  the project commissions)
- Third-party services the
  deployer uses (Google Calendar
  API, Slack, GitHub, SendGrid /
  Mailgun, Sentry, etc.) — the
  firm may flag concerns but the
  third-party is responsible for
  their own controls
- The underlying operating
  system, the cloud account, the
  CDN, the DNS — those are the
  deployer's existing controls
  and should be tested in a
  separate engagement
- Denial-of-service attacks (the
  firm may test for amplification
  but should not run volumetric
  DDoS — the deployment is in
  production)

### 2.3 Credentials provided

The deployer will provide:

- 1 admin account (the firm uses
  it to exercise the admin surface;
  the account is created in the
  test environment, not in
  production)
- 5 user accounts across 3
  workspaces (so the firm can test
  the workspace / board / card
  permission model)
- 1 API token (the firm uses it
  to exercise the API surface
  directly)
- 1 OAuth 2.0 client (the firm
  uses it to exercise the OAuth
  surface; the client is created
  in the test environment with
  the full scope)
- 1 MCP client (the firm uses it
  to exercise the MCP surface)

The test environment mirrors
production in every way that
matters for the assessment (same
Cardscape version, same
configuration, same integrations
enabled, same auth providers
configured). The only difference
is the data is synthetic and the
secrets are disposable.

## 3. Engagement type

`<engagement type>` — typically
"grey-box" (the firm has the
credentials listed in §2.3 and
knows the architecture; the firm
does **not** have source-code
access — the firm treats the
deployment as an opaque system
unless they ask for source and
the deployer agrees). The grey-box
engagement matches what an
attacker with a single compromised
user account would see; it is the
right trade-off for a project
management platform where most
realistic attackers start from
phishing a user.

## 4. Methodology

The firm should use a methodology
that maps to OWASP ASVS Level 2
(application) + the relevant
sections of the OWASP Top 10 for
LLM Applications (the MCP surface
exposes LLM clients to
prompt-injection and
tool-confusion attacks; the firm
should cover these). The
deliverable should also map to:

- OWASP ASVS v4.0.3 — the
  application security controls
  in §V1-V14
- NIST SP 800-115 — the technical
  guide for information security
  testing
- PTES (Penetration Testing
  Execution Standard) — the
  end-to-end engagement framework
- MITRE ATT&CK — the firm should
  tag findings by the ATT&CK
  technique they exercise
  (e.g. T1078 valid accounts,
  T1190 exploit-public-facing-app)

## 5. Engagement window

`<start date>` to `<end date>` —
typically 5-10 business days for
an application-layer assessment of
this size, with 1-2 weeks of
reporting. The firm should
specify:

- the number of consultant-days
- the lead consultant's
  accreditation (CREST, OSCP, GIAC)
- the report timeline (initial
  findings within 24h of discovery
  for critical issues, full draft
  within `<days>` days, final
  report within `<days>` days)

## 6. Deliverables

The firm should deliver:

1. **Daily status report** during
   the engagement (1-2 paragraphs,
   no findings — just status)
2. **Critical / high findings
   within 24h** of discovery (the
   deployer needs to start the
   remediation clock immediately
   for anything that would fail
   an audit or be exploitable in
   production)
3. **Full draft report** within
   `<days>` days of engagement end
4. **Final report** within
   `<days>` days of receiving the
   deployer's comments on the
   draft
5. **Re-test pass** for every
   finding within `<retest
   window>` days of the deployer's
   fix (the firm confirms the fix
   is real, not a paper change)
6. **Letter of attestation** the
   auditor can rely on (the
   letter states the firm is
   accredited, the scope, the
   window, the methodology, and
   the firm's opinion on the
   residual risk)

The final report should follow
the template in §A.

## 7. Notification and incident
##    handling

The firm will discover real
findings, some of which may be
actively exploitable. The
notification flow:

1. The firm sends the finding to
   the deployer's secure channel
   (PGP-encrypted email, the
   deployer's bug-bounty platform,
   or the deployer's secure
   intake — whichever the
   deployer prefers)
2. The firm does **not** disclose
   the finding to anyone outside
   the firm and the deployer
   until the deployer approves
   disclosure (the
   vulnerability-disclosure policy
   §6 covers coordinated
   disclosure)
3. The firm does **not** retain
   the project's data, the
   credentials, or any artefact
   that could be used to attack
   the deployment after the
   engagement. The firm provides
   a written statement of
   data / credential destruction

## 8. Confidentiality

The firm signs the deployer's
mutual NDA before receiving the
RFP. The deployer marks the
documents shared with the firm
"CONFIDENTIAL" and the firm
returns / destroys them at the
end of the engagement per the
NDA.

## 9. Commercial

The deployer should expect to
pay `<currency> <amount>` for an
engagement of this size. The
firm's proposal should itemise:

- pre-engagement scoping
  (1-2 days, fixed fee)
- the engagement itself
  (consultant-days × day rate)
- the report writing
  (1-2 days, fixed fee)
- the re-test pass
  (per-finding, capped at the
  total engagement cost)
- travel + expenses (if any)

The deployer should reject
proposals that do not itemise
this way (an un-itemised
proposal hides scope creep).

## 10. Selection criteria

The deployer scores the firms on:

| Criterion | Weight |
| --- | --- |
| CREST / OSCP / GIAC accreditation of the lead consultant | 20% |
| Hands-on experience with ASP.NET Core + Blazor (not just generic web app) | 15% |
| Hands-on experience with the MCP / LLM-application attack surface (prompt injection, tool confusion) | 15% |
| Methodology mapping (OWASP ASVS + ATT&CK + NIST 800-115) | 15% |
| Re-test included in the proposal (not as an upsell) | 10% |
| Letter of attestation that the auditor can rely on | 10% |
| References from comparable open-source / SaaS deployments | 10% |
| Commercial (itemised, capped) | 5% |

A firm that scores low on the
ASP.NET Core / MCP / LLM
experience is a yellow flag — the
project's surface is unusual and
generic web-app firms tend to
miss the protocol-level issues
(unique token formats, the
resource-subscription handshake,
the OAuth client-credentials +
authorisation-code split, the
cross-process broadcaster).

## 11. Re-test and re-engagement

The firm should be available for
a re-test pass within `<retest
window>` days of the deployer's
fix. The deployer should plan
for a re-engagement 12 months
after the initial engagement
(annual) and after every major
release (the project ships
quarterly releases; the
deployer should re-engage the
firm for the major releases
that touch the security surface
— the auth surface, the MCP
surface, the admin surface).

---

## Appendix A — Final report
##             template

The firm's final report should
follow this structure (the
deployer is free to adapt, but
the auditor will ask for most
of these sections):

1. **Executive summary**
   (1 page, the deployer's board
   reads this)
2. **Engagement summary** (scope,
   window, methodology, the firm's
   opinion on residual risk)
3. **Findings** (per finding):
   - ID
   - Title
   - Severity (Critical / High /
     Medium / Low / Info)
   - CWE (Common Weakness
     Enumeration)
   - OWASP ASVS section
   - MITRE ATT&CK technique (if
     applicable)
   - Affected component(s)
   - Description (what the
     finding is, how the firm
     found it, why it matters)
   - Evidence (screenshots, HTTP
     request / response snippets,
     the firm's working PoC — the
     PoC is included for the
     deployer's red team, not for
     public disclosure)
   - Impact (what an attacker
     could do, what data could
     leak, what the blast radius
     is)
   - Remediation (specific,
     actionable; the firm should
     point at the project's
     existing secure-coding
     checklist or the relevant
     ADR if the project has
     already documented the
     fix)
   - References (the project
     docs, OWASP, NIST, etc.)
4. **Positive findings** (what
   the firm found that the
   project does well — the
   auditor will appreciate the
   balance)
5. **Out-of-scope observations**
   (things the firm noticed but
   were out of scope; the
   deployer may want a separate
   engagement to address these)
6. **Methodology detail** (the
   full scope, the test cases
   the firm ran, the tools the
   firm used, the standards the
   firm mapped to)
7. **Letter of attestation**

---

## Disclaimer

This template is provided for
**convenience only**. The project
does not provide security
consulting; the deployer is
responsible for their own
procurement and the engagement
contract. The firm selection
criteria in §10 are a starting
point, not a substitute for the
deployer's own procurement
process.
