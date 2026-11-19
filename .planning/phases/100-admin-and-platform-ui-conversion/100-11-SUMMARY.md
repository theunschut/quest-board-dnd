---
phase: 100-admin-and-platform-ui-conversion
plan: 11
subsystem: ui
tags: [gate-1, mockups, admin, platform, refused-controls, look-check, html]
requires:
  - phase: 100-05
    provides: audit answers (O-3, O-9, O-12) and the approved refusal and toast sentences
  - phase: 100-10
    provides: server rules whose refusals the mocks draw
provides:
  - Gate 1 package A drawings: mocks A to F as renderable HTML with frames and a look-check result per mock
  - mask-real-rows.py and before-capture-100.mjs for real-data BEFOREs
  - mockups/index.html review aid (BEFORE, AFTER, live and Direction A references side by side)
affects: [100-12, 100-13, 100-14, 100-15, 100-16]
tech-stack:
  added: []
  patterns: [static mock pages linking the repository sheets copied at one commit, computed-style look check against Direction A and a live converted page, masking by solid block with a flat-colour verification]
key-files:
  created:
    - ~/qb-ui-review/phase-100/mockups/v1/ (site/, 27 mock pages, png/, frames.json, lookcheck.json, data/)
    - ~/qb-ui-review/phase-100/mockups/index.html
    - ~/qb-ui-review/phase-100/tools/build-mocks-100.py
    - ~/qb-ui-review/phase-100/tools/mockup-shoot-100.mjs
    - ~/qb-ui-review/phase-100/tools/lookcheck-100.mjs
    - ~/qb-ui-review/phase-100/tools/look-spec-100.json
    - ~/qb-ui-review/phase-100/tools/mask-real-rows.py
    - ~/qb-ui-review/phase-100/tools/before-capture-100.mjs
    - ~/qb-ui-review/phase-100/tools/build-sheet-100.py
  modified:
    - ~/qb-ui-review/phase-100/tools/mockup-shoot.mjs (routes --mocks to the new shooter)
    - ~/qb-ui-review/phase-100/tools/lookcheck.mjs (routes --spec to the new checker)
key-decisions:
  - "Mocks are HTML pages that render alone in a browser; frames are screenshots of them; mockups/index.html shows every frame beside its BEFORE and look references"
  - "Refusal and toast sentences are read from audit-answers.json `sentences` at build time, so they are the approved role-free wording verbatim"
  - "O-12 answered toast: the modal frames draw the empty modal, the errors state as the reopen alternative, and the Members page with the Error toast as the answered path"
requirements-completed: [D-02, D-03, D-09, D-14, D-15, D-16, D-18, D-19, DoC-4, DoC-6]
duration: ~3h
completed: 2026-11-18
status: complete
actuals:
  tokens: 21000
  tasks: 3
  commits: 1
---

# Phase 100 Plan 11: Gate 1 package A mocks Summary

**Mocks A to F of Gate 1 as renderable HTML (8 mock groups, 36 frames, desktop 1440/1024/820/320 and phone 390), built from the repository's own sheets at one commit, fixture data only, all scanned clean and equal to the look references by computed style.**

## What was drawn

All under `~/qb-ui-review/phase-100/mockups/v1/` (open any `<mock>/<page>.html` from `file://`; `mockups/index.html` is the review aid).

| Mock key | Pages | Frames (png/) | Look-check |
|----------|-------|---------------|------------|
| admin-users (A) | desktop, phone, last-admin desktop, last-admin phone | 1440, 1024, 320, phone 390, last-admin 1440 and phone 390 (6) | pass (40 and 37 properties) |
| group-index (B) | desktop, phone | 1440, 320, phone 390 (3) | pass |
| edit-user (C) | desktop, phone, invalid desktop, invalid phone | 1440, 820, phone 390, invalid 1440 and phone 390 (5) | pass (25 and 25) |
| create-user (C) | desktop, phone, invalid desktop | 1440, invalid 1440, phone 390 (3) | pass |
| group-delete (D) | desktop, phone | 1440, phone 390 (2) | pass (27 and 27) |
| members (E) | desktop, phone | 1440, 1024, phone 390 (3) | pass |
| members-modal (E) | empty and errors, desktop and phone; toast, desktop and phone | empty 1440 and phone, focus ring on the named close button, errors 1440 and phone, answered-path toast 1440 and phone (7) | pass (45 and 42) |
| refusals (F) | desktop, phone, toasts desktop, toasts phone | 1440, 1024, phone 390, focus ring on a refused button desktop and phone, the five Error toasts desktop and phone (7) | pass |

