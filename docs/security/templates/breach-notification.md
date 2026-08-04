# Breach notification template

> **TEMPLATE** for the data controller
> (the Cardscape deployer) to use when
> notifying the supervisory authority
> (Art. 33 GDPR) and the affected data
> subjects (Art. 34 GDPR) of a personal
> data breach. The template below is
> the starting point; the deployer fills
> in the `<...>` placeholders, runs the
> notification past their legal counsel,
> and sends the final text within the
> GDPR-mandated timeline (72 hours for
> the supervisory authority, "without
> undue delay" for the data subjects).
>
> The template assumes the breach is
> **reportable** under Art. 33 (the
> breach is likely to result in a risk
> to the rights and freedoms of natural
> persons). For non-reportable breaches,
> the deployer still documents the
> breach internally (Art. 33(5) requires
> documentation of every breach,
> reportable or not) using the same
> template.

---

# Personal data breach notification

## To the supervisory authority (Art. 33 GDPR)

### A. Nature of the breach

- **Breach category** (pick one):
  - [ ] **Confidentiality breach** —
    unauthorised or accidental
    disclosure of, or access to,
    personal data.
  - [ ] **Integrity breach** —
    unauthorised or accidental
    alteration of personal data.
  - [ ] **Availability breach** —
    unauthorised or accidental
    loss of access to, or
    destruction of, personal data.
- **Approximate number of data
  subjects affected**: `<number>`.
- **Approximate number of personal
  data records affected**: `<number>`.
- **Categories of data subjects**:
  `<e.g. employees, customers, etc.>`.
- **Categories of personal data**:
  `<e.g. email, name, content, etc.>`.

### B. Contact point

- **Name**: `<name>`.
- **Role**: `<role>`.
- **Email**: `<email>`.
- **Phone**: `<phone>`.

### C. Likely consequences of the breach

`<Describe the likely consequences for
the affected data subjects. If the
breach involves account credentials,
the consequence is unauthorised access
to the affected accounts. If the
breach involves card content, the
consequence is unauthorised disclosure
of the content. Etc.>`

### D. Measures taken or proposed

`<Describe the measures the deployer
has taken or proposes to take to
address the breach and mitigate its
possible adverse effects. Examples:
rotating the affected credentials,
resetting the affected sessions,
notifying the affected users,
deploying a hotfix, restoring from
backup, etc.>`

### E. Timeline

- **Date and time of the breach**:
  `<timestamp UTC>`.
- **Date and time of detection**:
  `<timestamp UTC>`.
- **Date and time of containment**:
  `<timestamp UTC>`.
- **Date and time of this notification**:
  `<timestamp UTC>` (must be within
  72 hours of detection).

### F. Reasoning for any delay

`<If this notification is more than
72 hours after detection, explain the
delay. The GDPR allows a delay if the
information was not available within
72 hours; the notification is made in
phases.>`

---

## To the affected data subjects (Art. 34 GDPR)

> Art. 34 requires notification to
> the data subjects when the breach
> is likely to result in a **high
> risk** to the rights and freedoms
> of natural persons. The notification
> must be in clear and plain language,
> describe the nature of the breach,
> and contain the same information as
> the supervisory-authority
> notification.

### Subject

Important security notice about your
`<organisation name>` account

### Body

Dear `<name>`,

We are writing to inform you of a
security incident at `<organisation name>`
that may have affected your personal
data. We take the security of your
data seriously and are notifying you
in compliance with the EU General Data
Protection Regulation (GDPR).

#### What happened

`<Describe the breach in plain
language. Avoid jargon.>` Example:

> On `<date>` at `<time>` we detected
> `<description>`. The incident
> affected `<number>` accounts,
> including yours.

#### What information was involved

`<List the categories of personal
data affected for the data subjects
being notified. Be specific: "your
email address, display name, and the
content of cards you created between
dates X and Y" rather than "some
personal data".>`

#### What we are doing

`<Describe the measures the deployer
has taken. Examples: rotated
credentials, deployed a security
patch, engaged a forensic firm,
notified law enforcement, etc.>`

#### What you can do

`<Describe the steps the data subject
should take. Examples: change your
password, enable MFA, review your
account activity, etc.>`

#### Who to contact

If you have any questions, please
contact us at `<email>` or `<phone>`.
Our Data Protection Officer is
`<dpo name>`, reachable at `<dpo email>`.

You have the right to lodge a
complaint with the supervisory
authority in your jurisdiction. The
supervisory authority for `<country>`
is `<name>`, reachable at `<url>`.

We apologise for the incident and
are committed to preventing it from
happening again.

Sincerely,
`<name>`
`<role>`
`<organisation name>`

---

## Internal documentation (Art. 33(5) GDPR)

> The controller must document every
> personal data breach, **including
> the facts surrounding the breach,
> its effects, and the remedial
> action taken**. The internal
> documentation is not sent to the
> supervisory authority; the
> supervisory authority may request
> it during an audit.

### Breach record

- **Breach ID**: `<id>`.
- **Date and time of the breach**:
  `<timestamp UTC>`.
- **Date and time of detection**:
  `<timestamp UTC>`.
- **Date and time of containment**:
  `<timestamp UTC>`.
- **Detected by**: `<name / system>`.
- **Detection method**: `<how the
  breach was detected>`.
- **Affected systems**: `<list>`.
- **Affected data**: `<categories
  of personal data>`.
- **Affected data subjects**:
  `<number> and categories`.
- **Root cause**: `<description>`.
- **Containment actions**: `<list>`.
- **Remediation actions**: `<list>`.
- **Notification to the supervisory
  authority**: yes / no, on `<date>`.
- **Notification to the data
  subjects**: yes / no, on `<date>`.
- **Lessons learned**: `<description>`.
- **Follow-up actions**: `<list>`.

---

## Templates for the messages to specific audiences

> Beyond the supervisory authority and
> the data subjects, the deployer may
> need to notify:
>
> - the hosting provider (so the
>   provider can investigate the
>   infrastructure layer)
> - the Cardscape maintainer (if the
>   breach was caused by a Cardscape
>   vulnerability; the maintainer
>   can issue a security advisory)
> - the third-party integration
>   provider (if the breach was
>   caused by the integration
>   provider's side)
> - law enforcement (if the breach
>   involves criminal conduct)
> - cyber insurance carrier (if the
>   deployer has a policy)
>
> Each of these notifications has a
> different content focus. The
> template above is for the GDPR
> audience; the others are not
> covered here.
