# Incident response

> The playbook for when something goes wrong in a
> self-hosted Cardscape deployment. The playbook covers
> the four most common incident types: the API is down,
> the database is corrupted, the MCP server is
> misbehaving, and a security vulnerability has been
> disclosed. The playbook is meant to be skimmed once
> (so you know it exists) and used under pressure (so the
> steps are short and unambiguous).
>
> This is a **runbook**. It is meant to be followed step by
> step during an incident, in order. It is not meant to
> be read cover to cover.

---

## 1. The principle

An incident is **a problem that is happening now**. The
goal of incident response is to **stop the bleeding first,
understand the cause second, fix the root cause third, and
document the learning fourth**. The order is non-negotiable;
the order is what separates a calm response from a
chaotic one.

The four phases:

1. **Detect.** The alert fires, or the user reports a
   problem, or the maintainer notices something is off.
2. **Mitigate.** Stop the bleeding. Get the system back
   to a working state, even if the working state is
   degraded.
3. **Resolve.** Find the root cause. Fix it. Restore the
   system to its full state.
4. **Learn.** Document the incident. File the follow-up
   issues. Update the runbooks. Communicate the learning
   to the community.

The four phases are not always sequential. Detection can
happen while mitigating (the alert that fired during
mitigation tells you the mitigation worked). Resolution
can find a new incident. Learning can be ongoing. The
principle is the same: **stop the bleeding first**.

---

## 2. The on-call

Today, the maintainer is the only person on call. The
on-call rotation will be defined when the project has at
least 2 active maintainers.

- **Critical alerts** wake the maintainer. The alert
  channel is configurable (email, SMS, Slack, PagerDuty,
  etc.).
- **Warning alerts** are batched and reviewed daily. The
  maintainer reviews the alerts at a consistent time
  (default: 09:00 local time).
- **Info alerts** are reviewed weekly. The maintainer
  reviews the alerts in a weekly review.

When the maintainer is unavailable (vacation, illness),
the alerts are silenced with an explicit "I'm out" message
in the `Announcements` category. The user is expected to
understand that a solo-maintained project has a single
point of failure; this is documented in the
[LAUNCH.md](../../docs/community/LAUNCH.md) §4.

---

## 3. The severity levels

| Severity | Definition | Example | Response time |
|---|---|---|---|
| **SEV-1** | the system is down; users cannot use it | the API is not responding; the database is corrupted | immediate |
| **SEV-2** | the system is degraded; some users are affected | the MCP server is returning errors for a specific tool; the email notifications are not being sent | within 1 hour |
| **SEV-3** | a minor issue; the system is working but something is off | a specific endpoint is slow; a log message is leaking PII | within 1 business day |
| **SEV-4** | a cosmetic or non-urgent issue; no user impact | a typo in a UI string; a missing log line | next release |

The severity is set by the responder, not the reporter.
When in doubt, **err on the side of higher severity**;
the severity can be downgraded after the mitigation.

---

## 4. The communication

Every SEV-1 and SEV-2 incident has a public status update
within 30 minutes of the alert. The update is posted in:

- The project's GitHub Discussions → Announcements
  (visible to anyone tracking the project).
- The status page (when Phase 5 ships the public status
  page; until then, the GitHub Discussions post is the
  status page).
- The maintainer's own channel (Twitter, Mastodon, etc.)
  if the incident is large enough.

The status update format:

```
[STATUS] <severity> <one-line summary>

Started: <timestamp UTC>
Detected: <timestamp UTC>
Mitigated: <timestamp UTC> | pending
Resolved: <timestamp UTC> | pending
Affected: <what is affected>
Cause: <known | investigating>
Next update: <timestamp UTC | when there is news>
```

Updates are posted at least every 30 minutes until
mitigated. The cadence slows after mitigation until
resolved.

A SEV-3 or SEV-4 incident does not get a public status
update; it is fixed in the next release and mentioned in
the release notes.

---

## 5. The incident types

### 5.1 The API is down

**Symptoms**: the API health check (`/health/live`) returns
non-200; the web client shows a "connection error"
message; users cannot sign in or interact with boards.

**Mitigation** (in order):

1. **Check the API container.** `docker compose ps`. If
   the container is not running, `docker compose up -d`.
