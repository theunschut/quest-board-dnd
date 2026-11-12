---
phase: 96-account-and-pickers-ui-conversion
plan: 10
subsystem: ui
tags: [home, landing, mobile-merge, guard-tests, closure, tracer]
requires:
  - phase: 96-09
    provides: account.css with the shell-gutter and refusal rules, the guard test class with its converted, retired and sheet lists
provides:
  - Home/Index as the one signed-out landing view for both layouts (Index.Mobile.cshtml deleted)
  - landing-card and landing-action rules in account.css
  - TheWholeAreaIsConverted, the area-wide closure fact
  - recorded counts against the ROADMAP's
affects: [96-11, 96-12]
tech-stack:
  added: []
  patterns: [one view with a body-class phone rule instead of a twin]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Home/Index.cshtml
    - QuestBoard.Service/wwwroot/css/account.css
    - QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Home/Index.Mobile.cshtml
key-decisions:
  - "Gate 1 answered home.merge yes, so Home is one view and its phone twin is deleted and listed as retired"
  - "Button label shipped is 'Log in' (Gate 1 item 11); the two HomeControllerIntegrationTests assertions changed in the same commit"
  - "AreaFiles_CarryNoPlanningReference already covered both sheets and the Profile script from earlier slices, so it needed no further extension"
requirements-completed: [D-06, D-07, D-08, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8, DoC-10]
duration: ~25 min
completed: 2026-11-12
status: complete
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
---

# Phase 96 Plan 10: Landing page merge and area closure Summary

The signed-out landing page is one view in the approved unified look on both layouts, and one guard fact keeps the whole account and picker area converted.

## What was built

- **Home merged: yes.** `Home/Index.cshtml` is the one view; `Index.Mobile.cshtml` is deleted (`test -e` fails) and added to `RetiredFiles`. It links `account.css` through `@section Styles`.
- **The card.** `row justify-content-center` > `col-lg-6 col-md-8` > `.card.modern-card.landing-card.text-center`. Header `h1.mb-0` with the decorative `fa-dice-d20` in the inherited title colour and `D&D Quest Board`; the sentence unchanged at body colour; `hr`; `div.landing-action` holding the `Account/Login` link as `btn btn-primary btn-lg` with a decorative `fa-sign-in-alt`.
- **Button label shipped: `Log in`.**
- **`account.css`.** `.landing-card { margin-top: var(--qb-space-xl) }` and `.mobile-layout .landing-action .btn { display: block; width: 100% }`, tokens only, inside the pages layer. The desktop markup carries no phone class and no sheet text with `mobile.css`.
- **Screenshots checked** at desktop 1440 and phone 390: one centred card, button centred and compact on desktop, full width on the phone, headings and sentence readable.

## Tests changed and added

- `HomeControllerIntegrationTests`: `Index_ShouldContainLoginButton` and `Index_WithQuests_ShouldNotDisplayQuestList` now assert `Log in` (the `/Account/Login` assertion and the no-quest-titles checks are untouched). New `Index_PhoneUserAgent_ServesTheLandingInThePhoneShell` (phone UA: 200, `mobile-layout`, `/Account/Login`, exactly one `<h1`).
- `AccountConversionGuardTests`: `Home/Index.cshtml` added to `ConvertedViews` and `SheetLinks`, `Home/Index.Mobile.cshtml` to `RetiredFiles`; new `TheWholeAreaIsConverted` enumerates every `.cshtml` in `Views/Account`, `Views/GroupPicker`, `Views/Home` plus `Shared/AccessDenied.cshtml` and asserts: each is listed as converted, has no inline style, style block, inline script or handler, links `account.css` (the picker excepted), no retired file exists, and `account.css` and `group-picker.css` carry no colour literal and no important flag. No test deleted.

## Counts against the ROADMAP (`after/96-10/counts.json`)

| Measure | ROADMAP | After |
|---|---|---|
| Area view files | 17 | 11 |
| Hex literals in the area sheets | 4 | 0 |
| `rgb(`/`rgba(` | 14 | 0 |
| `!important` | 11 | 0 |
| Inline style attributes in views | 2 | 0 |
| Style blocks | 0 | 0 |
| Inline scripts | 2 | 0 |
| Inline handlers | 2 | 0 |
| `transition: all` (baseline at HEAD 0) | 0 | 0 |

`AccountSheets_DeclareNoTransitionOnAll` passes over the two live sheets.

## Sweep (`after/96-10`, stack mode on 8000)

`home`: 4 captures, 0 non-200. `readability-compare --zero-contrast home`: `failingRows: 0`, `residualContrast` 0, `ok`. scrollWidth 320 / 1440 desktop UA, 390 / 412 phone UA, no overflow, `smallTargets` empty. `allow.json` lists the `Log In` to `Log in` relabel; the compare passed regardless.

## Deviations from Plan

### Environment substitutions (operator-approved, see ENVIRONMENT.md)

Port 8000 for 8096, `python3` for `python`, bundled Chromium, `--stack` on the sweep.

### Auto-fixed issues

None. The plan executed as written.

### Plan wording and process

- **Tracer gate.** Neither auto-advance flag is set; as in the previous slices the gate was met by running the tracer's verify end to end (130 pinned tests green, sweep and compare `ok`) before Task 2. Operator review stays at Gate 2.
- **`AreaFiles_CarryNoPlanningReference`.** The plan asked to extend it to the Profile script and both sheets; earlier slices had already done so, so it is unchanged.
- **Phone UA.** The new fact uses the iPhone UA string the existing mobile tests use.
- Phase 97 fixture drift does not affect Home (no board listing on the page).

## Known Stubs

None.

## Threat Flags

None new. T-96-27: the page stays static; `Index_WithQuests_ShouldNotDisplayQuestList` still asserts no quest titles on `/`.

## Verification results

- `dotnet build`: 0 errors.
- Full `dotnet test` with the SQL-backed tests required: unit 1389 passed, integration 1891 passed, 0 failed.
- App on 8000 restarted on the final code and left running.

## Self-Check: PASSED

- `Home/Index.cshtml` present, `Home/Index.Mobile.cshtml` gone; `counts.json` and `area-counts.py` present under `~/qb-ui-review/phase-96`.
- Commits 08ddf036 (Task 1) and 76c48cf6 (Task 2) exist on `milestone/v9-rolling-improvements`.
