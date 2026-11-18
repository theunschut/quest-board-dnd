---
phase: 100-admin-and-platform-ui-conversion
plan: 12
subsystem: ui
tags: [gate-1, operator-approval, wording, admin, platform, html]
requires:
  - phase: 100-11
    provides: Gate 1 package A mocks (v1, 36 frames) and the look-check
  - phase: 100-05
    provides: audit answers and the approved refusal and toast sentences
provides:
  - Gate 1 package B mocks (look check per page type, proposals) and the assembled local sheet
  - Operator-approved Gate 1 answers (items 1 to 20) in gate1/answers.json
affects: [100-13, 100-14, 100-15, 100-16, 100-17, 100-18, 100-19, 100-20, 100-21, 100-22]
tech-stack:
  added: []
  patterns: [one self-contained local gate sheet with an answer builder, answers recorded verbatim with source per item]
key-files:
  created:
    - ~/qb-ui-review/phase-100/gate1/index.html
    - ~/qb-ui-review/phase-100/gate1/README.md
    - ~/qb-ui-review/phase-100/gate1/items.md
    - ~/qb-ui-review/phase-100/gate1/img/
    - ~/qb-ui-review/phase-100/gate1/answers.json
  modified: []
key-decisions:
  - "Wording: One-Shot (6a), Dungeon Master (6b), Admin instead of Administrator on Admin/Users (6c) - all yes"
  - "Copy changes applied: Remove from Group sentence (8), password hint (9), Refresh renamed Refresh Email Stats (20); Cancel is relabelled on Reset Password only (19:reset), the modal keeps Cancel"
  - "Layout and look approved: one title in the card header on Create/Edit User (2), Members stacked (3), email under the name on Admin/Users with header Name (4), refused-control look (5), Group/Index actions left-aligned in fixed slots (16), Done and Closed badge outlined in phone slips (18)"
patterns-established:
  - "Gate answers carry source operator, default or audit-checkpoint per item so a later plan can tell an answer from a default"
requirements-completed: [D-09, D-13, D-14, D-15, D-16, D-18, D-19, DoC-4, DoC-6]
duration: ~4h
completed: 2026-11-18
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 1
---

# Phase 100 Plan 12: Gate 1 answer Summary

**The operator approved every Gate 1 drawing (version v1, no redraw) and accepted all recommendations: One-Shot, Dungeon Master and Admin wording, the four copy changes except the modal Cancel, left-aligned Group/Index slots, and the relayouts of Create/Edit User, Members and Admin/Users.**

## Operator answer (verbatim)

The operator viewed `~/qb-ui-review/phase-100/gate1/index.html` and replied in chat: "looks good, go with recommendations".

That equals the sheet's recommended line, which is recorded as the answer:

`approved 2:ok 3:ok 4:ok header:Name 5:ok 6a:yes 6b:yes 6c:yes 8:yes 9:yes 16:left 18:yes 19:reset 20:yes`

Answer source: operator (answerSource "operator in chat: looks good, go with recommendations"). The machine-readable record is `~/qb-ui-review/phase-100/gate1/answers.json` (answerLine, operatorWords, every key the later plans check). All seven no-default items (2, 3, 4, 5, 6a, 6b, 6c) are answered by name inside that line.

## Gate 1 answers

