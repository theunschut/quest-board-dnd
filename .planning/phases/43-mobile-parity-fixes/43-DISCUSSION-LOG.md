# Phase 43: Mobile Parity Fixes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 43-Mobile Parity Fixes
**Areas discussed:** Real-device verification access, Recap badge visual style on mobile, Recap badge placement in the mobile card

---

## Real-device verification access

| Option | Description | Selected |
|--------|-------------|----------|
| Physical iPhone available | You (or someone on the team) has a real iPhone to test on directly. | ✓ |
| Real-device-cloud service | BrowserStack, Sauce Labs, or similar. | |
| Not arranged yet | Verification access isn't set up; flag as a blocker. | |

**User's choice:** Physical iPhone available.
**Notes:** User followed up asking whether the phone would be able to reach the dev machine over the network. Explained the `launchSettings.json` binds to `localhost` only today, and laid out three options for reaching a locally-run instance from a real device.

| Option | Description | Selected |
|--------|-------------|----------|
| LAN + firewall rule | Rebind Kestrel to 0.0.0.0, open the port in Windows Firewall, connect phone to same Wi-Fi. | ✓ |
| Tunnel (ngrok/Cloudflare) | Expose local dev server via a public HTTPS URL. | |
| Deploy to real host first | Push the fix to /opt/questboard/ and verify there directly. | |

**User's choice:** LAN + firewall rule — phone and dev PC are on the same Wi-Fi.
**Notes:** This is a per-verification-session runtime setting (e.g. `dotnet run --urls "http://0.0.0.0:8000"`), not a change to the committed `launchSettings.json`.

---

## Recap badge visual style on mobile

| Option | Description | Selected |
|--------|-------------|----------|
| Match desktop's fantasy style | Amber/gold pill (`rgba(255,193,7,0.2)` bg, matching border, `fa-scroll` icon) — true visual parity with desktop's `recap-badge` class. | ✓ |
| Match mobile's existing badge look | Plain Bootstrap badge consistent with the CR badge already on the same card. | |

**User's choice:** Match desktop's fantasy style.
**Notes:** Requires new CSS in `quest-log.mobile.css` since the `.recap-badge` styling doesn't exist there today (only in desktop's `quests.css`).

---

## Recap badge placement in the mobile card

| Option | Description | Selected |
|--------|-------------|----------|
| New row at the bottom | Own row below the DM name — mirrors desktop's card-footer placement. | ✓ |
| Inline next to the date | Same row as the completed date. | |
| Top, next to the title | Near the title/CR badge row. | |

**User's choice:** New row at the bottom.
**Notes:** None — chosen directly to mirror desktop's existing layout order.

---

## Claude's Discretion

- Fixing `site.css`'s copy of the `background-attachment: fixed` bug even though it's unreachable by any real iOS Safari session in this codebase (iPad routes to `mobile.css` via `MobileDetectionMiddleware`) — fixed anyway for consistency between the two files, at negligible cost.
- Keeping the full "Session Recap Available" label text on mobile rather than a shortened variant — the mobile card has ample width for the full phrase.

## Deferred Ideas

None — discussion stayed within phase scope.
