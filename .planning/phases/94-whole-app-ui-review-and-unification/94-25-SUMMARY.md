---
phase: 94-whole-app-ui-review-and-unification
plan: 25
subsystem: ui-foundation
tags: [documentation, guidelines, guard-tests]
requires:
  - phase: 94-24
    provides: "modern-card.css retired, alerts as dark panels, heading scale and --qb-fg-heading"
provides:
  - ".claude/ui-guidelines.md rewritten to describe the shipped system"
  - ".claude/ui-guidelines.md scanned by the planning-reference guard"
  - "PROJECT.md states the Platform mobile layout is live"
affects: [94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Guideline code examples are checked against the shipped CSS by a script (scratch), so an example cannot name a class that does not exist"
key-files:
  created: []
  modified:
    - .claude/ui-guidelines.md
    - .planning/PROJECT.md
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "The guidelines follow the shipped CSS where the design contract differed: the refusal line is the .refusal-line class, the commit action is wood (no gold button fill), alerts are dark panels with light status text, a Shop theme goes on html"
  - "check-classes.py also accepts Font Awesome classes (icons in examples), beyond the four stylesheets the plan listed"
  - "Accessibility checklist items are plain checkboxes without the contract's item numbers, since the guidelines carry no planning identifiers"
metrics:
  tasks: 2
  commits: 2
  completed: 2026-11-09
status: complete
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
---

# Phase 94 Plan 25: Rewrite the UI guidelines for the shipped system Summary

`.claude/ui-guidelines.md` now documents the layered token system, the component catalogue with one example each, the colour and accessibility rules, the phone layout contract, the no-inline-code rule, themes and tokens, and the self-hosted vendor rule, with every example class proven to exist in the shipped CSS.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `929367ec` | Card example on the shipped classes, "Tokens and layers" section, guidelines added to `FoundationFiles_CarryNoPlanningReferences` |
| 2 | `66c6ea1a` | Components, Colour, Accessibility checklist, Mobile, No inline code, Themes and tokens, Vendor files; PROJECT.md Platform mobile layout line |

## What the guidelines say (verified against the current CSS)

- Layers `vendor, tokens, base, components, pages, utilities`, declared once in `app.css`; legacy page sheets (`legacy-pages.css`, `quests.css`, `shop.css`, `calendar.css` and the rest) are unlayered and win on their own selectors; `modern-card.css` and `site.css` are not mentioned as live files.
- Components: card and page header, type scale (four sizes, two weights, headings mapped in `base.css`), buttons by role, form group, refusal line (`.refusal-line`), empty state, alerts (dark panel, light status text), toast keys (errors and warnings persist, success and info auto-hide with `role="status"`).
- Colour: status colour as text means ink on paper surfaces, light variant on glass, board and alerts; `--qb-fg-heading` for headings on unconverted cards; the allowed-use table; the reserved gold list.
- Mobile: layout by User-Agent, static phone bar with offcanvas, desktop bar collapses below 1200px, 44px targets, three phone sizes with a 20px card title, no `@media` in twins.
- Themes: board photo through `--qb-board-photo` on `:root`, never `body { --qb-board-image }`; a Shop theme on `<html data-theme="shop">`.
- The four original rules (card pattern, `<hr>` and button layout, mobile twins and "check before fixing a missing field", `Html.LocalTime` / `Html.WallClock` with the four style names) are intact.

## Verification

- `check-classes.py .claude/ui-guidelines.md`: 30 classes in 6 code blocks, 0 undefined.
- All nine required headings present as whole lines; PROJECT.md contains the replacement line and no longer calls the Platform mobile layout dead code.
- `UiFoundationLayerGuardTests`: 14 passed (planning-reference scan now includes the guidelines).
- Full `dotnet build` and `dotnet test`: 1751 passed, 11 skipped (SQL Server migrator tests), 0 failed.

## Deviations from Plan

None that change behaviour. Notes: `check-classes.py` additionally reads the Font Awesome stylesheet so icon classes in examples are checked rather than ignored; no view in the app loads a page stylesheet through `@section Styles` from a desktop view today (desktop page sheets are linked by the layout), so the guideline states the rule for new code, which the Mobile twins already follow.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- `.claude/ui-guidelines.md`, `.planning/PROJECT.md`, `UiFoundationLayerGuardTests.cs` present and committed; commits `929367ec` and `66c6ea1a` exist.
