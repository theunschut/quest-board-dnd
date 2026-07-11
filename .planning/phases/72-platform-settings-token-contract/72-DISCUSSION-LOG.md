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

---

## Session 2 — Settings storage redesign

**Date:** 2026-07-11
**Areas discussed:** Settings storage schema, Per-group override scope, Authorization for group override

User opened this session already rejecting the existing plan: *"I'm not a fan of a single row with columns as settings. It's not future proof and a bit of an ugly design."* Codebase scout confirmed no key-value settings precedent exists anywhere in the codebase, and that `PROJECT.md`'s original milestone wording had said "key-value store" from the start — the fixed-column design was an undocumented deviation introduced during research/planning.

### Settings storage schema

| Option | Description | Selected |
|--------|-------------|----------|
| Generic key-value table | `PlatformSettingEntity{ Key, Value }`, no migration needed for future settings; no DB type safety | ✓ (later refined with GroupId) |
| Singleton row + JSON column | One row, JSON-serialized typed object; future-proof and typed, but no query-inside-JSON needed anyway for this use case | |
| Fixed-column singleton row (original plan) | `IntegrationSettingEntity{ Url, SharedSecret, IsEnabled }`; simplest but needs a migration per future setting | |

**User's choice:** Generic key-value table, with a follow-up: *"add an additional column with GroupId so group settings are possible."*

### Per-group override scope

Three follow-up questions narrowed this down:
1. **Schema-only groundwork vs. build the feature now** → User chose **build it now**.
2. **Which per-group scenario** → User confirmed groups pointing at separate Omphalos deployments (not one shared instance needing multi-secret verification), but added: *"if two groups point to the same omphalos instance, it should still work... a single shared secret is fine"* — i.e. duplicate secret values across groups are acceptable, no uniqueness constraint needed.
3. **GroupId nullable, so both instance-wide and per-group settings share one table** — user's own suggestion, adopted as D-08's cascade design (group override → instance-wide fallback).

**Notes:** This resolves the "Omphalos has no tenant concept" objection that was in `REQUIREMENTS.md`'s Out of Scope table — Omphalos itself never needs multi-tenant awareness under this design, since each deployment only ever verifies its own single secret regardless of how many Quest Board groups point at it.

### Authorization for group override

| Option | Description | Selected |
|--------|-------------|----------|
| Group DungeonMaster or Admin | Matches the existing `DungeonMasterOnly` policy pattern | |
| Group Admin only | Narrower — mirrors SuperAdmin being sole owner of the instance-wide default | ✓ |

**User's choice:** Group Admin only

### Consequence: REQUIREMENTS.md and PROJECT.md updated

User confirmed (explicit y/n question) that `REQUIREMENTS.md` should be updated directly rather than just noted as superseded in CONTEXT.md, since SETT-06 as originally worded directly contradicted the new design. SETT-06/07/08 revised, SETT-09/10 added, the "Per-group Omphalos configuration" Out of Scope row rewritten, traceability table updated. `PROJECT.md`'s target-features bullet and Key Decisions table row (previously "Pending: confirm during Phase 72 implementation") both updated to reflect the resolved decision.

## Claude's Discretion (Session 2)

- Exact `(Key, GroupId)` uniqueness enforcement mechanism (filtered unique index vs. app-level check) — left to research/planning.
- Exact repository/service method shape for the cascade lookup — left to research/planning.
- Whether the group-override page is a new action on the existing `AdminController` or a new sibling controller — left to planning.