2. **Check the API logs.** `docker compose logs --tail=200
   api`. Look for unhandled exceptions, OOM kills, or
   panics.
3. **Check the database.** Verify that the SQLite file exists,
   is writable by the container user, and the volume has free
   space. If database access fails, see §5.2.
4. **Check the host.** `docker system df`, `df -h`, `free
   -h`. The host may be out of disk space or memory.
5. **Restart the stack.** `docker compose restart`. This
   is the "have you tried turning it off and on again" of
   self-hosted incidents; it works more often than it
   should.
6. **Roll back to the previous release.** If the restart
   does not help, the release is the problem. `docker
   compose down`, then bring up the previous tag
   (see the [release process](../development/04-release-process.md)
   §6 for the rollback procedure).

**Resolution** (after mitigation):

- Identify the root cause from the logs, the metrics, or
  the OTel traces.
- File an issue in the tracker. The issue is the
  post-mortem.
- Fix the root cause in a PR. The PR is reviewed and
  merged per the normal flow.
- Cut a patch release (`vX.Y.Z+1`) with the fix.

### 5.2 The database is corrupted

**Symptoms**: the API returns 500 errors; the database
logs show corruption messages; the user's data is not
visible or is in an inconsistent state.

**Mitigation**:

1. **Stop the API.** `docker compose stop api`. This
   prevents further writes that could worsen the
   corruption.
2. **Take a snapshot of the current state.** This is the
   "before" of the forensic analysis, even if we end up
   restoring from a backup.
3. **Restore from the most recent backup.** Follow
   [`02-backup-restore.md`](02-backup-restore.md) §7.
4. **Verify the data.** Sign in, check the boards, the
   cards, the attachments.
5. **Start the API.** `docker compose start api`.

**Resolution**:

- Identify the root cause (filesystem corruption, a bad
  migration, a host crash). The forensic snapshot from
  step 2 is the input.
- File an issue. The issue is the post-mortem.
- Fix the root cause in a PR.

### 5.3 The MCP server is misbehaving

**Symptoms**: AI clients return errors when calling MCP
tools; the MCP logs show repeated failures; the
`HighErrorRate` alert fires for the MCP tool class.

**Mitigation**:

1. **Identify the failing tool.** The MCP dashboard (§3.2
   of [`03-monitoring.md`](03-monitoring.md)) shows the
   per-tool error rate. The failing tool is the one with
   the spike.
2. **Check the MCP logs.** `docker compose logs --tail=200
   api | grep mcp`. Look for the specific tool call that
   is failing.
3. **Disable the failing tool.** Set the tool's
   feature flag to `false` (see
   [`docs/design/06-feature-flags.md`](../design/06-feature-flags.md)).
   The tool is now disabled; the AI client gets a
   `mcp.tool.disabled_for_flag` error; the user is
   informed. The rest of the system keeps working.
4. **Check the upstream.** The failing tool may be a
   symptom of a deeper issue (database, OTel, a
   dependency). The alert on the database or the
   dependency may also be firing.
5. **Restart the API.** `docker compose restart api`. The
   tool's state is reset.

**Resolution**:

- Identify the root cause from the logs, the metrics, or
  the OTel traces.
- Fix the root cause in a PR.
- Re-enable the tool (the feature flag is set back to
  `true`).
- Cut a patch release.

### 5.4 A security vulnerability has been disclosed

**Symptoms**: a `security@fitty.ar` email arrives; a CVE
in a dependency is published; a security researcher
contacts the maintainer via a public channel.

**Mitigation**:

1. **Do NOT post publicly** until the fix is ready. The
   security policy in [`SECURITY.md`](../../SECURITY.md)
   is explicit on this.
2. **Acknowledge the report** within 3 business days
   (per the SECURITY.md SLA).
3. **Triage the report.** Is it real? What is the impact?
   What is the affected version?
4. **Develop the fix** on a private branch.
5. **Cut a patch release** (or a hotfix release, per the
   release process §6).
6. **Publish the security advisory** (on GitHub Security
   Advisories) and the patch release.
7. **Post the public announcement** in the Announcements
   category. The announcement references the advisory
   and the release; it does not include the exploit
   details until the patch is widely deployed.

