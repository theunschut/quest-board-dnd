---
phase: 100-admin-and-platform-ui-conversion
plan: 23
subsystem: release-gate
tags: [gate-2, operator-approval, gap-closure, heading-order, release-handoff]
requires:
  - phase: 100-22
    provides: the final AFTER, contrast-exceptions.json, findings-trace.json and the evidence the sheet shows
provides:
  - Gate 2 answered by the operator (gate2/answers.json, contrast-exceptions.json statuses)
  - The e5 gap closure: Group/Delete reads h1 then h2 on both layouts, pinned by a render fact
  - Gate 2 sheet v2 (gate2-v1 kept beside it) with the group-delete pair re-captured under both User-Agents
  - The release hand-off for v6.5.0
affects: [release]
tech-stack:
  added: []
  patterns: [a one-heading-level gap closure is a view change plus a render fact over a seeded page that shows every section, run on both layouts]
key-files:
  created:
    - .planning/phases/100-admin-and-platform-ui-conversion/100-23-SUMMARY.md
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
    - QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs
    - .planning/phases/100-admin-and-platform-ui-conversion/deferred-items.md
key-decisions:
  - "e5 is fixed, not accepted: the three section headings of the delete body become h2; no stylesheet changed because base.css gives h2 and h3 the same size and the delete sheets style the heading by its class"
  - "e1 to e4 and d1 to d3 are accepted exactly as they stand; none needs a gap-closure plan"
  - "Gate 2 v2 is rebuilt into gate2/ with the answered v1 kept as gate2-v1/"
requirements-completed: [D-01, D-09, D-14, D-15, D-16, D-17, D-18, D-19, D-21, DoC-4, DoC-5, DoC-6, DoC-7, DoC-10]
duration: ~2h
completed: 2026-11-19
status: complete
actuals:
  tokens: 4200
  tasks: 3
  commits: 2
---

# Phase 100 Plan 23: Gate 2 and release hand-off Summary

The operator approved the Gate 2 sheet with one fix (the Group/Delete heading order), the fix is made and re-captured, and the phase is ready for one release, v6.5.0, through a pull request the operator opens.

## Gate 2 decision

Sheet v1 was answered in chat with this reply line, recorded verbatim in `~/qb-ui-review/phase-100/gate2/answers.json` (answerSource "operator"):

`approved pairs:approved e1:accept e2:accept e3:accept e4:accept e5:fix d1:accept d2:accept d3:accept t1:accept t2:accept t3:accept t4:accept t5:accept t6:accept t7:accept t8:accept t9:accept t10:accept t11:accept t12:accept t13:accept t14:accept t15:accept t16:accept t17:accept findings:approved q:approved fixtures:keep`

- Versions: answered on v1; v2 was rebuilt after the e5 fix with only the group-delete pair re-captured (`gate2-v1/` keeps the answered sheet). gate-hygiene.py passes on v2 (289 JPEG, only README.md, answers.json, img, index.html).
- Pairs, findings (46 owned) and inherited questions: approved. Fixtures: keep.
- The v2 sheet's e5 text still shows the v1 recommendation ("fix"); the answer and the fix are recorded here and in answers.json.

## Exceptions

Statuses are set in `~/qb-ui-review/phase-100/contrast-exceptions.json` (`approved.status`, with `gateItem`).

| Gate item | Exception | Answer |
|-----------|-----------|--------|
| e1 | `A100-board-delete-help`: Group/Delete help line, 4.25:1 at the lightest possible pixel (6.95:1 desktop, 5.56:1 phone at the pixel behind it) | accepted |
| e2 | `A100-switch-knob`: white knob on the gold track, 1.63:1 | accepted |
| e3 | `A100-wraps-1440`: Group/Index Delete wraps on 4 of 26 rows, Members Email cell wraps | accepted |
| e4 | `A100-toast-close-21px`: toast Close button 21 by 21 px on desktop (shared partial) | accepted |
| e5 | `A100-delete-heading-skip`: Group/Delete h1 then h3 | fix (made, see below) |
| d1 | `D100-no-submit-lock`: four admin forms without the submit lock, as at the baseline | accepted |
| d2 | `D100-emailstats-alert`: persistent alert box on Email Stats states | accepted |
| d3 | `D100-questservice-removeasync`: signups deleted in separate saves before the final tracked save | accepted (stays open for a later review) |

`D100-pixel-vs-conservative` is a method note, not a Gate 2 item; its status is recorded as information. The text changes t1 to t17 are all accepted as built.

## The e5 fix

- `QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml`: "What will be deleted", "Kept, but unlinked" and "Not affected" are h2 (were h3). Both Delete views share this partial, so desktop and phone read h1, h2.
- CSS: nothing changed. `base.css` maps h2 and h3 to the same size step and leading, and the heading's spacing comes from `.board-delete-heading` in both delete sheets (no h3 selector anywhere in them); `.modern-card-header :is(h1, h2)` only reaches headings inside a card header, which these are not. The look is unchanged.
- Fact: `PlatformBoardDeleteTests.Get_HasOneH1ThenH2SectionHeadings_WithNoSkippedLevel` (both layouts) seeds a board with a follow-up quest on another board so all three sections show, and asserts the heading levels are exactly 1, 2, 2, 2 with the three texts in order. No earlier fact pinned the h3, so no test name changed and `tests-renamed.md` has no 100-23 entry.
- Commit `37fe6df0` (`fix(100-23)`).