- **Look check.** `lookcheck.mjs --spec look-spec-100.json` opens each `look` frame and compares computed styles for page backdrop, card, title band, h1, table header, cell and striped cell, badge, slip, modal and primary button against the Direction A quest form, the live converted `/Players` page (frozen baseline) and, where no oak-theme counterpart exists (the slip, the modal), the UI-SPEC literals (`#2f1b0e` overlay, glass border, radius, no blur). Every mock passes with **no** property difference; the three intended differences of the UI-SPEC (new name-and-email and stacked-section arrangements, tables and slips held to live pages, the reason line as a new element) are layout or new elements that no row reads, and are listed in `lookcheck.json` under `intendedDifferences`.
- **Scan.** `mock-scan.py` over `mockups/v1`: 33 files, 0 hits (no literal outside the copied token sheet, no `!important`, no `aspect-ratio`, no `object-fit`, no `innerHTML`, no inline style or handler). A reduced-motion block is in `admin-pages.css`.
- **Provenance.** `site/PROVENANCE.txt` records commit `b3aa4b83` for the copied `tokens`, `base`, `components`, `shell`, `shell.mobile`, `mobile`, `app` sheets, vendor files, the two Phase 102 delete sheets and the board photo. The two proposed shared classes live in `site/components-proposed.css` (`.control-reason`, `.mobile-layout .row-slip` with `__head` and `__actions`, `@layer components`, tokens only) and the draft page sheets in `site/admin-pages.css` and `site/admin-pages.mobile.css` (`@layer pages`).
- **Refused controls (F).** Four kinds on desktop table cells and phone slips: self (Demote to DM and Remove from Group), last Admin (Demote to DM and Remove from Group), account reach (Edit, and Resend Welcome Email for an unconfirmed row), and the Platform Users own-account Disable. Every refused control is a `button` with `aria-disabled="true"` and `aria-describedby` outside any form, in the tab order, with a `.control-reason` line and lock icon; none relies on `title` (checked in `frames.json` per frame: all `outsideForm`, `reasonFound`, `inTabOrder`, none `titleOnly`). Sentences are the approved ones from `audit-answers.json` verbatim: reasonSelf, reasonLastAdmin, reasonReach, the own-account sentence kept from today, and the toasts toastSelf, toastLastAdmin, toastReach, the own-account toast, toastStale.
- **Real data.** BEFOREs of Group/Index (3 frames) and Members (3) and the Members modal (2) list real boards or users: every non-fixture row or slip (21 of 26 board rows, 39 of 63 member rows) is covered by a flat block, verified flat by `mask-real-rows.py`; the unmasked raw images stay in `private/before-raw/` (mode 700 directory under the scratch root) and are never referenced by a package. `frames.json` marks those frames `masked: true`. Admin/Users, EditUser, CreateUser and Group/Delete BEFOREs are p100 fixture pages and are unmasked. Mocks themselves hold fantasy names and `@example.invalid` addresses only; no credential, `localhost` or `127.0.0.1` in any mock HTML.
- **Approved answers followed.** O-3 (peer Admin reachable, own Edit reachable, own reset allowed, so the self row's Edit is active and only role actions and removal are refused), O-9 (generic reach sentence), O-12 toast (the answered path is drawn; the reopened modal is drawn as the alternative only).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Live reference taken from the frozen baseline on 8341, not 8340**
- **Found during:** Task 1
- **Issue:** 8340 was not running; the converted Phase 98 and 99 pages are unchanged by phase 100 (no Phase 98/99 view was edited), so the frozen baseline renders the same references.
- **Fix:** `before-capture-100.mjs` and `lookcheck-100.mjs` read the live `/Players` page as the p100 Admin on 8341. No app was started or stopped by this plan; 8341 was left running.
- **Files modified:** scratch tools only

**2. [Rule 3 - Blocking] The Direction A shop slip is not an oak-theme reference**
- **Found during:** Task 1 look check (slip background `rgb(18, 22, 43)` navy)
- **Issue:** the only Direction A slip is the navy Shop theme, so equality against it is meaningless for the oak area.
- **Fix:** the slip row is held to the UI-SPEC look-check literals (overlay `#2f1b0e`, glass border, 8px radius, no blur); recorded in `look-spec-100.json` with a note. Gate 1 must know the slip has no live oak counterpart yet.

**3. [Rule 1 - Bug] Mock defects found in the first shots and fixed**
- the board photo was missing (copied `images/` beside `site/`); the reason line split its icon from its sentence in a right-aligned cell (now a compact block flush right, `44ch`); `Remove Member` wrapped inside a narrow column (button labels never break); phone slip buttons were top-aligned in their 44px box (inline-flex centring).

**4. [Scope note] No repository file changed, so Tasks 1 and 2 carry no commit.** The plan's `files_modified` is empty; the only commit of the plan is the docs commit below. The tracer gate (Task 1 verify) passed end to end before the other mocks were drawn: tracer verified, expanding.

**5. [Coherence] Last-Admin row is a variant frame, not a row of the main table.** A non-SuperAdmin Admin always has a peer when another Admin row exists, so a last-Admin row can only be seen by a SuperAdmin. Mock A therefore draws it as `admin-users-last-admin-*` (SuperAdmin viewing the one-Admin board, with its BEFORE captured live from the baseline). Mock F is a pattern sheet with one row per refusal kind, and says so in its page title only; its frame captions on the index carry the explanation.

## Known Stubs

None. No repository file was changed; the mocks are static pages and none is wired to data by design.

## Threat Flags

None. Nothing under `QuestBoard.Service` or the tests was touched (`git status --porcelain -- QuestBoard.Service QuestBoard.IntegrationTests` prints nothing).

## What Gate 1 (100-12) must know

- Open question 6c (Admin vs Administrator): the mocks draw the recommended `Admin` word in the badge; no sentence names a role. Decide 6c at Gate 1 and redraw only if the answer is `Administrator`.
- Items 8 (Remove from Group confirm sentence) and 9 (password hint) are text-only Gate 1 questions; the mocks keep today's confirm sentence on `data-confirm-message` and draw no password hint.
- O-12 was answered toast: frame `members-modal-toast-*` is the answered path; the errors frames show what a reopened modal would look like if the operator reverses it.
- The Admin/Users reach row refuses only Edit (and Resend for an unconfirmed row); role buttons and Remove stay live for it, as the server rules built in 100-06 to 100-10 do.
- The index (`mockups/index.html`) lists each frame beside BEFORE, the live reference and the Direction A quest form; every mock page also opens on its own. Real-data BEFOREs are masked; never use anything from `private/`.
- `before-capture-100.mjs` re-captures every BEFORE and live reference (read only, plus opening the modal); `build-mocks-100.py`, `mockup-shoot.mjs --mocks`, `lookcheck.mjs --spec` and `mock-scan.py` regenerate, reshoot and re-verify a redraw.

## Self-Check: PASSED
