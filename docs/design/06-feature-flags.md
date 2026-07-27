# Feature flags

> The project's mechanism for shipping code that is not yet
> ready to be enabled, and for phasing features safely.
> Feature flags are an **operational** tool, not a
> development tool: they let the maintainer decouple
> **deploy** from **release**.
>
> This is a **design** document. The implementation lands
> in Phase 1 (the basic flag mechanism) and is extended in
> Phase 3 (the automation engine and the extensions).

---

## 1. The principle

A feature is **deployed** when its code is on `master` and
the build is green. A feature is **released** when the
flag is enabled for users. The two are different events.

The benefit:

- **Ship the code, then decide when to enable it.** The
  maintainer merges a feature, the CI is green, the code is
  on `master`. The release happens on a separate schedule.
- **Test in production safely.** A flag is enabled for a
  subset of users (the maintainer's workspace, or a beta
  cohort) before it is enabled for everyone.
- **Roll back without a deploy.** A feature that breaks in
  production is disabled by toggling a flag, not by
  reverting a commit.
- **A/B test.** Two variants of a feature are compared on
  the same production traffic, with a metric (engagement,
  retention) deciding the winner.

---

## 2. The flag types

| Type | Values | Use |
|---|---|---|
| **Boolean** | on / off | the simple case: enable a feature or not |
| **Multivariate** | `variant_a` / `variant_b` / `variant_c` | compare two or more variants of a feature |
| **Kill switch** | on / off | disable a feature that is causing an incident; the code path is still in the binary but is unreachable |
| **Gradual rollout** | 0% to 100% | enable a feature for a percentage of users, ramp over time |
| **Workspace allowlist** | list of workspace ids | enable a feature for specific workspaces (the maintainer's workspace, a beta cohort, a paying customer) |
| **User allowlist** | list of user ids | enable a feature for specific users (the maintainer themselves, an internal team) |

The type is declared in the flag definition. The flag is
**typed**; a `boolean` is not interchangeable with a
`multivariate`.

---

## 3. The flag definitions

Flags are declared in code, in
`src/Cardscape.Infrastructure/FeatureFlags/Flags.cs`. The
file is the source of truth. The flag definitions are
**typed**:

```csharp
public static class Flags
{
    // Boolean
    public static readonly BooleanFlag NewBoardView =
        new("new-board-view", defaultValue: false);

    // Multivariate
    public static readonly MultivariateFlag<string> OnboardingFlow =
        new("onboarding-flow",
            new[] { "wizard-v1", "wizard-v2", "single-page" },
            defaultValue: "wizard-v1");

    // Kill switch
    public static readonly BooleanFlag McpServer =
        new("mcp-server", defaultValue: true);

    // Gradual rollout
    public static readonly PercentageFlag NewSearch =
        new("new-search", defaultPercentage: 0);
}
```

A flag is **referenced by name** in the code. The runtime
evaluates the flag against the current user / workspace /
request:

```csharp
if (await _flags.IsEnabledAsync(Flags.NewBoardView, user, ct))
{
    return NewBoardView(...);
}
else
{
    return OldBoardView(...);
}
```

The reference is **typed**; a typo is a compile error.

---

## 4. Where the flag values come from

The flag values come from a **flag store**. The default
implementation reads from `appsettings.json` (Development)
or from an environment variable (Production). The flag
store is **pluggable**; a future implementation can read
from LaunchDarkly, Unleash, or a self-hosted flag service.

### Phase 1: the in-process flag store

```json
// appsettings.json
{
  "FeatureFlags": {
    "new-board-view": { "type": "boolean", "value": false },
    "onboarding-flow": {
      "type": "multivariate",
      "variants": ["wizard-v1", "wizard-v2", "single-page"],
      "value": "wizard-v1"
    },
    "mcp-server": { "type": "boolean", "value": true },
    "new-search": { "type": "percentage", "value": 0 }
  }
}
```

The flag store is read at startup and on every
`IFeatureFlagService` call (the values are cached in
memory, with a configurable refresh interval).

### Phase 5 (or earlier, if needed): a self-hosted flag service

A future PR can introduce a `Flags` bounded context with
its own database table, its own admin UI (in the MCP
server, naturally), and a webhook-based change-notification
mechanism. Until then, the in-process store is enough.

---

## 5. The flag lifecycle

Every flag has a lifecycle:

1. **Introduced.** The flag is declared in `Flags.cs` with
   a default value. The code that reads the flag is
   shipped. The feature is **off by default**.
2. **Enabled.** The flag is toggled on, for the maintainer's
   workspace first, then for a beta cohort, then for
   everyone. The toggle is a config change, not a code
   change.
3. **Rolled out.** The feature is enabled for 100% of
   users. The flag is still in the code.
4. **Retired.** The old code path (the "if not flag" branch)
   is removed. The flag is removed from `Flags.cs`. The
   feature is now the only path.

The lifecycle is enforced by the **`no flag left behind`**
rule:

> A flag that has been at 100% (or fully on) for more than
> 30 days is a CI failure. The PR that resolves the
> failure removes the flag.

This rule prevents flag debt from accumulating.

---

## 6. The flag categories

Flags are tagged with a category. The category determines
who can toggle the flag.

| Category | Toggled by | Use |
|---|---|---|
| `release` | the maintainer, via the release process | phased features that ship with a version |
| `kill-switch` | the maintainer, via the admin UI or the config | features that need to be disabled in an incident |
| `experiment` | the maintainer, via the admin UI | A/B tests and gradual rollouts |
| `ops` | the maintainer, via the config | operational toggles (cache TTLs, log levels, etc.) |

The category is declared in the flag definition. The admin
UI (Phase 5) shows the toggle for the relevant categories
based on the user's role.

---

## 7. The flag and the build

Flags are **runtime** decisions, not **compile-time**. The
code is shipped; the flag is checked at runtime. This
avoids the "we have to rebuild to enable the feature"
problem.

The exception is **dead-code elimination**. A flag that is
known to be at 100% for the lifetime of a release (e.g. a
flag that will be removed before the next release) can be
inlined by the compiler. The C# compiler does not do this
automatically; the maintainer removes the flag and the
non-flag branch in a follow-up PR.

---

## 8. The flag and the MCP server

The MCP server is itself behind a kill-switch flag
(`Flags.McpServer`). The default is `true` (the MCP server
is on), but the maintainer can disable it in an incident
without redeploying.

The MCP server's tools are also behind flags. The flag is
declared per tool:

```csharp
public static readonly BooleanFlag McpToolCardsCreate =
    new("mcp-tool-cards-create", defaultValue: true);
```

A tool that is disabled returns the
`mcp.tool.disabled_for_flag` error to the AI client. The
client can switch on the error and inform the user ("the
`cards_create` tool is currently disabled").

---

## 9. The flag and the audit log

Every flag toggle is logged in the audit log. The log
entry carries:

- The flag name.
- The old value and the new value.
- The actor (the user, the system, or the CI process that
  toggled the flag).
- The timestamp.
- The reason (free-form, optional).

A flag toggle is an administrative action; it is logged as
such.

---

## 10. The flag and the tests

A flag has a test for each of its states:

- **Default value**: the test sets up the flag store with
  no overrides, the flag has the default value, the
  behavior is the default.
- **Enabled**: the test overrides the flag to `true`, the
  behavior is the "new" path.
- **Disabled**: the test overrides the flag to `false`,
  the behavior is the "old" path.
- **Multivariate (each variant)**: the test runs for each
  variant.

A flag without a test for each state is a CI failure.

---

## 11. Anti-patterns (do not do this)

- **A flag that is never retired** — the `no flag left
  behind` rule exists for a reason.
- **A flag for a value, not a feature** — flags are for
  **features**, not for **config values**. A config value
  goes in `appsettings.json`, not behind a flag.
- **A flag that is toggled in code, not in config** — the
  flag value comes from the flag store, not from a
  hard-coded `if (DateTime.Now > ...)` block.
- **A flag with no test** — see §10.
- **A flag with a side effect in the toggle** — toggling a
  flag is a config change, not a state change. The code
  reacts to the new value; it does not run a migration.
- **A flag that controls a security check** — a kill-switch
  on a security check is a vulnerability, not a flag. If
  the check needs to be disabled, it is a code change.

---

## 12. When to revisit

This document is revisited when:

1. A new flag type is added (e.g. a `time-window` flag
   that is on during a specific time range).
2. The flag store changes (e.g. a move to a self-hosted
   flag service like Unleash).
3. The admin UI is added (Phase 5) and the categories or
   roles change.
4. A new rule is added to the `no flag left behind`
   policy (e.g. a flag at 100% for 60 days, not 30).

Until then, this document is the source of truth for
feature flags in Cardscape.
