# Phase 72: Platform Settings + Token Contract - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-02
**Phase:** 72-platform-settings-token-contract
**Areas discussed:** Token contract fields & replay nonce, Shared secret input UX, Settings page naming & nav placement, Cross-repo contract delivery

---

## Token contract fields & replay nonce

**Q1: Replay key**

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit nonce field (recommended) | Add a random token-ID field to the canonical message; Phase 74 keys its used-token table on it | ✓ |
| Hash of the full token | Phase 74 stores a hash of the complete signed token as its "used" key | |
| You decide | Let researcher/planner pick | |

**User's choice:** Explicit nonce field (recommended)

**Q2: Encoding**

| Option | Description | Selected |
|--------|-------------|----------|
| JSON payload, then Base64URL (recommended) | Serialize fields as JSON, HMAC-sign the raw bytes, Base64URL-encode for the URL — no delimiter-collision risk | |
| Fixed-order delimited string | Concatenate fields with a strict delimiter in fixed order | |
| You decide | Let researcher/planner pick the concrete format | ✓ |

**User's choice:** You decide
**Notes:** Claude's discretion applied — JSON + Base64URL chosen (matches the "recommended" option), to avoid the field-shifting collision risk research flagged as highest-risk.

---

## Shared secret input UX

**Q1: How the secret gets into the field**

| Option | Description | Selected |
|--------|-------------|----------|
| Manual paste only | SuperAdmin supplies the value themselves | |
| "Generate Secret" button | Page generates a cryptographically random value | (user raised a third option, see below) |

**User's choice (free text):** User asked whether a REST API between Quest Board and Omphalos (to sync the secret automatically) was also Claude's idea, expressed interest in a "Generate Secret" button, and asked for security advice comparing manual vs. automatic.

**Notes:** Responded that a sync API is exactly the BIDI-01 bidirectional-API scope already deferred to a future milestone — this milestone is a one-way browser-redirect handoff, neither app calls the other's API. Explained the chicken-and-egg trust-bootstrap problem with auto-syncing a shared secret, and the added attack surface/availability coupling a sync API would introduce. Recommended "Generate Secret" button with manual one-time copy into Omphalos's `.env` — no sync API. User confirmed via follow-up: **"Yes, lock it in."**

---

## Settings page naming & nav placement

| Option | Description | Selected |
|--------|-------------|----------|
| Standalone "Omphalos Integration" page | New nav item named after Omphalos specifically | |
| Generic "Integrations" page | Nav item titled "Integrations", framed as a category for future integrations | ✓ |
| You decide | Let Claude pick naming/placement | |

**User's choice:** Generic "Integrations" page
**Notes:** Route, icon, and exact copy left to planning.

---

## Cross-repo contract delivery

| Option | Description | Selected |
|--------|-------------|----------|
| Commit a doc into the Omphalos repo directly | New docs file added to C:\Repos\omphalos now | |
| Keep it in Quest Board's .planning/, reference it from the Phase 74 PR | No new file in Omphalos; PR description links back | ✓ |
| You decide | Let Claude pick based on friction | |

**User's choice:** Keep it in Quest Board's .planning/, reference it from the Phase 74 PR
**Notes:** Confirmed Omphalos has no .planning/docs convention today (only README.md/CLAUDE.md at repo root) before asking this question.

---

## Claude's Discretion

- Canonical message serialization: JSON + Base64URL encoding (Token contract fields area, Q2) — user deferred explicitly.
- Exact route path, FontAwesome icon, and page copy for the "Integrations" settings page — left to planning/UI phase.
- Exact nonce generation method (RandomNumberGenerator vs GUID) — left to research/planning.

## Deferred Ideas

- **Server-to-server API between Quest Board and Omphalos** — raised during the Shared Secret UX discussion. Not a new deferred item: already covered by requirement BIDI-01 and the milestone's Key Decisions log. Redirected back to existing scope rather than added to the roadmap backlog.
