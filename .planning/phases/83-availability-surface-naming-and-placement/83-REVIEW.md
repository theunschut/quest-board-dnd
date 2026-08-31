---
phase: 83-availability-surface-naming-and-placement
reviewed: 2026-08-30T00:00:00Z
depth: standard
files_reviewed: 14
files_reviewed_list:
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Events/Index.cshtml
  - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Agenda/Index.cshtml
  - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/wwwroot/css/modern-card.css
  - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  - QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
status: issues_found
---

# Phase 83: Code Review Report

**Reviewed:** 2026-08-30
**Depth:** standard
**Files Reviewed:** 14
**Status:** issues_found

## Summary

This phase's authorization-gating logic is correct throughout: all eight new/changed `DungeonMasterOnly`
conditionals are right-side-up, none swallows the adjacent My Agenda button on Calendar, the desktop
Calendar dropdown collapse leaves no orphaned `dropdown-menu`/`data-bs-toggle` markup, and the nested
`activeBoardType` gate on the moved DM-menu entry reuses the layout's existing local rather than
re-resolving it. `EventsController.Index` still carries only the bare class-level `[Authorize]` with no
policy on the action itself, matching D-09 exactly, and the comment above it now documents the
open-page/gated-links split without referencing any phase or decision ID. The subtitle's board-name
interpolation is plain Razor `@` output (HTML-encoded, no `Html.Raw`) and its empty-name fallback uses
`string.IsNullOrEmpty`, which correctly covers both `null` and `""`. The four test files carry solid
role-flip proof (presence and absence, each absence assertion paired with a positive marker) and the
new `StaleAvailabilityOverviewLabelGuardTests` correctly targets `"Availability Overview"` as the
`NotContain` target rather than mistakenly targeting `"Board Availability"` on a page that legitimately
renders it. No GSD planning/requirement/decision references leaked into any of the 14 files.

One real, non-obvious visual defect was found in the new shared `.header-subtitle` CSS rule: on both
desktop pages that use it (`Events/Index.cshtml`, `Agenda/Index.cshtml`), a pre-existing, more specific,
`!important`-bearing rule silently overrides the color, text-shadow and font-weight the design contract
calls for, so the "muted" subtitle actually renders in the same loud gold/shadow treatment as the page
heading, just 25% more transparent. This does not affect functionality, security, or authorization
correctness -- it is a Warning, not a Blocker -- but it does fully invert the phase's own stated design
intent (UI-SPEC Dimension 3 Color, "must PASS"), and the phase's own summary flags this exact area
("Visual rendering of the subtitle text ... was not screenshot-verified") as unverified, so it slipped
through undetected.

## Warnings

### WR-01: `.header-subtitle` is silently overridden by `.modern-card p` on both desktop pages, defeating the muted-subtitle design contract

**File:** `QuestBoard.Service/wwwroot/css/modern-card.css:44-50` (new rule) vs. `QuestBoard.Service/wwwroot/css/modern-card.css:79-88` (pre-existing rule)
**Also affects:** `QuestBoard.Service/Views/Events/Index.cshtml:20`, `QuestBoard.Service/Views/Agenda/Index.cshtml:26`

**Issue:** The new rule is:

```css
.header-subtitle {
    display: block;
    margin-top: 0.25rem;
    font-size: 0.8125rem;
    font-weight: 400;
    opacity: 0.75;
}
```

It deliberately declares no `color` or `text-shadow`, so the UI-SPEC's intent ("inherits
`.modern-card-header`'s own `color: #1a1a1a` and light text-shadow, dimmed via `opacity: 0.75`") depends
entirely on ordinary CSS inheritance from the `.modern-card-header` ancestor.

But on both desktop views, the `<p class="header-subtitle mb-0">` sits inside `<div class="card
modern-card">`, and this pre-existing rule also matches it:

```css
.modern-card p,
.modern-card li,
.modern-card span,
.modern-card small {
    color: #F4E4BC !important;
    text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.9),
                 -1px -1px 2px rgba(0, 0, 0, 0.9),
                 1px 1px 6px rgba(0, 0, 0, 0.8) !important;
    font-weight: 500;
}
```

