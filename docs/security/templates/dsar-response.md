# Data Subject Access Request (DSAR) response template

> **TEMPLATE** for the data controller
> (the Cardscape deployer) to use when
> responding to a Data Subject Access
> Request (DSAR) under Art. 15 GDPR
> (right of access). The deployer
> receives the request by email (or
> post), verifies the identity of the
> requester, generates the export using
> the Service's `GET /api/users/{id}/export`
> endpoint, and sends the response
> within **30 days** of the request
> (extendable by 60 days for complex
> requests, with notice to the
> requester).
>
> The template below is the starting
> point for the cover letter that
> accompanies the export bundle.

---

# Data Subject Access Request response

## Request details

- **Requester name**: `<name>`.
- **Requester email** (the email on
  file): `<email>`.
- **Requester user id** (if known):
  `<id>`.
- **Date the request was received**:
  `<date>`.
- **Date this response is sent**:
  `<date>` (must be within 30 days
  of receipt).
- **Identity verification**:
  `<description of how the requester
  was verified, e.g. password
  re-authentication, security
  questions, in-person ID check>`.

## Right exercised

- [ ] **Right of access** (Art. 15) —
  copy of the personal data we hold.
- [ ] **Right to data portability**
  (Art. 20) — portable copy in a
  structured, machine-readable
  format.

The response covers both rights by
default; the requester can opt out
of portability.

## Cover letter

Dear `<name>`,

Thank you for your request of
`<date>` regarding the personal data
we hold about you.

We confirm that you are a registered
user of the Service. We have
processed your request and the
personal data we hold about you is
attached to this email as a JSON
file (`export-<user-id>-<date>.json`).

### What the export contains

The export contains every personal
data field associated with your
account and your activity in the
Service. Specifically:

- **Account data**: your email
  address, display name, registration
  date, last login date.
- **Workspace and board data**: the
  workspaces you are a member of and
  your role in each; the boards you
  are a member of and your role in
  each.
- **Content you authored**: every
  card you created, every comment
  you posted, every custom field
  value you set, every attachment
  you uploaded.
- **Activity feed entries**: the
  actions you took on the Service
  (created a card, moved a card,
  etc.), the timestamp, and the
  target entity.
- **Audit log entries** (security):
  the authentication events
  associated with your account
  (logins, logouts, password
  changes, MFA events).
- **API tokens**: the API tokens
  you have created (the hashed
  secret is **not** included for
  security; the token prefix and
  the metadata are).
- **OAuth apps**: the OAuth 2.0
  third-party apps you have
  registered (the hashed client
  secret is **not** included for
  security; the metadata is).
- **Third-party integration
  connections**: the third-party
  services you have connected
  (Slack, Google, GitHub, etc.);
  the OAuth tokens are **not**
  included for security; the
  connection metadata is.

### What the export does NOT contain

The export does not contain the
following for security reasons:

- **Password hash** — we never
  disclose the password hash.
- **API token secrets** — the
  plaintext secrets are never
  stored; we only store the hash.
- **OAuth client secrets** — same.
- **Third-party integration
  tokens** — the plaintext tokens
  are never returned; we only store
  the encrypted form.
- **MFA TOTP secret** — the
  plaintext secret is never
  returned; we only store the
  encrypted form.
- **Audit log entries for other
  users** — the export contains
  only entries where you are the
  subject; entries where you are
  the actor (e.g. you administered
  another user) are included.

### Your other rights

The export responds to your right
of access (Art. 15) and your right
to data portability (Art. 20). You
also have the following rights under
the GDPR:

- **Right to rectification**
  (Art. 16) — if any of the data
  in the export is inaccurate,
  email us at `<dpo email>` and
  we will correct it.
- **Right to erasure** (Art. 17)
  — if you want to delete your
  account, email us at
  `<dpo email>` and we will
  initiate the soft-delete
  process (30-day grace period).
- **Right to restriction** (Art. 18)
  — if you want to restrict the
  processing of your data, email
  us at `<dpo email>` and we
  will set the restriction flag.
- **Right to object** (Art. 21) —
  if you object to the processing
  for legitimate interest, email
  us at `<dpo email>` and we
  will review the objection.
- **Right to lodge a complaint**
  (Art. 77) — with the
  supervisory authority in your
  jurisdiction. The supervisory
  authority for `<country>` is
  `<name>`, reachable at `<url>`.

### Contact

If you have any questions about
this response, please contact the
Data Protection Officer at
`<dpo email>` or by post at
`<registered address>`.

Sincerely,
`<name>`
`<role>`
`<organisation name>`

---

## Export bundle (attached)

The response email includes the
file `export-<user-id>-<date>.json`.
The file is encrypted with the
requester's PGP key if one is on
file; otherwise it is sent as a
password-protected ZIP with the
password delivered by SMS to the
requester's phone number on file.

The export bundle is the JSON
output of the Service's
`GET /api/users/{id}/export`
endpoint. The endpoint is
admin-only; the admin generates
the export on behalf of the
requester and attaches it to the
response email.

## Internal log

The DSAR response is logged in the
Service's audit log as
`dsar.responded` with the
requester's user id and the
response date. The log is the
controller's record of the
response, in case the supervisory
authority asks for it during an
audit.