| # | Item | Answer | Source |
|---|------|--------|--------|
| 1 | Look check on the page types | approve (as drawn, three intended differences) | operator |
| 2 | One page title in the card header on Create User and Edit User | ok | operator |
| 3 | Members sections stacked full width; Current Members heading on the phone | ok | operator |
| 4 | Admin/Users email under the name, five buttons in the freed Actions cell; header word | ok; header Name | operator |
| 5 | Refused-control look (aria-disabled control, reason line, lock icon) | ok | operator |
| 6a | One-Shot in the board-type selects | yes | operator |
| 6b | Dungeon Master on Members and in the role selects | yes | operator |
| 6c | Admin on Admin/Users (instead of Administrator) | yes (the badge word is "Admin") | operator |
| 7 | Result toasts for Remove from Group and Delete Quest | settled at the audit checkpoint, built in 100-08 | audit-checkpoint |
| 8 | Remove from Group confirm sentence names the deleted availability answers | yes | operator |
| 9 | ResetPassword length rule and hint aligned to the server policy | yes | operator |
| 10 | Create New User dialog after a validation error | settled by O-12 (Error toast after a redirect) | audit-checkpoint |
| 11 | Buttons by role as drawn | ok | operator |
| 12 | Required markers on the five forms and the modal | ok | operator |
| 13 | Accessible names and 20px phone h1 titles | ok | operator |
| 14 | Error toast instead of the bare 429 page | settled by O-4, built in 100-07 | audit-checkpoint |
| 15 | EmailStats tiles | ok | operator |
| 16 | Group/Index actions | left (left-aligned fixed slots) | operator |
| 17 | New decoration (icons) | ok | operator |
| 18 | Done and Closed badge outlined inside phone slips | yes | operator |
| 19 | Cancel labels | reset: only Cancel on Reset Password becomes Back to Users; the modal Cancel stays | operator |
| 20 | Refresh renamed Refresh Email Stats | yes | operator |

No approved sentence was changed (`sentenceChanges` is empty).

## Drawings approved

All at version v1 under `~/qb-ui-review/phase-100/mockups/v1/` (13 look-check entries pass), assembled into `~/qb-ui-review/phase-100/gate1/index.html` (111 JPEGs in `img/`, hygiene ok).

| Drawing | Path | Version |
|---------|------|---------|
| admin-users | mockups/v1/admin-users/ | v1 |
| group-index | mockups/v1/group-index/ | v1 |
| create-user, edit-user (forms) | mockups/v1/create-user/, mockups/v1/edit-user/ | v1 |
| group-create, group-delete | mockups/v1/group-create/, mockups/v1/group-delete/ | v1 |
| members | mockups/v1/members/ | v1 |
| members-modal | mockups/v1/members-modal/ | v1 |
| refusals | mockups/v1/refusals/ | v1 |
| quests-status | mockups/v1/quests-status/ | v1 |
| emailstats | mockups/v1/emailstats/ | v1 |
| platform-users, linked-boards | mockups/v1/platform-users/, mockups/v1/linked-boards/ | v1 |
| proposals | mockups/v1/proposals/ | v1 |

## Planner assumptions

UI-SPEC planner assumptions 1 to 10 were listed on the sheet and the operator approved the sheet without objection to any, so they stand as written: hook names survive; controllers keep their routes (only DeleteUser and DeleteQuest verbs changed, already done in 100-08); one partial per badge kind; old unlayered rules deleted in the same commit that moves a consumer to the layered sheet; the required guard tests; p100 fixtures through the app's own forms; real-data BEFOREs stay local; no planning IDs in source; server work first (done in 100-06 to 100-10); both gate packages stay local. Source: default (the sheet asked no separate question).

## Redraw history

None. v1 was approved at the first presentation.

## What later plans implement

- 100-13: the shared status and badge mapping with the words One-Shot, Dungeon Master, Admin; the Done and Closed badge outlined inside phone slips (18).
- 100-14: Admin/Users relayout (email under the name, header Name, five buttons in the freed Actions cell), refused-control look, Remove from Group sentence (8), Remove disabled on own row and the only Admin.
- 100-15: one page title on Create/Edit User, ResetPassword password hint (9), Reset Password cancel relabelled (19:reset).
- 100-16: Group/Index actions left-aligned in fixed slots.
- 100-17, 100-18: One-Shot in the board-type selects; refusal look where used.
- 100-19: Members stacked, Current Members heading on the phone, modal keeps Cancel.
- 100-20: EmailStats tiles, Refresh renamed Refresh Email Stats.

## Deviations from Plan

None - plan executed exactly as written. The operator's single reply ("looks good, go with recommendations") was taken, at the caller's direction, as the sheet's recommended line, which names all seven no-default items.

## Known Stubs

None.

## Threat Flags

None. No repository source file changed; the package stayed local.

## Self-Check: PASSED

- gate1/answers.json present, key check prints ok, gate-hygiene.py ok.
