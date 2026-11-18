---
phase: 100-admin-and-platform-ui-conversion
plan: 22
subsystem: testing
tags: [closing-sweep, accessibility, click-throughs, live-refusals, platform-layouts, sql-suites, finding-trace, security-audit-record]
requires:
  - phase: 100-21
    provides: the closing guards and a zero census; every view converted
  - phase: 100-20
    provides: the EmailStats stub seam, the Platform layout probes, tiles-100.mjs
provides:
  - The final AFTER of all 32 manifest rows under both User-Agents (after/final/, 139 records) with 0 regressions against BEFORE
  - contrast-exceptions.json: 7 entries and 4 decisions for explicit Gate 2 answers
  - findings-trace.json: all 47 owned findings with owner, BEFORE and AFTER value and status
  - after/final/a11y.md, clicks.json, refusals.json, layouts.json, sql-suites.txt and the other probe records
  - 100-SECURITY.md final (status verified-pending-audit) and deferred-items.md closed
affects: [100-23, gate-2, phase-security-audit]
tech-stack:
  added: []
  patterns: [a refused control is disabled, so its server refusal is proven with a crafted post that carries the page token while the page is checked for the disabled control and its visible reason under both User-Agents; destructive click-throughs target throwaway rows by their fixture name and are dismissed first, then accepted, then restored]
key-files:
  created:
    - .planning/phases/100-admin-and-platform-ui-conversion/100-22-SUMMARY.md
  modified:
    - .planning/phases/100-admin-and-platform-ui-conversion/100-SECURITY.md
    - .planning/phases/100-admin-and-platform-ui-conversion/deferred-items.md
key-decisions:
  - "No repository source file changed: the one regression the sweep found (Group/Delete heading order, h1 then h3) is a Gate 2 entry and a gap-closure request, not a fix here"
  - "Every remaining or inherited shortfall is an explicit entry for a Gate 2 answer; none is treated as approved"
  - "The proof tools that click now-disabled buttons were adapted: refusals are proven by a crafted post with the page token plus a read of the disabled control and its visible reason on both layouts"
  - "The SQL-backed suites ran in two invocations (15 + 2) because four classes are SQL-gated, not the three the plan's filter names"
requirements-completed: [D-01, D-02, D-03, D-04, D-08, D-09, D-10, D-11, D-17, D-21, DoC-5, DoC-6, DoC-7, DoC-10]
duration: ~1h
completed: 2026-11-18
status: complete
actuals:
  tokens: 12300
  tasks: 3
  commits: 1
---

# Phase 100 Plan 22: Closing sweep Summary

**The finished Admin and Platform area measures clean against BEFORE on every row under both User-Agents (95 comparisons, 0 failing), every destructive form and every server refusal is proven live on p100 fixtures, the 17 SQL-backed facts pass, all 47 owned findings have an outcome, and the one in-main contrast failure, one regression and four look items are recorded as explicit Gate 2 entries.**

## Performance

- Build: 0 errors, 72 warnings (none in a file this plan touched; the plan changes no repository source file)
- Unit 1636 passed. Integration 3036 passed, 17 skipped (the SQL-gated facts). SQL-backed suites with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`: 17 passed, 0 skipped.
- `git status --porcelain -- QuestBoard.Service QuestBoard.Domain QuestBoard.Repository QuestBoard.IntegrationTests QuestBoard.UnitTests` prints nothing.

## Tracer (Task 1)

The closing method was proven on Admin/Users first: `admin-users`, `admin-users-lastadmin` and `admin-users-focus-refused` swept under both User-Agents (14 captures, all 200; kept under `after/final/tracer/`), `readability-compare.py` against BEFORE for `admin-users` (5 compared, 0 failing, ok), the proofs `reach-refused-reset`, `guard-self-demotion` and `guard-last-admin` refused and unchanged, and the Admin/Users findings traced. The gate was verified by running these before expanding.

## Final AFTER and the comparison with BEFORE (Task 2)

- **Rows swept:** all 32 manifest rows (the two EmailStats stats and error rows with the scratch stub, restarted without it afterwards; the key-missing row without it), 139 records, no capture failed, no action or assert failed.
- **`readability-compare.py` over every row:** 95 compared, 0 failing (no new contrast failure, lost control, overflow, small phone target, missing focus ring or console error). With `--zero-contrast phase`: the only in-main failure left is `#board-delete-help` on Group/Delete, which is an entry of `contrast-exceptions.json`; with the file the residual is 0.
- **Findings by status (47 of 47):** closed 40, verified 2 (A-shell-037, A-shell-038), recorded 3 (A-admin-018, -023, -027), moot 1 (A-admin-043), exception 1 (A-admin-013, the same Group/Delete help line). A-admin-038 closed and A-admin-043 moot carry the 100-21 result. P-members-modal-binding closed. Every owner is one of the owner table's plans.

### Accessibility bar (`after/final/a11y.md`)

