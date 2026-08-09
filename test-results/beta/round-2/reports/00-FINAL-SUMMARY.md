# A8 — Settings + Global UI Beta Report — Final Summary

> **Test session:** 2026-08-09 (Cardscape v1.0.0, .NET 10)
> **Tester:** A8 general worker (in-app browser via Playwright MCP + API)
> **URL base:** http://localhost:8080 (Docker container `cardscape.api`)
> **Per-test report:** `D:\GitHub\Cardscape\test-results\beta\round-2\reports\A8-settings.md`
> **Screenshots:** `D:\GitHub\Cardscape\test-results\beta\round-2\screenshots\A8-*.png`

---

## TL;DR

Round 2 confirms that **all 8 round-1 Critical/High fixes (BUG-A8-000/001/002/003/005/006/007/008) hold** in the running v1.0.0 image: 2FA re-enroll returns 400 `auth.totp.already_enrolled` (BUG-A8-005 verified end-to-end), OAuth app invalid-redirect-URI returns 400 `oauth.redirect_uri_invalid` (round 1 BUG-A1-002 verified), 2FA enrollment still returns the otpauth URI + 10 recovery codes, the appearance page renders 12 themes with 5-badge swatches and a live preview, SCIM tokens issue/revoke and reject after revoke, and the OAuth apps page exposes register/revoke/list end-to-end.

The only new finding is **BUG-A8-019 (Medium)**: the task spec asked for `/settings/profile`, `/settings/security`, `/settings/data`, and `/settings/notifications` pages but **none of them exist** in the Blazor surface (or in the API — no `POST /api/users/me/password`, no `GET /api/users/me/sessions`, no `GET /api/users/me/notification-preferences`).

A few **Documented** items: no explicit Slack-disconnect endpoint (BUG-A8-020 Low), GitHub pulls/issues need outbound internet (BUG-A8-021 Low), and the **BUG-A8-018 (retracted)** — the initial 500 vs 400 in the container log was a stale container, the current binary returns 400 deterministically.

| Severity | Count | Status |
| --- | --- | --- |
| Critical | 0 | — |
| High | 0 | — |
| Medium | 1 | **Documented (BUG-A8-019)** — settings/profile, /security, /data, /notifications pages don't exist |
| Low | 3 | **Documented (BUG-A8-020/021/022)** — Slack disconnect, GitHub egress, regression-check summary |

---

## BUG-A8-NNN entries

| ID | Severity | Title | Status |
| -- | -------- | ----- | ------ |
| BUG-A8-000 (round 1) | Critical | `Copyright ? 2026` literal in MainLayout footer | Fixed (round 1) — verified |
| BUG-A8-001 (round 1) | Critical | `LanguageSwitcher.OnChange` discards the new selection | Fixed (round 1) — verified |
| BUG-A8-002 (round 1) | High | `NotFound.razor` is missing the "back to home" link | Fixed (round 1) — verified |
| BUG-A8-003 (round 1) | Critical | Language combobox shows "English" when localStorage is "es" | Fixed (round 1) — verified |
| BUG-A8-004 (round 1) | High | Settings appearance reverts to English on refresh | Fixed (round 1) — verified |
| BUG-A8-005 (round 1) | High | 2FA enroll returns 500 on duplicate-key | **Fixed (round 1) — verified end-to-end in round 2** |
| BUG-A8-006 (round 1) | Critical | `SharedResource.es.resx` is double-encoded | Fixed (round 1) — verified |
| BUG-A8-007 (round 1) | Critical | `@Body` does not re-render on language change | Fixed (round 1) — verified |
| BUG-A8-008 (round 1) | High | `EmptyLayout.razor` does not initialise the culture | Fixed (round 1) — verified |
| BUG-A8-011 (round 1) | Low | `SettingsGoogleCalendar` route mismatch with env file | Fixed (round 1) — verified (page now has 2 `@page` directives) |
| BUG-A8-012 (round 1) | Medium | Register form has no "Confirm password" field | Documented (round 1) — still present |
| BUG-A8-014 (round 1) | Medium | No "Forgot password" link on /login; no password-reset flow | Fixed (round 1) — verified (forgot-password + reset-password endpoints + /forgot-password page + "Forgot password?" link on /login all present) |
| **BUG-A8-019 (round 2)** | **Medium** | **No `/settings/profile`, `/settings/security`, `/settings/data`, `/settings/notifications` pages; no `POST /api/users/me/password`, no `GET /api/users/me/sessions`, no `GET /api/users/me/notification-preferences`** | **Documented (round 2)** |
| BUG-A8-020 (round 2) | Low | No `DELETE /api/workspaces/{id}/integrations/slack/` (no explicit disconnect) | Documented (round 2) |
| BUG-A8-021 (round 2) | Low | GitHub pulls/issues need outbound internet (no `api.github.com` egress in the test container) | Documented (round 2) |
| BUG-A8-022 (round 2) | Info | 12th round 1 bug regression check: re-enroll, recovery code, page-loads, no console errors | All round 1 fixes hold |

Retracted:
- BUG-A8-018 (round 2) — initial flaky 500 on `POST /api/oauth-apps` invalid-redirect-URI was from a stale container, not the current v1.0.0 build. Re-tested 5x in a row → all 400.

---

## Commit list

No new commits in this pass — the running `cardscape/api:0.1.0-mvp` image is
the same v1.0.0 release that round 1 finalised. All 8 round-1 fixes hold
and were re-verified via the API + in-app browser.

The only follow-up is **BUG-A8-019**: implement the four missing
`/settings/*` pages (or remove them from the spec).

---

## 1-paragraph summary

The v1.0.0 settings surface is **stable** — all round-1 Critical/High fixes
(BUG-A8-000 through BUG-A8-008) hold under round-2 re-verification, the
2FA re-enroll path now returns a deterministic 400 `auth.totp.already_enrolled`
(confirmed end-to-end via API), SCIM tokens issue + revoke and reject after
revoke, OAuth apps register/revoke cleanly, the 12-theme appearance catalog
renders and persists per-user, the cardscape-classic and
cardscape-classic-dark custom themes apply correctly, and the language
switcher re-renders the page text in both EN ↔ ES. The single actionable
new finding is **BUG-A8-019 (Medium)**: the task spec enumerates
`/settings/profile`, `/settings/security`, `/settings/data`, and
`/settings/notifications` but **none of those pages (or the underlying
APIs) exist** — the Blazor app ships only `/settings/appearance`,
`/settings/two-factor`, `/settings/oauth-apps`,
`/settings/external-logins`, plus `/settings/integrations/google-calendar`
+ `/settings/integrations/google-drive`, and the workspace-scoped
SCIM/SAML/Slack/GitHub/Email/Integrations pages. Change-password is only
available via the self-serve `forgot-password` → `reset-password` flow
(no in-app change-password surface), sessions are JWT-only (no session
list / revoke-session API), and notification preferences are not exposed
at all. Recommended action: either implement the four missing pages (and
the three missing APIs) or remove the items from the spec.
