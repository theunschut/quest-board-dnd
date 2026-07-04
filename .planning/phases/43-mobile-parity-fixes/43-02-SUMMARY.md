---
phase: 43-mobile-parity-fixes
plan: 02
subsystem: ui
tags: [razor, css, mobile, quest-log]

# Dependency graph
requires: []
provides:
  - "Session Recap Available badge ported into the mobile Quest Log list view, matching desktop"
affects: [43-mobile-parity-fixes]

# Tech tracking
tech-stack:
  added: []
  patterns: [mobile-component ports desktop's already-shipped markup/CSS pixel-for-pixel rather than re-deriving visual values]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quest-log.mobile.css

key-decisions:
  - "Badge span uses d-block (not a new wrapper div) to force its own row, since mobile has no quest-log-card-footer flex-column parent like desktop does"
  - "New .quest-log-item .recap-badge CSS rule scoped one level shallower than desktop (no .quest-log-card-footer ancestor) and given its own !important color/text-shadow so it does not inherit the sibling faded meta-text rule"

patterns-established:
  - "When porting a desktop component to mobile, scope new CSS directly under the mobile component root (.quest-log-item) rather than reproducing desktop's nested wrapper class that doesn't exist on mobile"

requirements-completed: []  # MOBILE-02 not yet complete — Task 3 (real-device checkpoint) still pending

# Metrics
duration: n/a (partial — checkpoint pending)
completed: 2026-07-04
status: partial
---

# Phase 43 Plan 02: Mobile Recap Badge Summary (Partial — Task 3 Pending)

**Ported desktop's amber/gold "Session Recap Available" badge into the mobile Quest Log card (markup + CSS); real-device verification still outstanding.**

## Performance

- **Tasks:** 2 of 3 completed (Task 3 is a blocking real-device checkpoint, not yet run)
- **Files modified:** 2

## Accomplishments
- `Index.Mobile.cshtml` now renders a `<span class="recap-badge d-block">` row (guarded by `!string.IsNullOrWhiteSpace(quest.Recap)`) as the last element of the `.quest-log-item` card, directly after the DM-name row — verbatim port of desktop's guard/markup, no `quest-log-card-footer` wrapper.
- `quest-log.mobile.css` gained a new `.quest-log-item .recap-badge` rule carrying desktop's exact amber/gold pill values (`rgba(255, 193, 7, 0.2)` background, `rgba(255, 193, 7, 0.4)` border, `12px` radius, full-opacity `#F4E4BC` text) — scoped so it does not inherit the sibling `.quest-log-item small, .quest-log-item .text-muted` faded-parchment rule.
- `dotnet build` succeeds (Razor view compiles cleanly, 0 warnings / 0 errors).

## Task Commits

Each task was committed atomically:

1. **Task 1: Port the recap-badge markup into the mobile Quest Log card** - `06c4345` (feat)
2. **Task 2: Add the amber/gold .recap-badge CSS to quest-log.mobile.css** - `7bda4d4` (feat)

Task 3 (real-device checkpoint) has not been executed — see below.

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` — added conditional recap-badge row after the DM-name `<small>`, before the card's closing `</div>`
- `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` — added `.quest-log-item .recap-badge` rule after the existing `.quest-log-item .badge` rule

## Decisions Made
- Used `d-block` on the badge `<span>` instead of introducing a new wrapper div, since mobile's card has no `.quest-log-card-footer` flex-column ancestor to force block placement the way desktop does.
- Scoped the new CSS rule directly under `.quest-log-item` (dropping desktop's `.quest-log-card-footer` ancestor, which doesn't exist on mobile) and gave it its own `!important` color/text-shadow declarations so it renders full-opacity parchment text rather than inheriting the sibling rule's faded `rgba(244, 228, 188, 0.7)` variant.

## Deviations from Plan

None — plan executed exactly as written for Tasks 1-2.

## Issues Encountered

None for Tasks 1-2. Both automated `grep` verification commands and `dotnet build` passed on first attempt.

## User Setup Required

None for the code changes. Task 3's real-device verification requires temporary manual setup (dev server bound to `0.0.0.0:8000`, a temporary Windows Firewall rule, and a physical iPhone on the same LAN) — this setup is verification-time only, not a persistent environment requirement, and is not yet performed.

## Next Phase Readiness — BLOCKED on Task 3

**This plan is incomplete.** Task 3 is a blocking `checkpoint:human-verify` requiring a physical iPhone over LAN — it cannot be automated or fabricated. Execution halted here per the checkpoint protocol.

### Checkpoint state

**Completed tasks:**

| Task | Status | Commit |
|------|--------|--------|
| Task 1: Port recap-badge markup into mobile Quest Log card | done | `06c4345` |
| Task 2: Add amber/gold `.recap-badge` CSS to `quest-log.mobile.css` | done | `7bda4d4` |
| Task 3: Real-device verification of the mobile recap badge | **awaiting human verification** | — |

**What's built:** The mobile Quest Log card now renders a "Session Recap Available" amber/gold pill badge (`fa-scroll` icon) as its own row below the DM name, whenever a completed quest has a non-empty recap — matching desktop's existing badge. Markup was ported into `Index.Mobile.cshtml`; matching `.recap-badge` CSS was added to `quest-log.mobile.css`.

**Why a real device is required:** Because markup + CSS must render together correctly, and because this codebase has previously shipped mobile bugs that only appeared on real devices (see `.planning/research/PITFALLS.md` Pitfall 5), sign-off requires confirming the rendered result on a real mobile browser session — devtools "iPhone" emulation is not sufficient.

**How to verify (steps for the human/orchestrator):**
1. Ensure a completed quest with a non-empty `Recap` exists (and ideally one without, for the negative case). Add one via the DM quest-edit flow if none exists.
2. Start the dev server bound to all interfaces: `dotnet run --project QuestBoard.Service --urls "http://0.0.0.0:8000"` (do not commit any config change). Add a temporary Windows Firewall inbound rule for TCP 8000; find the dev PC's LAN IP via `ipconfig`; confirm the iPhone is on the same Wi-Fi.
3. On the physical iPhone in Safari, browse to `http://<dev-PC-LAN-IP>:8000` and navigate to the Quest Log (completed quests) list view.
4. On a quest WITH a recap: expect an amber/gold pill reading "Session Recap Available" with a scroll icon, on its own row directly below the DM-name row, as the last thing in that card — matching desktop's amber pill (translucent gold background, gold border, parchment text), not the plain grey Bootstrap CR badge.
5. On a quest WITHOUT a recap: expect no badge and no empty gap/placeholder.
6. Cross-check against desktop's Quest Log: color, shape, icon, and label should match.
7. Record the exact iPhone model + iOS/Safari version in the verification evidence (per PITFALLS.md Pitfall 5). Remove the temporary firewall rule afterward.

**Resume signal:** Type "approved" (with the recorded device model + iOS version), or describe what renders wrong (wrong color/shape, wrong placement, badge missing on a recap quest, or badge showing on a no-recap quest).

**Blockers:** Requires a physical iPhone on the same LAN as the dev machine — cannot be executed by this agent. `requirements-completed` in this summary's frontmatter is deliberately left empty; MOBILE-02 is not complete until Task 3 is approved.

---
*Phase: 43-mobile-parity-fixes*
*Partial completion recorded: 2026-07-04*