| Check | Result |
|-------|--------|
| Contrast in main (A-1) | 4 failures over 139 records, all `#board-delete-help` (exception). Pixel check at the lightest pixel behind the text: 80 cases, 72 pass, 8 not present on that layout (the phone lists show names only, no tables), 0 below the bar, worst 5.12:1 |
| Phone targets (A-5) | 0 under 44px on every phone record |
| Desktop targets (A-5) | 2 under 24px, both the toast Close button (21px, inherited, shared toast partial, Gate 2 entry); 0 inline links under 24px |
| Reflow and 1.4.12 text spacing (A-6) | no horizontal scroll on any record (320 desktop layout, 390 phone layout); 0 records with a text-spacing overflow |
| Focus ring (A-2/A-3) | 0 missing in the sweep; 56 frames, one per interactive kind and layout on glass, paper and overlay: ring on all 55 present targets (1 not present on the phone), none clipped by an ancestor, including the refused `aria-disabled` buttons and the controls at a `.table-responsive` edge |
| Focus obscured (A-4) | 0 over 64 tab-throughs |
| Reduced motion (A-7) | emulated `prefers-reduced-motion: reduce`: 0 running animations and 0 transitions over 1 ms on every row; 0 infinite animations and 0 `transition: all` with a duration without the preference |
| Names and landmarks (A-8) | one h1 on every row; 0 unnamed controls; 2 heading-order skips, both Group/Delete (h1 then h3, new this phase, Gate 2 entry) |
| Console | 0 console, page or failed-request errors on all 139 records |
| EmailStats tiles with the stub | 6 records (1440/390/320 desktop, 412/390/320 phone): figures at least 9.87:1, labels at least 11.79:1, 4/2/1 columns, no overflow, no clip under the stress |
| Submit in flight (POST held back) | Group/Delete (the only form that opts in to the `site.js` lock): the button locks and stays legible, 5.94:1 desktop and 6.31:1 phone. Create User, Reset Password, Create Group and Edit User do not opt in (as at the baseline): the button stays enabled, 6.95:1 (decision `D100-no-submit-lock`) |
| Platform layouts (7 pages, both User-Agents) | `layouts-100.mjs`: 231 records, 0 failing (skip link, landmarks, nav contrast, toggler size, reach through the collapsed toggler, foundation sheets in order) |
| Quest and Calendar unmoved | computed styles on `/Calendar`, a Quest Manage and a Quest Details page, both User-Agents, against the frozen baseline on 8341: 0 differences; the home page differs only by the port number inside background URLs |

### Click-throughs on p100 fixtures (`after/final/clicks.json`, 34 records, all ok)

Targets were picked by fixture name only. Under both User-Agents: **Delete Quest** (throwaway quests `P100 Audit Throwaway 1` and `2`, created as p100dm) dismissed (0 requests, quest still listed) then accepted (one POST, quest gone, Success toast); **Remove from Group** (throwaway member `P100 Audit Throwaway Member` of P100 Audit Lone, created by the SuperAdmin through Create New User) dismissed then accepted, then added back through Add; **Remove Member** (Platform Members) the same; **Disable** (Platform Users) dismissed then accepted, then Enabled; **Remove Board** (`P100 Audit Campaign Two` in `P100 Audit Set`) dismissed then accepted, then re-added through Add Board; **Delete Set** (`P100 Audit Lone Set`) dismissed only. 12 dismissals, 10 acceptances; in every one the dialog text equals the server-rendered `data-confirm-message` and the expected sentence. Cleanup verified: the throwaway quests are deleted, the throwaway member is on no board (P100 Audit Lone empty) and its account is disabled; `fixtures.mjs --check` passed after.

### Live refusals (`after/final/refusals.json`, 10 records, all refused)

`reach-refused-reset`, `reach-refused-edit`, `reach-refused-superadmin` (superAdminAsMember yes), `reach-refused-ui` (the other-board Admin and the SuperAdmin account: Edit and Resend Welcome Email are disabled buttons with the visible reason, both User-Agents), `guard-self-demotion`, `guard-self-removal`, `guard-last-admin` (the SuperAdmin on P100 Audit Campaign), `guard-stale`, `platform-remove-non-member-after` (Warning, nothing removed) and `deletequest-cross-board-post`. The reach refusal reads "Only a SuperAdmin can change that account." and the guards "You cannot change your own role or remove yourself from the board." and "A board must keep at least one member who can manage it. Give someone else that role first."; `allowed-paths.json` holds the allowed reset (Success toast naming the account).

### SQL-backed suites (`after/final/sql-suites.txt`)

Operator `sqlSuites: yes`. Run once with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and `migrator.env` loaded into that command's environment (never printed; the records contain no connection detail). The plan's filter (Migrator, QuestDeleteSqlServerTests, BoardDeletionSqlServerTests) passed 15 of 15; the fourth SQL-gated class, `ShopItemSqlServerTests`, passed 2 of 2. Together they are the 17 facts the default run skips.

## Exceptions and decisions for Gate 2 (`contrast-exceptions.json`)

