# Data Protection Impact Assessment (DPIA) template

> **TEMPLATE** for the data controller
> (the Cardscape deployer) to use when
> performing a Data Protection Impact
> Assessment (DPIA) under Art. 35 GDPR.
> A DPIA is required when the
> processing is **likely to result in a
> high risk to the rights and freedoms
> of natural persons**.
>
> The default Cardscape kanban use
> case is **not** high-risk and does
> not require a DPIA. The processing
> that **is** high-risk (and therefore
> requires a DPIA) includes:
>
> - the AI features (the LLM provider
>   receives user content)
> - large-scale deployment (the
>   controller is processing the
>   personal data of a large number
>   of data subjects)
> - systematic monitoring of data
>   subjects (the workspace activity
>   feed and the audit log
>   systematically record the
>   behaviour of the data subjects)
> - use of new technologies (the
>   AI features qualify)
> - processing of special-category
>   data (Cardscape does not
>   process special-category data
>   today; if the deployer
>   customises the Service to
>   process health data, biometric
>   data, etc., a DPIA is required)
>
> The template below is the starting
> point; the deployer fills in the
> `<...>` placeholders, runs the
> DPIA past their DPO, and submits
> it to the supervisory authority
> **before** the processing starts
> (Art. 36(1) GDPR).

---

# Data Protection Impact Assessment (DPIA)

## 1. Identify the need for a DPIA

- **DPIA owner**: `<name, role>`.
- **DPIA date**: `<date>`.
- **Processing activity**:
  `<e.g. AI-powered card description
  generation for the kanban service>`.
- **Is the processing likely to result
  in a high risk?** (tick all that
  apply)
  - [ ] Systematic and extensive
    evaluation of personal data
    aspects (profiling) on which
    decisions are based that produce
    legal effects.
  - [ ] Processing on a large scale
    of special categories of data
    (Art. 9) or data on criminal
    convictions (Art. 10).
  - [ ] Systematic monitoring of a
    publicly accessible area on a
    large scale.

  If **any** of the above is ticked,
  a DPIA is required.

- **Justification** (why the
  processing is high-risk):
  `<description>`.

## 2. Describe the processing

### 2.1 Nature of the processing

`<Describe what the processing does,
in plain language. Example: "The AI
features accept a card's existing
title, description, and comments as
input; the LLM provider returns a
suggested title, description, or
comment summary. The user reviews
the suggestion in the Web UI and
can accept, edit, or reject it. The
LLM provider receives the input and
the system prompt; the LLM provider
does not receive the user's history,
the user's other cards, or other
users' data.">`

### 2.2 Scope of the processing

- **Categories of data subjects**:
  `<e.g. the controller's employees
  who use the kanban service>`.
- **Categories of personal data**:
  `<e.g. the card title, description,
  and comments the user selects; the
  LLM does not receive the user's
  account data or other cards>`.
- **Volume of data**: `<approximate
  number of cards processed per day,
  approximate number of distinct
  users>`.
- **Duration of the processing**:
  `<the processing is event-driven;
  each invocation processes one card;
  no long-running batch processing>`.

### 2.3 Context of the processing

- **Nature of the controller's
  business**: `<e.g. software
  company, financial services, etc.>`.
- **Sector**: `<e.g. B2B SaaS>`.
- **Relationship with the data
  subjects**: `<e.g. employer /
  employee; controller / customer>`.
- **Vulnerability of the data
  subjects**: `<e.g. employees are
  not a vulnerable population;
  customers may be children if the
  controller sells to consumers>`.

### 2.4 Purposes of the processing

`<Describe the purposes. Example:
"The AI features help the user write
better card titles, descriptions,
and comment summaries, which
improves the clarity of the
workspace and the productivity of
the team.">`

## 3. Assess necessity and proportionality

### 3.1 Lawful basis

