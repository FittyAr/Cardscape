# Privacy notice template

> **This is a TEMPLATE** for the
> self-hosted deployer to fill in.
> The project does not publish a
> privacy notice on the deployer's
> behalf; the deployer is the data
> controller and is responsible for
> publishing their own privacy notice
> under their own domain.
>
> The template below is the starting
> point; the deployer fills in the
> `<...>` placeholders, runs the
> notice past their legal counsel,
> and publishes the final text.

---

# Privacy notice for `<organisation name>`

_Last updated: `<date>`_

`<organisation name>` (the "**we**",
"**us**", "**our**") operates the
Cardscape kanban service at
`<service URL>` (the "**Service**").
This privacy notice describes what
personal data we collect, why, how
long we keep it, and the rights you
have. It is published in compliance
with the EU General Data Protection
Regulation (GDPR) and any local
data-protection law that applies to
our operations.

## 1. Who we are

- **Data controller**: `<organisation name>`, `<registered address>`, `<contact email>`, `<phone>`.
- **Data Protection Officer (DPO)**: `<name>`, `<email>`, `<phone>` (if appointed).
- **EU representative** (Art. 27 GDPR, if we are not established in the EU): `<name>`, `<email>`.

## 2. What personal data we collect

We collect the personal data you
provide when you register and use
the Service:

- **Account data**: email address,
  display name, password (stored as
  an Argon2id hash, not in clear
  text).
- **MFA data** (if you enable it):
  a TOTP shared secret (encrypted
  at rest), the recovery codes
  (one-way hashed).
- **Content data**: the text you
  type into Cardscape (card titles,
  descriptions, comments, custom
  field values, attachment metadata).
- **Activity data**: the actions you
  take on the Service (created a
  card, moved a card, etc.), the
  timestamp, and the target entity.
- **Audit data** (for security):
  your user id, the IP address, the
  user-agent, the action, the result,
  the timestamp. Retained for 730
  days.
- **Integration data** (if you
  connect a third-party service):
  the OAuth tokens (encrypted at
  rest), the channel / repo /
  calendar metadata.
- **Email integration data** (if you
  enable inbound email): the email
  headers, body, and attachment
  metadata of the messages you
  forward to the Service.
- **Operational logs**: your user id
  (when authenticated), the IP, the
  request path, the response code,
  the response time. Retained for
  30 days.

## 3. Why we collect it (lawful basis)

The lawful basis for processing your
personal data is our **legitimate
interest** (Art. 6(1)(f) GDPR) in
providing a kanban tool to you and
the other members of your workspace.
We have performed a Legitimate
Interest Assessment (LIA) and
documented it in our internal
records; the LIA is available on
request to the DPO.

For specific features, the lawful
basis may differ:

- **MFA enrolment** (the TOTP
  secret) — **consent** (Art. 6(1)(a)
  GDPR); you can withdraw consent
  at any time by disabling MFA.
- **Email integration** (the
  forwarding address) — **consent**;
  you can disable the feature at
  any time.
- **Third-party integrations** (Slack,
  Google, GitHub, Drive) —
  **consent**; you connect the
  service by completing the OAuth
  flow, and you can disconnect at
  any time.
- **AI features** (generate
  description, summarize thread) —
  **consent**; you initiate the
  feature and the LLM provider
  receives only the text you
  selected. You can disable the
  features at the deployment level
  via the configuration.

## 4. Who we share it with

We share your personal data with
the following categories of
recipients:

- **Other members of your workspace**
  (the users you collaborate with).
  They see the content you post in
  the workspace.
- **Workspace administrators**
  (the users who own the workspace).
  They see the audit log and the
  activity feed.
- **Third-party integration
  providers** (Slack, Google,
  GitHub, Microsoft, the email
  provider you choose). They
  receive the data the integration
  needs to operate; their use of
  the data is governed by their
  own privacy notice and their
  contract with us.
- **The LLM provider for the AI
  features** (the provider we
  configure for the deployment).
  They receive the text you
  selected plus a system prompt;
  they do not receive your user
  history or other users' data.
- **Hosting and infrastructure
  providers** (the cloud / on-prem
  host we run the Service on).
  They process the data on our
  behalf under a Data Processing
  Agreement (DPA).
- **The Cardscape maintainer**
  (the project that ships the
  software). The maintainer does
  not hold any of your data; the
  Service is self-hosted.

We do **not** sell your personal
data. We do **not** share your
personal data with advertisers.

## 5. Cross-border transfers

If we transfer your personal data
outside the European Economic Area
(EEA), we do so under one of the
following safeguards:

- **Adequacy decision** — the
  destination country has an
  adequacy decision from the
  European Commission.