| Id | What | Measured |
|----|------|----------|
| A100-board-delete-help | Group/Delete help line | 4.25:1 at the lightest possible board pixel, identical BEFORE (6.95:1 and 5.56:1 at the pixel actually behind it) |
| A100-switch-knob | white knob on the gold checked track, Group/Edit | 1.63:1 knob to track, 1.47:1 checked to unchecked track |
| A100-wraps-1440 | Group/Index Delete wraps (4 of 26 rows); Members addresses wrap | structure only, no overflow |
| A100-toast-close-21px | toast Close button, desktop | 21 by 21 px, inherited |
| A100-delete-heading-skip | Group/Delete h1 then h3 | one level skipped, new this phase |
| D100-no-submit-lock | admin forms do not opt in to the submit lock | button stays enabled in flight |
| D100-emailstats-alert | persistent `role="alert"` box on Email Stats | informational |
| D100-questservice-removeasync | `QuestService.RemoveAsync` separate saves | residual, older than the phase |
| D100-pixel-vs-conservative | sweep vs pixel-check figures | method note |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The phase-99 proof and closing tools click controls that no longer submit**
- **Found during:** Task 1
- **Issue:** `audit-proofs.mjs` proves the guards by clicking "Demote to DM" and "Remove" on the signed-in Admin's own row and the only Admin's row; those controls are now disabled buttons, so a click posts nothing and the old `refused` test (an Error toast after the click) would read false. Some of its records also carry no `refused` key, which the plan's verify one-liner reads.
- **Fix:** copies named `-100-22` (as the plan permits): `audit-proofs-100-22.mjs` proves each refusal with a crafted post carrying the page token and reads the disabled control with its visible reason on both layouts, and every record carries `refused`; `pixel-check`, `focus-frames`, `submit-inflight`, `a11y-table` and `findings-trace` were re-pointed to this area's kinds, rows and findings. Scratch tools only; no repository file.

**2. [Rule 1 - Bug, in my own tool] Two measurement artefacts corrected before recording**
- The pixel check first scored rounded badges and bordered alerts against their own corner or border pixels (1.0 to 1.4:1); the lightest pixel is now taken from the inner area, scaled for the device pixel ratio. The click-through cleanup check first counted the Available Users list as the member still being listed; it was re-verified with the corrected check (`clicks-cleanup-verify-100-22.mjs`) and the record replaced.

**3. [Rule 3 - Blocking] SQL-gated class missed by the plan's filter**
- `ShopItemSqlServerTests` is also gated on the migrator environment; it was run in a second invocation so all 17 skipped facts ran.

**4. [Method] A-admin-019 judged from the focus frames**
- The Tab-walk extras read the box shadow of text inputs mid-transition, identical BEFORE and AFTER; the verdict (one focus look on 16 focused form controls of eight kinds) comes from the settled frames.

**5. [Method] The tracer rows were swept to `after/final/tracer/`**
- The full sweep then wrote `after/final/report.json`; the tracer report is kept beside it.

None of these changed a repository file. One slip: the reach-allowed proof (`reach-allowed-reset`, p100player) invalidated that account's saved session, so `fixtures.mjs --check` failed once until `login.mjs p100player` refreshed the state; no fixture changed.

## Issues Encountered and open items

- **Regression found (not fixed, by rule):** Group/Delete heading order (h1 then h3 on both layouts; BEFORE h2 then h3). A gap-closure plan would turn the two h3 of `_BoardDeleteBody.cshtml` into h2 and update the render fact that pins them. Gate 2 entry `A100-delete-heading-skip`.
- **Open for Gate 2:** the Group/Delete help line contrast, the switch knob, the two 1440 wraps, the 21px toast Close, the submit-lock decision and the `QuestService.RemoveAsync` separate-save residual. All are in `contrast-exceptions.json` and `deferred-items.md`.
- The first run of the click-through tool created the throwaway member (the second run recorded it as already on the board); `after/final/clicks-run1-note.txt` records that.
- The throwaway account `P100 Audit Throwaway Member` stays as a disabled p100 account on no board (an account cannot be deleted through the app).

## Authentication Gates

None.

## Known Stubs

None (no source file changed).

## Threat Flags

None (no source file changed, no new endpoint or trust boundary). Mitigations of the plan's threat model: T-100-73 every accepted action targeted a throwaway p100 row created for it and `fixtures.mjs --check` passed after; T-100-74 the migrator connection never appeared in any record or output (grep of the run files: 0 hits); T-100-75 real-member captures stay under the scratch root and every report here is structural; T-100-76 each gap row of `100-SECURITY.md` cites its commit and fact, or the operator's words.

## Self-Check: PASSED

- `100-SECURITY.md`: `status: verified-pending-audit`, 54 gap and closing rows, none open; `deferred-items.md` closing section present.
- `findings-trace.json`: 47 findings, every one with a status and an owner from the owner table; `contrast-exceptions.json`: every entry has `measured`, `element`, `page`, `ua`, `consequenceIfRejected`.
- `after/final/clicks.json` 34 records ok under both User-Agents; `refusals.json` 10 of 10 refused; `layouts.json` 231 of 231 pass; `a11y.md` names reduced motion and 1.4.12; `sql-suites.txt` 17 passed, 0 skipped.
- No repository source change: the porcelain check over the five projects prints nothing.
