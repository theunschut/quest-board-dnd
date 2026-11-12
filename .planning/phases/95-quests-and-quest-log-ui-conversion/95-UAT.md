---
status: complete
phase: 95-quests-and-quest-log-ui-conversion
source: [95-VERIFICATION.md, 95-18-SUMMARY.md, WINDOWS.md entries 3 and 32]
started: 2026-11-12T10:05:00Z
updated: 2026-11-12T10:50:00Z
---

## Current Test

[testing complete]

## Tests

<!-- Run by Claude for the user on 2026-11-12: local app on localhost:8000 (milestone/v9-rolling-improvements after the
     phase 102 close-out), local SQL Server, Playwright Chromium with the desktop profile (1440 and 320) and phone profiles
     (Pixel 7 and iPhone 13 User-Agents with touch, 390 and 412). Evidence under ~/qb-ui-review/phase-95/after/uat-2026-11-12/
     (full phase sweep: 162 records, all HTTP 200) and after/uat-2026-11-12-foreign/. By the user's decision, checks that need
     a real phone or a real calendar client are deferred until after the production deploy. -->

### 1. Quest board (/quests) serves the phone layout
expected: Under a phone User-Agent /quests uses the .Mobile layout, nothing scrolls sideways at 390, every control is at least 44px.
result: pass
evidence: Sweep rows board-crowded, board-dm, board-campaign, board-empty at 390 and 412 - mobile layout, no overflow, 0 targets under 44px.

### 2. Voting Quest Details on a phone
expected: The open quest with date voting serves the phone layout with the vote UI, no sideways scroll, 44px targets.
result: pass
evidence: Sweep rows details-open, details-voting-checked (three votes ticked, assert passes), details-dm, details-anon, details-signedup-update and the three finalized views - mobile layout, no overflow, 0 small targets.

### 3. Spectator hides only the voting, and switching back restores it
expected: Choosing Spectator hides only the [data-vote-section] wrapper and shows the spectator sentence; DM Controls never hide; switching back restores the section and the original instruction. Desktop and phone.
result: pass
evidence: Sweep rows details-spectator and details-spectator-back - 8 records (both User-Agents, every width), 32 asserts evaluated, 0 failures.

### 4. Character modal on a phone
expected: The change-character modal opens on a phone, fits the 390 viewport, scrolls inside, and its controls are at least 44px.
result: pass
evidence: Pixel 7 at 390 on the finalized quest as the selected player - dialog 374px wide, scrollable body, no page overflow, Remove Character, Cancel, Save, the select and the close button all 44px high, no console errors (~/qb-ui-review/phase-95/tools/uat95-modal.mjs).

### 5. Tab and Shift+Tab leave the Markdown editor
expected: On every editor host focus leaves the editor with Tab and Shift+Tab, and the editing surface is named and described.
result: pass
evidence: Quest Create, Create errors, Edit, Edit finalized, CreateFollowUp (3 states) and EditRecap - 36 of 36 records leave, named and described. Foreign hosts Characters Create, Events Create, Shop Create and DM EditProfile - 16 of 16 leave. Contacts Create probed directly: Tab and Shift+Tab both leave, on desktop and phone.

### 6. Manage Description and Rewards disclosures on a phone (WINDOWS #32, phone User-Agent part)
expected: On a quest with Rewards, Manage's Rewards disclosure expands, announces its state and keeps 44px targets.
result: pass
evidence: |
  Throwaway quest "UAT Rewards Disclosure Probe Quest" (Audit Board) with Rewards, Manage under Pixel 7 and iPhone 13 at
  390: Description and Rewards buttons 324x44, aria-expanded false -> true on open (content shown) -> false on close
  (content hidden), no sideways scroll. The quest was deleted afterwards through Manage's Delete Quest (Manage then 404).
  Screenshots: after/uat-2026-11-12/rewards-*.png. The same check on a real phone is item 12 (deferred).