### Re-capture

SQL Server at localhost:1433 answered when this step ran (it had been down earlier), so the re-capture was done. Only the group-delete row was re-run: `run-app.mjs` on 8340 from the working tree, `fixtures.mjs --check` passed under both User-Agents, `sweep.mjs --pages group-delete --out after/final --merge` (4 captures: desktop 1440 and 320, phone 390 and 412, 0 not 200). The record shows h1, h2, h2 under both User-Agents at every width, no overflow, no small target and no console error; the single contrast entry per capture is the accepted e1 line, unchanged. `readability-compare.py` for the row (allow file and the exceptions file, `--zero-contrast phase`): ok, residual contrast 0. `after/final/a11y-extra.json` heading skips for group-delete set to 0 from this re-check (`report.pre-100-23.json` keeps the earlier report). 8340 was stopped afterwards; 8341 untouched.

## Owned findings

46 owned findings, approved at Gate 2: 40 closed, 2 verified, 3 recorded by design (Q-86, Q-89, Q-90, Q-94 twin differences), 1 moot, 1 exception. The exception is A-admin-013 (muted text on the glass card), which is exactly e1 and is accepted. The heading-order regression the closing sweep found is closed by the fix above.

## Server rules shipped

From `100-SECURITY.md`:

- G-01 closed: Reset Password and Edit User refuse an account with more reach than the actor (GET and POST).
- G-02 residual: reach rule closes elevated accounts; an ordinary account also on another board can still be added by email.
- G-03 closed: role actions judge the resulting role (no self-demotion, no last Admin removed).
- G-04 closed: a stale or out-of-board target answers an Error toast.
- G-05 closed: quest delete removes reminder rows and clears an other-board follow-up's link through the change tracker (the separate signup saves remain, d3).
- G-06 closed: the resend budget is taken after the scope, reach and already-confirmed checks.
- G-07 closed: the reach rule applies to the resend.
- G-08 closed: a refused email change writes nothing and answers a redirect with an Error toast.
- G-09 closed: a per-actor limit on account creation.
- G-10, G-11, G-16, G-17 residual, accepted as designed.
- G-12 residual, deferred to the email phase.
- G-13 closed: Remove Member answers success only when a row was removed.
- G-14 closed: the SuperAdmin removal is exempt from the last-Admin rule by decision.
- G-15 closed: a failed Create Member redirects with an Error toast.
- G-18 closed: the Remove from Group sentence names the deleted availability answers.
- G-19 closed: the Reset Password hint and rule match the 8-character policy.
- G-20, G-21, G-22: no gap.
- G-23 closed: crafted requests answer a redirect with an Error toast.
- X-1, X-2, X-3 closed (Demote to DM matches its label, the reset success toast shows, the Create New User dialog binds).

## Release handoff

The phase is ready for one release, v6.5.0, through a pull request from `milestone/v9-rolling-improvements` into `main` that the operator opens when they choose, followed by the existing draft-release deploy approval. No plan of this phase opened a pull request, created a tag, merged or touched `main`, and no review branch was published.

Caveat: `.github/workflows/docker-publish.yml` publishes the image on every tag push matching `v*.*.*`, regardless of the draft-release approval in `release.yml` (environment `deploy`). Tagging therefore publishes the image before the deploy approval.

## Fixtures for the operator

The p100 fixtures are kept (answer fixtures:keep): boards P100 Audit One-Shot, P100 Audit Campaign, P100 Audit Campaign Two, P100 Audit Empty and P100 Audit Lone; the accounts qb-audit-p100admin, p100dm, p100player, p100unconfirmed, p100otheradmin, p100longname and the disabled throwaway member (all at example.invalid); a linked set; quests on those boards. The reused SuperAdmin qb-audit-platform is a Player of P100 Audit One-Shot. An account cannot be deleted through the app; remove boards through Platform > Group > Delete if wanted. Credentials stay in `~/qb-ui-review/phase-100/credentials.json` (mode 600) and are not recorded here.

## Phase UAT checklist

After deploy, for the operator:

- A real phone with touch over the whole area: the refusal announcements on Admin/Users, the Create New User dialog, the native confirms, the email links on Platform Users.
- A screen reader on the refusal reasons, the toasts, the Create New User dialog and the native confirms; on Group/Delete confirm the headings read as one h1 then three h2 sections.
- The Platform and Admin pages on the real boards (the sweep ran on fixtures only).

## Task Commits

1. Tasks 1 and 2 (sheet built and checked) were earlier steps of this plan with nothing committed in the repository.
2. Task 3 gap closure: `37fe6df0` fix(100-23): the Delete board section headings are h2.

## Verification

- Build: 0 errors.
- Unit: 1636 passed. Integration: 3038 passed, 17 skipped (the SQL-gated facts; 2 new facts here, both layouts of the heading fact).
- Re-capture: done (see above).

## Deviations from Plan

None to the plan's intent. The plan said nothing was pushed by this plan; the milestone branch is pushed after this plan at the orchestrator's instruction, which is the usual push after a green wave (no PR, tag or merge).

## Known Stubs

None.

## Threat Flags

None: a heading level changed, no endpoint, route or trust boundary did.

## Self-Check: PASSED

The partial, the test file, `gate2/answers.json` and `contrast-exceptions.json` exist and carry the changes; commit `37fe6df0` exists on the milestone branch.
