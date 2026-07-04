# Phase 43: Mobile Parity Fixes - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Two independent, isolated bug fixes that restore mobile/desktop parity — no new capabilities, no shared code between the two fixes:

1. **iOS Safari fixed-background bug (#116):** `background-attachment: fixed` on `body` in both `site.css` and `mobile.css` scrolls the background image with the page content on iOS Safari, instead of keeping it visually fixed. This is a known WebKit-iOS compositing limitation — desktop Chrome/Safari and devtools "mobile" emulation do NOT reproduce it, so it can only be verified on a real iOS Safari session.
2. **Missing Session Recap badge on mobile Quest Log (#115):** Desktop's `Views/QuestLog/Index.cshtml` shows a `recap-badge` pill ("Session Recap Available") on any completed quest with a non-empty `Recap` field. `Views/QuestLog/Index.Mobile.cshtml` has no equivalent markup at all — the badge needs to be added to the mobile list-item card.

Both fixes are CSS/markup-only — no controller, ViewModel, or database changes for either.

</domain>

<decisions>
## Implementation Decisions

### iOS Safari Background Fix (locked by prior project research)
- **D-01:** Replace `background-attachment: fixed` in **both** `site.css` (line 382) and `mobile.css` (line 21) with a `position: fixed` pseudo-element/layered-div approach (`::before` or dedicated `<div>`, sized `100vh`/`100dvh`, `z-index: -1`) — the standard fix for this WebKit-iOS compositing bug. No JS scroll-listener workaround. Full rationale and sourcing: `.planning/research/SUMMARY.md` and `.planning/research/PITFALLS.md` (Pitfall 5).
- **D-02:** `site.css`'s copy of the bug is not reachable by any real iOS Safari session in this codebase today — `MobileDetectionMiddleware` routes `iPhone` AND `iPad` user agents to `mobile.css` (see Code Context below), so only `mobile.css`'s rule is user-facing on iOS. Fix both files anyway for consistency (both currently carry the identical broken rule, and there's no cost to fixing the unreachable copy too) — this is Claude's discretion, not something requiring a user decision.

### Real-Device Verification Access
- **D-03:** User has a physical iPhone available for verification — no real-device-cloud service (BrowserStack, etc.) needed.
- **D-04:** Verification will happen over the local Wi-Fi network (phone and dev PC on the same LAN), not a tunnel or the production host. This requires, at verification time (not a permanent code/config change):
  - Running the dev server bound to all interfaces rather than `localhost` only — e.g. `dotnet run --urls "http://0.0.0.0:8000"` — rather than editing the committed `launchSettings.json` (which defaults to `https://localhost:8001;http://localhost:8000` and is shared by all devs).
  - A Windows Firewall inbound rule allowing that port.
  - Accessing via `http://<dev-PC's-LAN-IP>:8000` from the iPhone (plain `http://`, not `https://` — the phone won't trust the machine's local ASP.NET dev cert, and there's no sensitive data in play for a CSS-only verification pass).
  - Recording the exact device/iOS version/method in the phase's verification evidence, per PITFALLS.md's standing recommendation (a generic "tested on mobile" note is not sufficient given this bug already escaped desktop/emulation testing once in this codebase).

### Recap Badge Visual Style
- **D-05:** Mobile's new recap badge matches desktop's fantasy styling exactly — same visual treatment as the `.recap-badge` class in `quests.css` (amber/gold background `rgba(255, 193, 7, 0.2)`, matching border `rgba(255, 193, 7, 0.4)`, `fa-scroll` icon, pill shape) — not mobile's plainer Bootstrap-badge look (the style currently used for the adjacent CR badge). This needs new CSS added to `quest-log.mobile.css`, since no equivalent class exists there today.
- **D-06 (Claude's discretion):** Keep the full label text "Session Recap Available" on mobile (not a shortened variant) — the mobile `.quest-log-item` card is full-viewport-width (minus small padding), with plenty of room for the phrase + icon on its own row; no truncation risk like a narrow-tile layout would have.

### Recap Badge Placement
- **D-07:** Badge renders as its own row at the bottom of the mobile card, below the DM name row — mirrors desktop's card-footer placement (recap badge is the last element shown before "click to view details"). Current mobile card row order: title + CR badge → completed date → DM name → **[new] recap badge row**.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-Level Research (pre-dates roadmap, directly covers this phase)
- `.planning/research/SUMMARY.md` §"Phase 5: iOS Safari background-attachment fixed fix (#116)" — locked technical approach, confidence assessment, sourcing
- `.planning/research/PITFALLS.md` §"Pitfall 5: iOS-Safari-Specific CSS/Behavior Bugs Are Declared 'Fixed' After Only Desktop and Devtools-Emulation Verification" — full pitfall detail, warning signs, and the standing "name the exact device/method" verification requirement
- `.planning/research/STACK.md` — confirms exact `background-attachment: fixed` line locations in `site.css`/`mobile.css` and the real-device verification tooling note

### Roadmap / Requirements
- `.planning/ROADMAP.md` §"Phase 43: Mobile Parity Fixes" — phase goal, success criteria, requirements mapping
- `.planning/REQUIREMENTS.md` §"Mobile Bugs" — MOBILE-01, MOBILE-02 full text

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/wwwroot/css/quests.css:891-899` — existing `.quest-log-card-footer .recap-badge` definition (desktop) — exact colors/spacing to port into `quest-log.mobile.css` for D-05.
- `QuestBoard.Service/Views/QuestLog/Index.cshtml:72-77` — desktop's recap-badge markup (`<span class="recap-badge"><i class="fas fa-scroll me-1"></i>Session Recap Available</span>`) — copy this markup pattern into `Index.Mobile.cshtml`, wrapped in the same `!string.IsNullOrWhiteSpace(quest.Recap)` guard.

### Established Patterns
- `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs` — `MobileKeywords` includes both `iPhone` and `iPad`, so iPad already routes to `.Mobile.cshtml`/`mobile.css` today. This resolves the "does the fix need to cover iPad-via-desktop-layout" question raised in PITFALLS.md — it doesn't, because iPad never reaches `site.css` in this codebase.
- No existing CSS in either `site.css` or `mobile.css` uses a negative `z-index` — a `z-index: -1` background layer for D-01 won't collide with any existing stacking context (existing z-index values found: 10, 297, 499, 1025, 1030, 1493, 1645 in `site.css`, all positive).
- `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` — existing `.quest-log-item` card conventions (glass-card look: `rgba(255,255,255,0.15)` bg, `backdrop-filter: blur(15px)`, parchment text color `#F4E4BC` with dark text-shadow) — new badge styling should coexist with, not replace, these.

### Integration Points
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` — badge row added inside the existing `.quest-log-item` foreach block, after the DM-name `<small>` element.
- `QuestBoard.Service/wwwroot/css/quest-log.mobile.css` — new recap-badge styles added here (mirroring `quests.css:891-899`).
- `QuestBoard.Service/wwwroot/css/site.css:377-383` and `QuestBoard.Service/wwwroot/css/mobile.css:13-22` — both `body` rules get their `background-attachment: fixed` line replaced per D-01/D-02.

</code_context>

<specifics>
## Specific Ideas

- Real-device verification setup: user will bind the dev server to `0.0.0.0` for the verification session and reach it from a physical iPhone over the same Wi-Fi network via its LAN IP (see D-03/D-04) — not a device cloud service, not a tunnel, not the production host.
- Recap badge should be a pixel-for-pixel visual match to desktop's amber pill styling (D-05), not a mobile-adapted simplification.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No new scope-creep items surfaced this session.

</deferred>

---

*Phase: 43-Mobile Parity Fixes*
*Context gathered: 2026-07-04*