### 7. Quest Log and a log entry on a phone
expected: /QuestLog, an entry with a recap, one without, and EditRecap serve the phone layout without sideways scroll.
result: pass
evidence: Sweep rows log-index, log-empty, log-details-recap, log-details-norecap, log-editrecap - mobile layout, no overflow, 0 small targets.

### 8. Move as SuperAdmin on a phone
expected: Move Quest and Move Event serve the phone layout and reach the move preview.
result: pass
evidence: |
  Sweep rows move-quest and move-event, both User-Agents: preview reached (?targetBoardId=10#move-preview), mobile layout
  on the phone profile, no overflow, 0 small targets. A first pass landed on the board picker only because the phase 102
  run had moved the super admin test session onto the deleted UAT board; re-run after a fresh login.

### 9. Manage module actions
expected: Remove player, Open Quest and the player limit run from modules with their confirm prompts; nothing reaches the server unless accepted.
result: pass
evidence: |
  manage-probe.mjs and manage-probe2.mjs re-run on both User-Agents with every mutating request intercepted: the
  remove-player confirm names the player, dismissing sends no DELETE, accepting sends one DELETE with the antiforgery
  token (aborted); the Open Quest confirm text is exact, dismiss sends no POST, accept sends one (aborted); the player-limit
  alert fires. Delete Quest ran for real on the throwaway quest in item 6 (confirm, then Manage 404).

### 10. Proposed-date rows on Quest Create
expected: Rows add and remove, renumber after a removal, and at least one date stays required.
result: pass
evidence: Desktop and Pixel 7: three rows added, removing the middle one renumbers to ProposedDates[0..1] with labels "Remove proposed date 1/2"; removing all leaves one row; an incomplete submit sends no POST (intercepted).

### 11. Controls and sideways scroll across the phase on a phone profile
expected: Every phase page on the phone profile is the .Mobile layout, nothing scrolls sideways, and controls are at least 44px.
result: pass
evidence: |
  82 of 82 phone records use the mobile layout, 0 of 162 records overflow. The only targets under 44px are the editor's
  text-area proxy (24px high) on the Create, Edit and FollowUp forms - the approved exception recorded in 95-VERIFICATION.
  The only console errors are /Characters/GetCroppedPicture/3013 404 on finalized and Manage pages - WINDOWS #31, accepted
  for this release and owned by the characters phase.

### 12. The phone checklist on a real phone (95-18 checklist, WINDOWS #32 on a device)
expected: On a real phone - /quests, a voting Quest Details page, Spectator and back, the character modal, Quest Create tabbing through the editor, Manage with Description and Rewards expanded, Quest Log and a log entry, Move as SuperAdmin - all serve the phone layout with 44px controls, no sideways scroll at 390, and disclosures announce their state.
result: skipped
reason: "Deferred follow-up: real-device check deferred by the user until after the production deploy (\"Let's skip the phone tests until it's deployed on production. Easier that way. We can always run a hotfix phase later.\"). Every item was checked under phone User-Agents with touch locally (items 1-11); only real-device behaviour remains."

### 13. Calendar subscription on a real device (WINDOWS #3)
expected: A real phone subscribes via iOS Calendar, Google Calendar or Outlook; calendar naming, refresh latency, stale-entry check and camera QR scan behave.
result: skipped
reason: "Deferred follow-up: needs a real phone and calendar client; deferred by the user until after the production deploy (same decision as item 12). Phase 84 ledger entry, carried in this UAT at the phase 95 close-out."

## Summary

total: 13
passed: 11
issues: 0
pending: 0
skipped: 2
blocked: 0

## Gaps

[none]

## Deferred Follow-Ups

- test: 12
  idea: "Real-phone run of the phase 95 phone checklist, including WINDOWS #32 (Rewards disclosure) - after the production deploy"
  deferred_at: 2026-11-12
- test: 13
  idea: "Real-device calendar subscription check (WINDOWS #3) - after the production deploy"
  deferred_at: 2026-11-12