`<Identify the lawful basis for the
processing. Example: legitimate
interest (Art. 6(1)(f)) for the
default AI features; consent
(Art. 6(1)(a)) for the user-initiated
AI features.>`

### 3.2 Necessity

`<Is the processing necessary for
the purpose? Could the purpose be
achieved without the processing?
Example: the user could write the
card description manually; the AI
feature is a productivity aid, not
a necessary component.>`

### 3.3 Proportionality

`<Is the processing proportionate to
the purpose? Is the data minimised?
Is the retention limited? Is the
access controlled?>`

### 3.4 Data subject rights

`<Confirm the data subject rights
are honoured. The data subject can
disable the AI features at the
deployment level; the data subject
can request access to the data the
LLM provider received (via the
controller's right-of-access
procedure); the data subject can
request deletion.>`

## 4. Identify and assess risks

| Risk | Likelihood | Severity | Risk level | Mitigation |
|---|---|---|---|---|
| The LLM provider retains the input for training | Low (the LLM provider is configured to not retain; documented in the DPA) | High (the data subject's card content would be used for training) | Medium | The DPA with the LLM provider prohibits retention for training; the controller audits the LLM provider's compliance annually |
| The LLM provider's data is breached | Low (the LLM provider is a tier-1 vendor with their own SOC 2) | High (the data subject's card content would be disclosed to the attacker) | Medium | The controller's DPA requires the LLM provider to notify the controller within 24 hours of any breach; the controller then runs the breach-response runbook |
| The AI suggestion is biased or inaccurate | Medium (LLMs are known to have biases) | Low (the user reviews the suggestion before accepting) | Low | The Web UI makes the suggestion visually distinct from the user-authored content; the user is required to click "Accept" or "Edit" to commit the suggestion |
| The data subject objects to the AI features | Low (the data subject can disable the features) | Low (the objection is honoured) | Low | The deployment-level toggle (`Cardscape:Ai:Enabled = false`) disables the features for all users; the per-user toggle (in the user profile) disables the features for the individual user |

## 5. Identify measures to mitigate the risks

`<For each risk in §4, describe the
mitigation. Examples: the DPA
prohibits retention; the audit
verifies compliance; the UI makes
suggestions visually distinct; the
deployment-level toggle disables the
features; the privacy notice explains
the processing.>`

## 6. Outcome of the DPIA

- [ ] **The processing is
  permissible** with the
  mitigations in place.
- [ ] **The processing is
  permissible** with the
  mitigations in place **and**
  the controller commits to
  additional measures (e.g.
  annual audit, periodic
  re-assessment of the LLM
  provider).
- [ ] **The processing is not
  permissible** and the
  controller will not proceed.

`<Justify the decision.>`

## 7. Consultation with the DPO

- **DPO consulted**: yes / no.
- **DPO name**: `<name>`.
- **DPO opinion**: `<the DPO's
  written opinion on the DPIA>`.

## 8. Consultation with the supervisory authority (if required)

> Art. 36 GDPR requires the
> controller to consult the
> supervisory authority **before**
> the processing starts if the DPIA
> indicates that the processing
> would result in a high risk in
> the absence of measures taken by
> the controller to mitigate the
> risk.

- **Supervisory authority
  consulted**: yes / no.
- **Date of consultation**:
  `<date>`.
- **Reference number**: `<number>`.
- **Outcome of the consultation**:
  `<the supervisory authority's
  response>`.

## 9. Sign-off

- **DPIA owner**: `<name, role, signature, date>`.
- **DPO**: `<name, signature, date>`.
- **Senior management**:
  `<name, role, signature, date>`.

## 10. Review

The DPIA is reviewed at least
annually and on every material
change to the processing (e.g.
switching to a new LLM provider,
adding a new AI feature, processing
a new category of personal data).

| Review date | Reviewer | Outcome | Action items |
|---|---|---|---|
| `<date>` | `<name>` | `<description>` | `<list>` |
