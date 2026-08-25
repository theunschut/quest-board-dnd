# Stack Research

**Domain:** Small ad-hoc rolling-improvements milestone (v9.0) on a mature ASP.NET Core 10 MVC app — 2 items: (1) UI-only quest-signup character change, (2) stale GitHub Dependabot alert cleanup
**Researched:** 2026-08-25
**Confidence:** HIGH

## Executive Summary

**Item 1 needs zero new packages, JS libraries, or tooling.** The existing Bootstrap 5.3.0 modal (`#addCharacterModal`) + plain `<form asp-action="UpdateSignupCharacter">` POST pattern already in `Details.cshtml` is sufficient and should simply be reused/extended. Bootstrap's full bundle (including Popper) is already loaded on both `_Layout.cshtml` and `_Layout.Mobile.cshtml`, so the identical modal markup works unmodified on mobile — no separate mobile-specific JS is needed.

**Item 2 needs no NuGet package or code change at all** — `System.Security.Cryptography.Xml` is confirmed absent from every tracked `.csproj` in the solution (verified independently in this research: `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` was deleted 2026-06-29 in commit `a477ab9`). This is a **GitHub-platform-side cleanup task**, not a code/dependency task. Live-checked against the actual repo via `gh api`: the 5 open alerts (#17–#21) were all *created* 2026-08-10 — over 6 weeks **after** the manifest was deleted — confirming GitHub's dependency graph is serving alerts from a stale cached snapshot of the deleted manifest rather than re-scanning it. This matches a long-standing, unresolved class of upstream bug (`dependabot/dependabot-core` issues #4129, #2041, #4951: "Should not generate alerts on deleted manifest files") that GitHub has closed as "not planned" — there is no reliable automatic-close mechanism to wait for. The fix is a direct REST API call (documented below) to dismiss each alert with `dismissed_reason: "not_used"`.

## Recommended Stack

### Core Technologies (unchanged — existing stack, confirmed sufficient)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Bootstrap (bundle, incl. Popper) | 5.3.0 (CDN, `bootstrap.bundle.min.js`) | Modal component for character select/change | Already loaded app-wide (`_Layout.cshtml` line 12/189-equivalent and `_Layout.Mobile.cshtml` lines 12, 189); reusing it means zero new script tags on either desktop or mobile |
| ASP.NET Core MVC form POST + `[ValidateAntiForgeryToken]` | 10 (existing) | Submits the character change | `QuestController.UpdateSignupCharacter(int questId, int? characterId)` already exists, already validates ownership + `CharacterStatus.Active`, already accepts `null` to clear — the view is the only gap |

**No new package, CDN script, or npm/JS dependency is warranted for Item 1.** The task is markup-only: (a) render the same "change character" trigger in the `Character != null` branch of `Details.cshtml` (currently only rendered when `Character == null`, lines ~130-144 and ~246-260), reusing `#addCharacterModal`/`UpdateSignupCharacter` verbatim; (b) add the equivalent trigger + modal to `Details.Mobile.cshtml`, which today has no add/change UI at all (only a read-only `@(participant.Character?.Name ?? "No character")` string, lines ~215/243); (c) add a "Clear (no character)" option to the existing `<select>`/its submit path so posting `characterId=null` is reachable from the UI, not just the controller contract. A plain full-page-reload form POST (the existing pattern — `RedirectToAction("Details", ...)`) is consistent with how every other mutation on this page already works (`revokeSignup` is the one exception, and it already uses vanilla `fetch()`, not a library) — no progressive-enhancement/AJAX layer is needed since the page already reloads on every other signup mutation.

### Supporting Libraries

None needed for Item 1. None needed for Item 2 (no library involved — the vulnerable package `System.Security.Cryptography.Xml` is a phantom entry from a deleted `.csproj`, not a real dependency of any current project).

### Development / Platform Tooling (Item 2)

| Tool | Purpose | Notes |
|------|---------|-------|
| GitHub REST API — `PATCH /repos/{owner}/{repo}/dependabot/alerts/{alert_number}` | Dismiss each of the 5 stale alerts directly | See exact fields below. This is the only reliable path — auto-close-on-manifest-deletion is not a behavior GitHub's dependency graph guarantees or currently performs correctly (see Pitfalls). |
| `gh api` (GitHub CLI, already installed — v2.89.0 confirmed in this environment) | Thin wrapper to call the REST endpoint without writing a script | `gh` has **no** native `gh dependabot` or `gh alert` subcommand (verified: `gh alert` → "unknown command"); use the generic `gh api` passthrough |
| `dependabot.yml` | Governs *future* update PRs/scan config | **Does not help here.** It has no field to acknowledge/close/suppress an existing alert for a manifest that no longer exists — it only configures `package-ecosystem`/`directory` targets going forward |

## GitHub REST API surface for Item 2

**Endpoint (per official docs, `docs.github.com/en/rest/dependabot/alerts`, API version `2022-11-28`):**

```
PATCH /repos/{owner}/{repo}/dependabot/alerts/{alert_number}
```

**Request body fields:**

| Field | Type | Values | Notes |
|-------|------|--------|-------|
| `state` | string | `dismissed`, `open` | Set to `dismissed` to close |
| `dismissed_reason` | string | `fix_started`, `inaccurate`, `no_bandwidth`, `not_used`, `tolerable_risk` | **Required** when `state=dismissed`. For this scenario (manifest deleted, package literally not present anywhere in the codebase) `not_used` is the semantically correct reason — the dependency isn't used by the project |
| `dismissed_comment` | string | free text, ≤280 chars | Optional; recommended here to record *why*, e.g. "Manifest deleted in a477ab9 (EuphoriaInn→QuestBoard rename); package confirmed absent from all tracked .csproj files and `dotnet list package --include-transitive`" |
| `assignees` | array of strings | GitHub usernames | Not needed for this task |

**Auth:** requires `security_events` scope on a classic PAT, or a fine-grained token with **"Dependabot alerts" repository permission: Read and write**; `gh api` inherits the CLI's existing auth so no separate token setup is needed in this environment (already authenticated, confirmed via successful `gh api repos/.../dependabot/alerts` read above).

**Concrete `gh` invocation per alert:**

```bash
gh api --method PATCH \
  repos/theunschut/quest-board/dependabot/alerts/{alert_number} \
  -f state=dismissed \
  -f dismissed_reason=not_used \
  -f dismissed_comment="Manifest deleted in a477ab9 (EuphoriaInn->QuestBoard rename); package confirmed absent from all .csproj files."
```

Repeat for alert numbers **17, 18, 19, 20, 21** (the 5 confirmed-open HIGH alerts, live-verified in this research — all created 2026-08-10, all target the deleted `EuphoriaInn.Domain/EuphoriaInn.Domain.csproj` manifest, all `System.Security.Cryptography.Xml` CVEs patched at 8.0.4). There is no batch/bulk PATCH endpoint — the REST API is one alert per call (the GitHub *web UI* supports multi-select dismissal, but that is a UI convenience layered over the same per-alert calls, not a distinct API).

**List endpoint (for verification before/after):**

```
GET /repos/{owner}/{repo}/dependabot/alerts
```
Supports a `state` filter accepting a comma-separated list of `auto_dismissed`, `dismissed`, `fixed`, `open` — useful to confirm all 5 move out of `open` after dismissal, and a `scope` filter (`development`/`runtime`) not relevant here.

## Why NOT to wait for auto-close, and why NOT a `dependabot.yml` change

| Approach | Why it doesn't apply here |
|----------|---------------------------|
| Wait for GitHub to auto-close on manifest deletion | **Not a guaranteed behavior.** GitHub's dependency graph is documented to scan only the **default branch**, and it snapshots per-manifest — but deleting a manifest is not documented to reliably purge or invalidate that snapshot. Live evidence from this repo: alerts #17–#21 were newly *created* 2026-08-10, six weeks after the manifest's 2026-06-29 deletion, meaning GitHub is still minting fresh alerts against a manifest path that no longer exists on `main`. This matches a known, long-standing upstream limitation tracked in `dependabot/dependabot-core` (issues #4129 "Should not generate alerts on deleted manifest files", #2041, #4951) — GitHub has closed at least one of these as "not planned" with no fix committed. Do not build a phase around "wait and it'll clear itself." |
| Push a `dependabot.yml` change to fix it | `dependabot.yml` only configures **future** dependency-update scanning (which ecosystems/directories to watch, schedule, grouping). It has no directive that targets or dismisses an *existing* alert tied to a path Dependabot no longer scans. Irrelevant to closing #17–#21. |
| Trigger a manual re-scan / re-submit the dependency graph | There is no documented, supported user-facing "re-scan now" action for Dependabot alerts (unlike code scanning, which can be re-triggered via a workflow re-run). Even if the dependency graph were refreshed, refreshing a *deleted* manifest produces nothing to reconcile against — there is no live manifest data for GitHub to compare and auto-resolve the alert to `fixed`. Manual dismissal via the API is the documented, correct mechanism for exactly this case. |
| Bump `System.Security.Cryptography.Xml` to 8.0.4 in the solution | **Not applicable / would be a no-op.** The package is not referenced by any current `.csproj` (verified: zero tracked references, zero transitive references via `dotnet list package --include-transitive` across `QuestBoard.slnx`). There is nothing to patch — the alert is a phantom pointing at deleted code. |

## Non-default-branch scanning

Per GitHub's official docs (`about-dependabot-alerts`): *"Dependabot scans your repository's default branch..."* — Dependabot alerts (and the dependency graph that powers them) are generated **only from the default branch** (`main` in this repo, confirmed via `gh repo view`). Non-default branches are not scanned for alert purposes, so a feature/milestone branch cannot itself trigger, and cannot itself resolve, these alerts — the dismissal must happen via the API/UI regardless of which branch the surrounding phase work lands on.

## Stack Patterns by Variant

**If a future item needs to close a large batch of Dependabot alerts (not just 5):**
- Still use `gh api` per-alert PATCH in a small loop/script (`for id in 17 18 19 20 21; do gh api --method PATCH ...; done`) rather than reaching for a third-party tool — no NuGet/npm package exists or is needed for this; it's a 5-line shell loop.

**If this pattern recurs (stale alerts from other deleted historical projects, e.g. `EuphoriaInn.Service`/`EuphoriaInn.UnitTests`/`EuphoriaInn.IntegrationTests`):**
- Same fix, same `dismissed_reason=not_used` — this repo's alert history (checked live) already shows most of those manifest paths resolved to `fixed`/`dismissed` naturally over time except this one HIGH batch, which is why it needs the manual push now rather than more waiting.

## Version Compatibility

Not applicable — no packages are being added, upgraded, or pinned by either item. `System.Security.Cryptography.Xml` 8.0.4 (the patched version referenced by the CVEs) never needs to be *installed* here since it is not a real dependency of the current codebase.

## Sources

- `docs.github.com/en/rest/dependabot/alerts?apiVersion=2022-11-28` — Update/List Dependabot alert endpoint fields, `state`/`dismissed_reason` enum values (HIGH confidence — official GitHub REST API reference)
- `docs.github.com/en/code-security/dependabot/dependabot-alerts/about-dependabot-alerts` — "Dependabot scans your repository's default branch..." (HIGH confidence — official docs, direct quote)
- `docs.github.com/en/code-security/dependabot/dependabot-alerts/viewing-and-updating-dependabot-alerts` — manual dismissal UI flow, GraphQL `dismissComment` field (HIGH confidence — official docs)
- `github.com/dependabot/dependabot-core` issues #4129, #2041, #4951 — known unresolved upstream behavior re: alerts persisting/regenerating against deleted manifest files (MEDIUM confidence — community/maintainer issue tracker, not formal docs, but directly on-point and corroborated by this repo's own live data)
- Live verification against `theunschut/quest-board` via `gh api repos/theunschut/quest-board/dependabot/alerts` (this research session, 2026-08-25) — confirmed exact alert numbers (17–21), manifest path, creation dates (2026-08-10, post-dating the 2026-06-29 manifest deletion), and current `state`/`dismissed_reason` for all 16 historical alerts in the repo (HIGH confidence — primary-source ground truth from the actual repository)
- `QuestBoard.Service/Views/Quest/Details.cshtml` (lines ~100-270, 819-863) and `Details.Mobile.cshtml`, `_Layout.Mobile.cshtml` (lines 12, 189) — read directly in this research session to confirm existing Bootstrap 5.3.0 modal pattern and its absence on mobile (HIGH confidence — direct source read)

---
*Stack research for: v9.0 Rolling Improvements (quest signup character change + Dependabot alert cleanup)*
*Researched: 2026-08-25*
