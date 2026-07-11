---
phase: 72-platform-settings-token-contract
plan: 02
subsystem: docs
tags: [hmac, sso, token-contract, security-design, omphalos]

# Dependency graph
requires: []
provides:
  - "Canonical .planning/TOKEN-CONTRACT.md specifying the six-field JSON payload, Base64URL wire format, and byte-exact HMAC-SHA256 signing/verification sequence for the Quest Board <-> Omphalos SSO redirect token"
affects: [73-omphalos-token-signing, 74-omphalos-sso-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Byte-exact HMAC verification: decode both Base64URL segments to raw bytes, recompute the HMAC over the decoded payload bytes, compare with CryptographicOperations.FixedTimeEquals, and only then deserialize — verifier never re-serializes parsed fields"

key-files:
  created: [".planning/TOKEN-CONTRACT.md"]
  modified: []

key-decisions:
  - "Payload field list locked verbatim: nonce (string GUID, uniqueness only), userId (int, UserEntity.Id — never Name/UserName/Email), questId (int), questTitle (string), questDate (ISO-8601 'o' UTC, pass-through), expiry (long, Unix seconds UTC, 300s TTL)"
  - "Wire format: JSON payload -> UTF-8 bytes -> HMACSHA256 signature -> WebEncoders.Base64UrlEncode(payload) + '.' + Base64UrlEncode(signature)"
  - "Verifier must decode-verify-then-parse: never re-serialize parsed fields to recompute the HMAC input, compare signatures with CryptographicOperations.FixedTimeEquals (not ==/SequenceEqual)"
  - "Contract stays a single canonical copy in Quest Board's .planning/ — not duplicated into the Omphalos repo (D-06); Phase 74's PR references it by link"

patterns-established:
  - "Cross-repo design contracts for this milestone live as top-level .planning/*.md files (not nested under a phase directory) so they outlive the phase that wrote them"

requirements-completed: [TOKEN-02]

coverage:
  - id: D1
    description: "Written HMAC canonical-message contract exists specifying the six-field payload, wire encoding, and byte-exact signing/verification sequence so Phase 73 and Phase 74 can each implement independently"
    requirement: "TOKEN-02"
    verification:
      - kind: other
        ref: "test -f .planning/TOKEN-CONTRACT.md && grep -q FixedTimeEquals/nonce/userId/Base64Url .planning/TOKEN-CONTRACT.md"
        status: pass
    human_judgment: false

# Metrics
duration: 12min
completed: 2026-07-11
status: complete
---

# Phase 72 Plan 02: Token Contract Summary

**Canonical `.planning/TOKEN-CONTRACT.md` written, locking the six-field JSON payload, `WebEncoders.Base64Url`-encoded HMAC-SHA256 wire format, and a byte-exact decode-verify-then-parse sequence so Phase 73 (signer) and Phase 74 (verifier) can each implement the SSO token independently without a live cross-repo test being the only way to catch a mismatch.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-11T14:19:00Z
- **Completed:** 2026-07-11T14:31:17Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Wrote `.planning/TOKEN-CONTRACT.md` at the repository top level (not nested under `phases/72-.../`) covering purpose/scope, the six payload fields with exact types, the Base64URL wire format, the signing sequence (Phase 73), the byte-exact verification sequence (Phase 74), shared-secret provisioning, and the cross-repo delivery note.
- Pinned the identity claim to `UserEntity.Id` (int) and explicitly forbade `Name`/`UserName`/`Email`, citing the Phase 34.3 display-name precedent.
- Specified the verifier must decode both Base64URL segments to raw bytes, recompute the HMAC over the decoded payload bytes, compare with `CryptographicOperations.FixedTimeEquals`, and only deserialize after the signature check passes — closing the serializer-non-determinism risk flagged in 72-RESEARCH.md's Pitfall 1.
- Recorded the 300-second TTL, nonce-based replay hook (±30s clock-skew allowance), and the D-06 single-canonical-copy delivery rule (no file created in `C:\Repos\omphalos`).

## Task Commits

Each task was committed atomically:

1. **Task 1: Write the canonical TOKEN-CONTRACT.md** - `c7b530c2` (docs)

**Plan metadata:** pending (this SUMMARY's own commit)

## Files Created/Modified
- `.planning/TOKEN-CONTRACT.md` - Canonical, byte-exact HMAC token-format contract for the Quest Board <-> Omphalos SSO redirect token (design document, no runtime code)

## Decisions Made
None beyond what CONTEXT.md (D-01, D-02, D-06) and RESEARCH.md's "Token Contract Design" section already locked — this plan transcribed those decisions into the canonical document verbatim, adding no new design choices of its own.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required. This plan produces a design document only; no code, migration, or runtime configuration is introduced.

## Next Phase Readiness
- `.planning/TOKEN-CONTRACT.md` is ready to be referenced by Phase 73 (Quest Board token signing implementation) and Phase 74 (Omphalos token verification implementation, via its PR description linking back to this file per D-06).
- No file was created under `C:\Repos\omphalos` — confirmed no cross-repo duplication occurred.
- This plan is independent of 72-01 (settings-storage schema/service work) and does not block or depend on it.

---
*Phase: 72-platform-settings-token-contract*
*Completed: 2026-07-11*

## Self-Check: PASSED

- FOUND: `.planning/TOKEN-CONTRACT.md`
- FOUND: commit `c7b530c2`