**Resolution**:

- Document the incident (the post-mortem is the security
  advisory).
- Update the threat model
  ([`docs/security/01-threat-model.md`](../security/01-threat-model.md))
  if the model was incomplete.
- Update the secure-coding checklist
  ([`docs/security/02-secure-coding-checklist.md`](../security/02-secure-coding-checklist.md))
  if the checklist missed the issue.

---

## 6. The post-mortem

A SEV-1 or SEV-2 incident has a post-mortem. The
post-mortem is **blameless**: the goal is to understand
the system, not to blame the responder. The format:

```
# Post-mortem: <incident title>

## Summary
<one paragraph: what happened, when, what was affected>

## Timeline
<UTC timestamps of the key events: detected, mitigated, resolved>

## Impact
<what was affected, for how long, how many users>

## Root cause
<the underlying cause; not the symptom>

## What went well
<the things the system did right, the things the responder did right>

## What went wrong
<the things the system did wrong, the things the responder did wrong>

## Where we got lucky
<the things that could have made this much worse>

## Action items
<the follow-up issues; each is a separate GitHub issue>
```

The post-mortem is filed as a PR. The PR is reviewed by
the maintainer. The merged post-mortem is a public
artifact in the project's repository (in
`docs/operations/post-mortems/`).

---

## 7. The "user reports a problem" path

A user reports a problem that is not yet an incident
(e.g. "the board view is slow on my account"). The path
is:

1. **The user files an issue** with the `type:bug` label.
2. **The maintainer triages** the issue within 7 days.
   The maintainer confirms the bug, the affected version,
   and the impact.
3. **The maintainer files a fix** as a PR. The PR
   references the issue.
4. **The fix is reviewed and merged** per the normal
   flow.
5. **The fix is released** in the next patch release.
6. **The user is notified** that the fix is available.

The issue is closed when the fix is released. The
post-mortem is not required for user-reported issues
(only for SEV-1 / SEV-2 incidents).

---

## 8. The "the maintainer is the only responder" reality

Today, the maintainer is the only responder. The
playbook assumes one responder. When the project has
more maintainers, the playbook is updated to:

- Define an on-call rotation.
- Define a secondary on-call (the person who gets the
  alert if the primary does not acknowledge within 15
  minutes).
- Define an incident commander (the person who
  coordinates the response, separate from the responder).
- Define a communications lead (the person who posts the
  public status updates, separate from the responder).

Until then, the playbook is the maintainer's playbook.

---

## 9. The "what if I am not sure?" rule

When the responder is not sure what to do:

1. **Stop the bleeding first.** The mitigation is "stop
   the API, restore the backup, restart the API" — this
   works for most incidents.
2. **Ask for help.** A SEV-1 incident is a good time to
   ask a peer (the maintainer's network, a Discord
   server, a relevant subreddit). The maintainer is not
   expected to know everything.
3. **Document the uncertainty.** The post-mortem is the
   place to say "I was not sure what to do; I did X; I
   should have done Y". The next responder benefits from
   the learning.

The "what if I am not sure" rule is the reason this
document exists: so the responder has a starting point
even when the situation is novel.

---

## 10. The "do not do this" list

- **Do not post the exploit details publicly** before the
  fix is ready. This is the security policy in
  [`SECURITY.md`](../../SECURITY.md) and it is
  non-negotiable.
- **Do not skip the mitigation to go straight to the
  resolution.** A SEV-1 incident is fixed by mitigation
  first, resolution second.
- **Do not modify the database directly** without a
  backup. The "I'll just fix this one row" instinct is
  the source of more incidents than any other.
- **Do not roll forward to a new version** to fix an
  incident. Roll back to the previous version, then fix
  the bug, then cut a patch release.
- **Do not blame the responder** in the post-mortem.
  The post-mortem is blameless; the goal is to learn.

---

## 11. When to revisit

This document is revisited when:

1. A new incident type is added to the playbook.
2. A real incident reveals a gap in the playbook.
3. The on-call rotation is defined (the playbook is
   updated to reflect the multi-responder reality).
4. A new tool is added to the monitoring stack (the
   dashboards and the alerts are updated).

Until then, this document is the source of truth for
incident response in Cardscape.