`.modern-card p` has higher specificity than `.header-subtitle` (0,1,1 vs 0,1,0) and carries `!important`
on `color` and `text-shadow`, both of which `.header-subtitle` never declares -- so this rule wins
outright on those two properties, with no contest. `font-weight` is a direct conflict (500 vs 400) with
no `!important` on either side, so specificity alone (`.modern-card p` being more specific) still decides
in its favor.

Net effect on **desktop** `Events/Index.cshtml` and `Agenda/Index.cshtml`: the subtitle renders in the
warm-gold heading color (`#F4E4BC`) with the full triple dark text-shadow and `font-weight: 500` -- i.e.
visually close to a smaller heading, not the muted `#1a1a1a`/light-shadow/`400`-weight secondary line the
Typography and Color sections of `83-UI-SPEC.md` specify. Only the `opacity: 0.75` from `.header-subtitle`
actually takes effect, since `.modern-card p` does not set `opacity`.

The **mobile** views (`Events/Index.Mobile.cshtml:21`, `Agenda/Index.Mobile.cshtml:25`) are unaffected:
their headers render outside any `.modern-card` ancestor, so `.modern-card p` never matches there and the
subtitle inherits the intended dark, light-shadowed treatment correctly. This asymmetry (correct on
mobile, wrong on desktop) is what makes the bug easy to miss in a spot-check that only exercises one
layout.

This is exactly the risk `83-01-SUMMARY.md` itself flagged and left unverified: "Visual rendering of the
subtitle text and its opacity/contrast against the glass-surface header was not screenshot-verified in
this session -- grep/build confirm the markup and CSS rule exist correctly, but actual on-page legibility
is a visual judgment call." The judgment call was never made, and the result fails the UI-SPEC's own
Dimension 3 (Color) checker sign-off criterion ("verify the subtitle does not use `.text-muted`, uses the
new `.header-subtitle` rule..." -- the rule is used, but a stronger pre-existing rule silently wins).

**Fix:** Give `.header-subtitle` its own explicit, higher-precedence declarations for the properties that
are currently being stolen, e.g.:

```css
.header-subtitle {
    display: block;
    margin-top: 0.25rem;
    font-size: 0.8125rem;
    font-weight: 400 !important;
    opacity: 0.75;
    color: #1a1a1a !important;
    text-shadow: 1px 1px 2px rgba(255, 255, 255, 0.8) !important;
}
```

(matching the light, single-layer shadow `.modern-card-header` itself uses, rather than the heavy
triple-shadow `.modern-card p/li/span/small` rule uses). This keeps the rule generic/unscoped as intended
for the mobile case, while no longer depending on inheritance winning a cascade fight it cannot win
against a pre-existing `!important` rule.

## Info

### IN-01: `GetWithUserAgentAsync` is re-duplicated in the new guard test class rather than shared

**File:** `QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs:42-50`

**Issue:** `StaleAvailabilityOverviewLabelGuardTests` (new in this phase) defines its own private
`GetWithUserAgentAsync(client, url, userAgent)` helper, which is now the fourth near-identical copy of
this method across the test project alongside `LayoutNavigationTests`, `CalendarButtonStyleTests`, and
`EventsOverviewControllerIntegrationTests`'s `GetMobileAsync`. `83-04-SUMMARY.md` confirms this was a
deliberate copy ("the `GetWithUserAgentAsync` helper copied from `CalendarButtonStyleTests`'s shape"),
matching this test project's established convention of per-class private helpers rather than a shared
base/utility. Not a defect and not out of step with the codebase's existing pattern, but each new test
class that needs mobile-UA testing continues to grow this duplication rather than shrinking it.

**Fix:** Optional -- if a fifth such class appears, consider hoisting `GetWithUserAgentAsync` into a
shared test helper (e.g. alongside `AuthenticationHelper`) rather than continuing to copy it per class.
No action required for this phase specifically.

---

_Reviewed: 2026-08-30_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
