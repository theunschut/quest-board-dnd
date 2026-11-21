---
phase: 103-themed-error-pages
plan: 11
subsystem: error-pages-gate1-mockups
tags: [error-pages, gate-1, mockups, look-check, phone, drawings-only, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: structural error page and its copy (103-04, 103-05, 103-06), frozen baseline and BEFORE frames (103-03)
provides:
  - Gate 1 mockups M1 to M11 under ~/qb-ui-review/phase-103/mockups/v1 (75 frames: 46 desktop, 29 phone), every desktop drawing with its phone frame
  - Draft site/error.css, site/error.mobile.css and site/error-actions.js, written as they would ship (tokens only, layered, no important flag, no colour literal, no crop rule, no innerHTML, a reduced-motion block)
  - Mock tooling: build-mocks-103.py, mockup-shoot-103.mjs, lookcheck-103.mjs, look-spec-103.json, read-copy-103.mjs, build-contact-sheet-103.py
  - frames.json (mock, variant, viewer, direction, User-Agent, width, BEFORE pairing, probes) and lookcheck.json (every look-checked frame passes)
  - A reviewer's contact sheet at ~/qb-ui-review/phase-103/mockups/gate1-v1-review.html
affects: [103-12]

actuals:
  tokens: 17800
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Mock copy read from the live structural page (copy-103.json), so drawings and build cannot disagree on a word"
    - "A phone frame is the Pixel 7 device descriptor with touch at 390 CSS pixels and its own phone markup, never a resized desktop window"
    - "Look check by computed style against live references, image urls compared by file name"

key-files:
  created:
    - .planning/phases/103-themed-error-pages/103-11-SUMMARY.md
  modified: []

key-decisions:
  - "The seal edge is drawn as a 1px glass-border ring plus a second 1px ring four pixels in, in color-mix(fg 25%); the UI-SPEC marks it tunable at Gate 1"
  - "Go back is drawn visible on M1 to M8 (the case where the page has a Referer link or history, so the contract's four-button row shows) and hidden then revealed on M9"
  - "On the anonymous 403 the desktop row follows the contract as written (Log in primary in the middle, Home rightmost); the primary-rightmost alternative is drawn in M11 for the operator"
  - "The converted-page look reference is /Players (phase 98, default oak theme) with the live Login card and today's AccessDenied; /Shop is measured for information only because it sets its own dark blue page theme"

requirements-completed: [D-02, D-03, D-08, D-10, D-12, D-15, D-16, D-17, D-18, D-19]

duration: 80min
completed: 2026-11-21
status: complete
---

# Phase 103 Plan 11: Gate 1 mockups Summary

**The shared error page drawn in every variant on desktop and phone, as it would ship, with the build's own words, beside today's responses, measured equal to the live Login card, today's AccessDenied and the converted Players page on every UI-SPEC look-check property, and not one repository file touched.**

## What was drawn

Scratch root `~/qb-ui-review/phase-103/mockups/v1/`. Nothing in the repository changed (`git status` on `QuestBoard.Service` and `QuestBoard.IntegrationTests` is empty); this plan's only commit is the docs commit.

| Mock | Drawn | Desktop frames | Phone frames | Today's response beside it |
|------|-------|----------------|--------------|----------------------------|
| M1 | 404, one board, Direction A and Direction B | 4 (1440, 1024 each) | 2 | blank 404 |
| M2 | 404, two boards, Switch board; wraps at 820 and 320 | 4 (1440, 1024, 820, 320) | 1 | blank 404 |
| M3 | 404 signed out, Log in with no return URL | 2 | 1 | blank 404 |
| M4 | 500 with chip and hint, short id and 40 character unbroken id | 6 (1440, 1024, 320 each) | 2 | bare 500 |
| M5 | 403 signed in | 2 | 1 | today's AccessDenied |
| M6 | 403 signed out, Log in the one wood primary | 2 | 1 | today's AccessDenied (signed out) |
| M7 | 429 signed out, unchanged sentence | 2 | 1 | plain text |
| M8 | 400, 409 and the generic page | 3 | 3 | bare responses |
| M9 | Go back hidden and shown (real script, history length 1 and 2), 1.4.12 text spacing on M4 and M2, focus ring on primary, secondary, info and Log out, phone focus order in four stops, 200 percent zoom at 1024 on M2 and M4 | 12 | 8 | |
| M10 | the equality tables, one per layout, from `lookcheck.json` | 1 | 1 | |
| M11 | proposals, each beside its alternative (items 2, 3 (twice), 5, 6, 7, 8, 11) | 8 | 8 | |

Phone frames use the Pixel 7 device descriptor (Android User-Agent, which `MobileDetectionMiddleware` matches; touch; `isMobile`) with the width set to 390, at scale 2, and the phone twin's own markup; every one reports `mobileLayout: true`, no horizontal overflow, and no control under 44px. Desktop frames report the desktop layout, no overflow at 1440, 1024, 820, 320 and at 200 percent zoom, and DOM order equal to drawn order for every control row.

## Look check

`lookcheck.json`: every look-checked frame (M1 to M8, desktop 1440 and phone 390, 24 frames) passes, 75 property rows each: backdrop colour and image, card (glass over scrim, border, radius, shadow, blur), title band, card body, h1, primary and secondary buttons (fill, border, radius, padding, weight), bar, brand and footer. References: the live anonymous Login card (8350), today's AccessDenied and the Players page (frozen baseline 8351, p103 fixture account). The four intended differences are named once at the top of M10 and are not measured properties: the seal, the docket line and reference chip, the brand-only bar, the action row. `mock-scan.py`: 68 files, 0 hits (self-test: 18 seeded bad strings caught).

Two measured observations, recorded in `look-spec-103.json`:

- The Login submit is `btn-lg` (20px, line height 30px); every other button on the converted pages and on the error page is 16px / 24px. Font size is not in the UI-SPEC button row (fill, radius, padding, weight), so the primary button is compared without it.
- `/Shop` sets its own page theme (a dark blue palette), so it cannot equal the oak look by number. It is measured for information in M10 (colour rows show different, geometry rows equal) and `/Players` is the converted oak-theme reference.

## Copy source

Every heading, docket line, sentence, hint, button label and glyph comes from `tools/copy-103.json`, read by `read-copy-103.mjs` from the structural page on 8350 for each variant, viewer and layout (desktop and mobile copy asserted identical). The M1 HTML heading, docket and sentence equal the live page character for character. Words written by hand: the made-up reference ids (`0HN7K2P9QX4VD:00000007` and a 40 character id), the footer version string, and the alternatives drawn beside the build's copy in M11 (the sentence-case 403 heading, the "Too many requests" heading).

## Proposals drawn in M11

Item 2 (A against B), item 3 (recommended roles against the research mapping, signed in and signed out; and the anonymous 403 primary in the middle against rightmost), item 5 (the signed-out 404 sentence as it reads, no alternative proposed), item 6 (403 heading kept against sentence case), item 7 (429 heading), item 8 (chip with hint against chip alone), item 11 (glyphs and colour tokens against all-parchment).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Third look reference /Shop sets its own page theme**
- **Found during:** Task 1 look check
- **Issue:** `/Shop` (named in the plan) renders in a dark blue page theme, so backdrop, card tint, title band, bar and footer colours can never equal the oak page; the first run failed 9 rows by design.
- **Fix:** `/Players` (phase 98, default oak theme) is the converted-page reference that gates the check; `/Shop` stays in M10 as an information column. The UI-SPEC says the page is held to the converted pages in the default theme.
- **Files modified:** `tools/look-spec-103.json`, `tools/lookcheck-103.mjs` (scratch only)

**2. [Rule 1 - Bug] Stale baseline session for p103player**
- **Found during:** Task 1 look check
- **Issue:** the saved 8351 state for p103player no longer held an active board, so `/Shop` redirected to the board picker.
- **Fix:** re-ran `login.mjs p103player --port 8351` (fixture account, credentials not printed).

**3. [Rule 1 - Bug] Go back showed "hidden" frames visible**
- **Found during:** Task 2 review of M9
- **Issue:** a fresh Playwright tab already holds a blank history entry, so `history.length` was 2 and the script revealed Go back in the "no history" frame.
- **Fix:** the no-history frame is opened as a new window (`window.open`), whose history holds only itself; `historyLength` is recorded in `frames.json` (1 and 2).

### Plan deviations to note

- M11 and M10 are shot at 1440 and the phone only (not 1024); the plan's "1440, 1024 and phone" applies to the page mocks M1 to M8, which have all three.
- The anonymous 403 and the phone button order for it are the planner's reading of an ambiguous contract: phone is Log in (primary), Home, Go back; desktop as written. Both orders on desktop are drawn in M11 (item 3).
- The structural build on 8350 still renders Home as the primary on the anonymous 403 and keeps one DOM order for both layouts; those are the visual build's job after Gate 1 (the mocks follow the UI-SPEC, not the structural view).
- Real-device contrast sweep, 44px check against the finished page and the click-through are Gate 2; Gate 1 shows the computed equality, the 44px floor on the phone frames and the focus ring frames.

## Authentication gates

None.

## Known Stubs

None (drawings only; no repository file created or changed).

## Threat Flags

None (no new network, auth, file or schema surface; fixture data and a made-up reference id only; no SuperAdmin or picker capture; no credential, localhost or 127.0.0.1 string in any mock page).

## Left for the operator

Gate 1 itself is 103-12. The reviewer's contact sheet is `~/qb-ui-review/phase-103/mockups/gate1-v1-review.html`; items 1, 2, 3 and 12 need an explicit answer.

## Self-Check: PASSED

Checked: `mockups/v1/frames.json` holds 75 frames covering m1 to m11, m1 to m8 each with a phone frame, M2 at 1440, 1024, 820 and 320; `lookcheck.json` has every key passing; `mock-scan.py` 0 hits; the M1 copy equals the live page; no `localhost` or `127.0.0.1` in any mock HTML; `git status --porcelain -- QuestBoard.Service QuestBoard.IntegrationTests` empty.
