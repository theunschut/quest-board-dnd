---
phase: 100-admin-and-platform-ui-conversion
plan: 05
subsystem: server-rules-audit-checkpoint
tags: [security, audit, operator-checkpoint, answers]
requires: ["100-04"]
provides:
  - "audit-answers.json: the operator's decision per gap (G-01 to G-23), finding (X-1 to X-3) and question (O-1 to O-16), plus 15 approved sentences; read by 100-06 to 100-23"
  - "100-SECURITY.md Operator answers section, approved-sentence table, plan-per-rule table and a status on every gap row"
  - "deferred-items.md updated for the answers that defer or accept (O-8 and G-12, G-16, O-7, X-3 note)"
  - "~/qb-ui-review/phase-100/audit/checkpoint.md and checkpoint.html (the one-sitting brief) and answers-template.json"
affects: [100-06, 100-07, 100-08, 100-09, 100-10, 100-19, 100-20]
key-files:
  created:
    - .planning/phases/100-admin-and-platform-ui-conversion/audit-answers.json
  modified:
    - .planning/phases/100-admin-and-platform-ui-conversion/100-SECURITY.md
    - .planning/phases/100-admin-and-platform-ui-conversion/deferred-items.md
decisions:
  - "O-1 accept: the reach rule is exactly D-02; an ordinary account that also sits on another board stays reachable (G-02 residual)"
  - "O-3: a peer Admin of the same board, the actor's own name and email, and the actor's own password reset are all reachable and allowed"
  - "O-4 toast: the limiter runs before any write and a refusal is a redirect with an Error toast, so the existing 429 tests change"
  - "O-12 toast: a failed Platform Create New User redirects with an Error toast like Add Member, which also answers Gate 1 item 10"
  - "O-13 dev-setting: the EmailStats capture seam is an EmailSettings:ResendBaseUrl read honoured only in Development"
metrics:
  duration: "operator checkpoint plus recording"
  completed: 2026-11-18
status: complete
actuals:
  tokens: 5500
  tasks: 3
  commits: 1
---

# Phase 100 Plan 05: Audit checkpoint Summary

The operator answered the whole audit in one sentence, "approve all recommendations", against the one-sitting brief; every gap, finding, question and sentence is now recorded in `audit-answers.json` and the security document before any server fix is made. No repository source, test, view or configuration file changed.

## What was done

- Task 1 (outside the repository, no commit): wrote the brief `~/qb-ui-review/phase-100/audit/checkpoint.md` (and its HTML form `checkpoint.html`) and `answers-template.json`, fixture names only.
- Task 2 (checkpoint): the operator answered in chat on 2026-11-18 with "approve all recommendations", shown against `checkpoint.html`, whose default resume line is every recommendation. Recorded with the source "operator in chat: approve all recommendations" (`answerSource`; the `source` key holds `operator`, as the validation requires).
- Task 3: recorded the answers in `audit-answers.json`, filled the security document and the deferred items, and made the docs commit with STATE.md and ROADMAP.md.

## Operator answers

G-01 and G-03 were decided before the checkpoint (D-02, D-03) and not re-asked; G-20 to G-22 are not gaps. Gate 1 item 6c (the role word) was not answered here: `wordingEarly.adminWord` is null, every sentence is role-free, and item 6c stays a Gate 1 question.

| Gap or question | Answer |
|---|---|
| G-02 | residual (elevated part closed by D-02; ordinary account on another board stays reachable) |
| G-04, G-05, G-06, G-07, G-08, G-09, G-13, G-14, G-15, G-18, G-19, G-23 | approve |
| G-10, G-11, G-12, G-16, G-17 | residual (accepted as designed; G-12 deferred; G-17 stays a GET) |
| X-1, X-2, X-3 | approve (recorded under `findings`) |
| O-1 | accept |
| O-2 | exempt |
| O-3 | peerAdmin reachable, selfEdit reachable, selfReset allowed |
| O-4 | toast |
| O-5 | limit 20 per 60 minutes |
| O-6 | get |
| O-7 | accept |
| O-8 | defer |
| O-9 | generic |
| O-10 | removeSentence gate1, passwordHint gate1 |
| O-11 | yes |
| O-12 | toast |
| O-13 | dev-setting |
| O-14 | dm (action demotes to Dungeon Master) |
| O-15 | yes (existing "Password reset successfully for {name}!" as a Success toast) |
| O-16 | yes (fix the dialog binding together with G-15) |
| Sentences | all 15 approved verbatim, role-free, including `toastRateLimit` (O-4 toast), `toastCreateLimit` (O-5 limit), `toastCreateMemberFailed` (O-12 toast) and `toastPlatformNonMember` (G-13 approve) |

