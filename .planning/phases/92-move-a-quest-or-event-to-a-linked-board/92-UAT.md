---
status: diagnosed
phase: 92-move-a-quest-or-event-to-a-linked-board
source: [92-VERIFICATION.md]
started: 2026-10-19T00:00:00Z
updated: 2026-10-19T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Phone-width layout of the Move page
expected: At 390px, for a quest and an event with a long target board name and a long title, the heading, target select and confirm button wrap with no horizontal scroll or clipped text; the .Mobile twin renders the cards.
result: pass
notes: Run in the in-app browser against the local app (mobile UA at 390x844). Quest 991001 (108-char title) and event 1 (112-char title), both Lupor -> The Boundless Domain. `.move-card-mobile` and move-entry.mobile.css served; document scrollWidth 390; both buttons inside the viewport (right edge 357); only overflow is the hidden off-canvas nav. Desktop (800px pane) also checked: long title wraps in the side card, single target preselected.

### 2. Toast and link after a move
expected: Moving a quest as a super admin who is a member of the target board redirects to the source Quest Board with the Moved toast and a working "Open it on {Board}" link; as a non-member super admin the toast shows plain text with no link.
result: pass
notes: Member (Lupor -> Boundless): redirect to /quests, green "Moved" toast with autohide off, dark "Open it on The Boundless Domain" button; the link opened the quest's Details. Quest left the source list; stored GroupId 3, FeedRevision 1 -> 2; audit log "Moved quest 991001 from board 4 to board 3 by user 1: kept 0, dropped 0, promoted 0, links cut 0". Non-member (Euphoria Inn -> The Zeroes, quest 991002): plain "Success" toast, no link. Event move also checked: lands on /Events with "1 answer was removed" and the link; GroupId 3, FeedRevision 2, date/time unchanged, 5 of 6 answers kept; log "kept 5, dropped 1, added 0".

### 3. Quest Moved and Waitlist Promoted email rendering
expected: In Email Preview, the Quest Moved email (player, Dungeon Master and removed audiences) and the Waitlist Promoted moved line render with a very long quest title without overflow or broken layout at a real mail-client width.
result: pass
notes: All three Quest Moved variants and WaitlistPromotedAfterMove render. The sample title was swapped in the DOM for a 104-char title: it wraps inside the 600px column, no element exceeds it, on all three variants. Removed variant has no button, as designed. The emails lay out at a fixed 600px like the existing templates. The empty gap above the details table and the broken "Wax Seal" image also appear on the unchanged Waitlist Promoted preview, so they are not from this phase. Not checked in a real mail client.

### 4. Double press on Confirm (review WR-01)
expected: Pressing Confirm twice quickly on a slow save does not end on a bare 404; the move itself is done after the first press.
result: issue
reported: "A real double-click on Confirm produced one move and landed on the Moved toast (the browser dropped the second click once navigation began), so the slow-save race did not reproduce. But pressing Back to the Move page after a successful move shows a blank page: GET /Move/Quest/991001 returns a bare 404 because the quest has left the active board."
severity: minor

## Summary

total: 4
passed: 3
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "After a successful move, revisiting or resubmitting the Move page does not end on a bare 404"
  status: failed
  reason: "Browser Back to /Move/Quest/{id} after a move returns a bare 404 (blank page) because the entry is no longer on the active board; same root cause as review WR-01 on the POST side"
  severity: minor
  test: 4
  artifacts: [QuestBoard.Service/Controllers/MoveController.cs]
  missing: ["GET and POST Move actions redirect with an info toast when the entry is no longer on the active board", "confirm button disabled on submit"]