- **Standard Contractual Clauses
  (SCCs)** — we have signed SCCs
  with the recipient.
- **Binding Corporate Rules (BCRs)**
  — for transfers within a corporate
  group, BCRs approved by a
  supervisory authority.
- **Explicit consent** — for one-off
  transfers you have explicitly
  consented to.

The current list of cross-border
transfers the Service performs:

- **Slack** (if you integrate):
  Slack Technologies, LLC (USA) —
  SCCs in place.
- **Google** (if you integrate):
  Google LLC (USA) — SCCs in place.
- **GitHub** (if you integrate):
  GitHub, Inc. (USA) — SCCs in
  place.
- **Microsoft** (if you integrate
  OneDrive or Outlook): Microsoft
  Corporation (USA) — SCCs in
  place.
- **LLM provider for AI features**:
  `<LLM provider>`, `<jurisdiction>`
  — `<safeguard>`.
- **Email integration provider**:
  `<email provider>`, `<jurisdiction>`
  — `<safeguard>`.
- **Hosting infrastructure**:
  `<host>`, `<jurisdiction>` —
  `<safeguard>`.

## 6. How long we keep it

The retention periods for the
personal data we collect are:

| Data | Retention |
|---|---|
| Account data (email, display name, password hash) | account lifetime + 30 days soft-delete grace |
| MFA secret (TOTP) | until you disable MFA or delete your account |
| MFA recovery codes | until you use them or delete your account |
| Content data (cards, comments, custom fields) | workspace lifetime + 30 days soft-delete grace |
| Activity feed entries | 365 days (rolling) |
| Audit log entries | 730 days (rolling) |
| API tokens | until you revoke or 90 days of inactivity |
| OAuth 2.0 third-party apps | until you revoke |
| Third-party integration tokens | until you disconnect |
| Email integration inbound payloads | until you remove the address binding |
| Operational logs | 30 days (rolling) |
| Backups | 30 days (rolling) |

At the end of the retention period,
the data is hard-deleted; it cannot
be recovered. The soft-delete grace
period gives you 30 days to change
your mind after you request account
deletion.

## 7. Your rights

You have the following rights under
the GDPR:

- **Right of access** (Art. 15) —
  request a copy of the personal
  data we hold about you.
- **Right to rectification** (Art. 16)
  — correct inaccurate personal data.
- **Right to erasure** (Art. 17) —
  request deletion of your personal
  data (the "right to be forgotten").
- **Right to restriction** (Art. 18)
  — restrict the processing of your
  personal data in certain
  circumstances.
- **Right to data portability**
  (Art. 20) — receive your personal
  data in a portable format.
- **Right to object** (Art. 21) —
  object to processing based on
  legitimate interest.
- **Rights related to automated
  decision-making** (Art. 22) — the
  Service does not perform automated
  decision-making that produces
  legal effects on you.
- **Right to withdraw consent**
  (Art. 7(3)) — if we process your
  data on the basis of consent, you
  can withdraw it at any time
  without affecting the lawfulness
  of the processing before the
  withdrawal.
- **Right to lodge a complaint**
  (Art. 77) — with the supervisory
  authority in your jurisdiction.

To exercise any of these rights,
email the DPO at `<dpo email>`. We
will respond within 30 days.

## 8. Security

We protect your personal data with
the following security measures
(the full list is in the Cardscape
threat model):

- **Encryption in transit** —
  TLS 1.2+ on every endpoint.
- **Encryption at rest** — the
  database is encrypted at the
  storage layer (the deployer's
  hosting provider certifies the
  encryption); the OAuth
  integration tokens are encrypted
  with the data-protection key.
- **Authentication** — Argon2id
  password hashing; optional MFA
  (TOTP, RFC 6238); per-API-token
  rate limit; OAuth 2.0
  (authorisation-code flow) for
  third-party apps.
- **Authorisation** — role-based
  (workspace Owner, Admin, Member,
  Guest) plus resource-based (board
  Admin, Member, Observer; card
  private / public).
- **Audit** — every authentication
  event, every authorisation change,
  every data export, and every
  administrative action is logged
  to the audit log (730-day
  retention).
- **Backup** — encrypted daily
  backups (30-day rolling retention).

## 9. Children

The Service is not directed at
children under 16. We do not
knowingly collect personal data
from children. If you believe a
child has registered, contact the
DPO and we will delete the
account.

## 10. Changes to this notice

We may update this notice from
time to time. The "Last updated"
date at the top of the notice
reflects the current version. If
the change is material, we will
notify you by email (the address
on file) at least 30 days before
the change takes effect.

## 11. Contact

For any question about this
notice or our processing of your
personal data, contact the DPO
at `<dpo email>` or by post at
`<registered address>`.