The approved sentence texts are in `audit-answers.json` under `sentences` and in the table in `100-SECURITY.md`.

## What each fix plan builds

| Plan | Builds |
|---|---|
| 100-06 | The D-02 reach rule: EditUser and ResetPassword (GET and POST) and the service overload refuse a SuperAdmin or other-board Admin target for a non-SuperAdmin actor; peer Admin, own name and email, and own reset stay reachable (O-3); add-by-email follows O-1 accept; Resend Welcome Email takes the same rule (G-07); `reasonReach` and `toastReach` generic (O-9). Also the ResetPassword success toast (X-2, O-15) |
| 100-07 | The limiter before any write (G-06, G-08); a refusal is a redirect with the `toastRateLimit` Error toast, and the 429 tests change (O-4); a per-actor Create User limit of 20 per 60 minutes with `toastCreateLimit` (G-09, O-5); the `ResendBaseUrl` Development-only setting (O-13) |
| 100-08 | Delete Quest and Delete User become POST forms with toasts (`toastRemoved`, `toastQuestDeleted`, `toastQuestMissing`, `toastQuestRefused`); quest delete removes reminder rows and unlinks an other-board follow-up (G-05, O-11) |
| 100-09 | The role half of D-03: the four role buttons refuse self change and the last-Admin demotion, judged on the resulting role; `DemoteFromAdmin` sets Dungeon Master (X-1, O-14); `toastSelf`, `toastLastAdmin`, `reasonSelf`, `reasonLastAdmin` |
| 100-10 | Remove from Group refuses self and last-Admin removal; stale target answers `toastStale` (G-04, G-23); Platform Remove Member says Warning `toastPlatformNonMember` for a non-member (G-13); the Platform removal stays unguarded (G-14, O-2 exempt) |
| 100-19 | Platform Create New User: the dialog binding is fixed and a failure redirects with the `toastCreateMemberFailed` Error toast (G-15, X-3, O-12, O-16) |
| 100-20 | Email Stats Refresh stays a GET (G-17 residual, O-6 get) |
| Gate 1, 100-14, 100-15 | G-18 (Remove from Group sentence, item 8) and G-19 (reset password hint, 8 characters, item 9) as open Gate 1 items; item 6c still asked |
| Not fixed | G-02 (remainder), G-10, G-11, G-12 (deferred to the email phase), G-16; O-7 race accepted |

## Deviations from Plan

None in the work itself. Two recording notes:

- The plan's validation requires `source` to equal `operator`, so the operator's phrasing is recorded in the added keys `answerSource` and `answerLine`; `source` is `operator`.
- Residual `words` for G-02, G-10, G-11, G-12, G-16 and G-17 are the brief's recommendation text, accepted by the operator's blanket phrase; the operator gave no separate wording.

## For 100-06 and later

- The plan texts of 100-06 to 100-10 read gap and question keys only; X-1 to X-3 and O-14 to O-16 live under `findings` and `extraQuestions` in `audit-answers.json` and are not named in those plans. X-2 (success toast key) rides with the ResetPassword POST in 100-06, X-1 with the role actions in 100-09, X-3 with G-15 in 100-19.
- The extra answers sit in `extraQuestions`, not `questions`, as the template set up.
- `wordingEarly.adminWord` is null, so no sentence may name the role word until Gate 1 item 6c is answered.

## Known Stubs

None.

## Threat Flags

None. The threats T-100-18, T-100-81 and T-100-19 are mitigated: answers recorded with source `operator`, nothing defaulted by the executor, every residual written into `100-SECURITY.md` with the operator's words, G-01 and G-03 decided rather than open.

## Self-Check: PASSED
