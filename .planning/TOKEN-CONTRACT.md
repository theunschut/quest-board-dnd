# Token Contract: Quest Board ↔ Omphalos SSO Redirect Token

**Status:** Locked — canonical reference for Phase 73 (signer) and Phase 74 (verifier)
**Single canonical copy:** This file lives only in Quest Board's `.planning/`. It is **not** duplicated into the Omphalos repository (`C:\Repos\omphalos`). Phase 74's PR description references this file by link; it does not restate its contents.

## 1. Purpose and Scope

This contract specifies the exact byte-level format of a single, short-lived, signed redirect token that Quest Board mints and Omphalos verifies as part of the SSO handoff: a player clicks into an Omphalos-hosted session from a Quest Board quest, and the token proves to Omphalos who they are and which quest they're arriving from, without either app needing to trust the other's session cookie.

Both applications are first-party software under common operational control (same team, same deployment operator). This is **not** JWT, and this design deliberately does not add a JWT library to either repo — no new NuGet package is introduced by this contract. It borrows JWT's core structural idea (sign over the literal transmitted bytes, never a re-derived copy) without any of JWT's header/claims/algorithm-negotiation machinery, because that machinery solves problems (algorithm agility, multi-issuer trust) that don't exist here: there is exactly one signer, one verifier, one algorithm, and one shared secret per Omphalos deployment.

The token is single-use in intent (5-minute TTL) and carries no long-lived session state of its own — it exists only to bridge the redirect.

## 2. Payload Field List and Types

The signed payload is a JSON object with exactly these six fields. No other fields are part of the contract; do not add or remove fields without revising this document and re-coordinating both sides.

| Field | Type | Description |
|-------|------|--------------|
| `nonce` | `string` | A unique token identifier, e.g. `Guid.NewGuid().ToString()`. **Uniqueness is the only requirement** — Phase 74 keys its replay-protection (used-token tracking) on this field. Cryptographic unpredictability is **not** required for the nonce; that property belongs to the shared secret, not this field. `Guid` is an acceptable, idiomatic .NET choice — do not build a custom random-string generator for this field. |
| `userId` | `int` | Quest Board's `UserEntity.Id`. This is the **only** acceptable identity claim. **Never** use `Name`, `UserName`, or `Email` to identify the user — this is a hard requirement (TOKEN-04), precedented by the Phase 34.3 display-name authorization bug in this codebase, where a display-name-based identity match produced an authorization hole. `UserEntity.Id` is a stable, non-reusable, non-guessable-in-practice integer surrogate key that does not require Omphalos to normalize case, handle email changes, or worry about display-name collisions. |
| `questId` | `int` | Quest Board's `QuestEntity.Id`. Identifies which quest/session the redirect is for. |
| `questTitle` | `string` | Free text (the quest's title). Safe to include as-is because the payload is JSON, not a delimited string — this is precisely the reason JSON was chosen over the originally-researched pipe-delimited format: a title containing a delimiter character (e.g. `\|`) would have shifted every subsequent field in a fixed-order concatenated string. JSON has no equivalent risk. |
| `questDate` | `string` | ISO-8601 round-trip format (`"o"` specifier in .NET, e.g. `2026-07-11T18:30:00.0000000Z`), UTC. This is a **pass-through** value for Omphalos session naming/display purposes only — it carries no security meaning and is never used for expiry or any authorization decision. |
| `expiry` | `long` | Unix seconds, UTC (i.e., `DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 300`). **Deliberately not an ISO-8601 string** — this avoids timezone-parsing ambiguity between two independently-deployed hosts for the one field that is genuinely security-relevant. TTL is **300 seconds (5 minutes)** from issuance (TOKEN-03). |

No `JsonSerializerOptions` customization (naming policy, casing convention, etc.) needs to be agreed between the signer and verifier. Section 5 explains why: the verifier never re-serializes the payload, so the signer's exact serializer settings are irrelevant to correctness — they only affect readability of the raw JSON if someone inspects it manually.

## 3. Encoding and Wire Format

1. The payload object is serialized to JSON, then to UTF-8 bytes.
2. The signature is computed as `HMACSHA256.HashData(UTF8(secret), payloadBytes)`.
3. Both the payload bytes and the signature bytes are independently Base64URL-encoded.
4. The final token string is: `Base64UrlEncode(payloadBytes) + "." + Base64UrlEncode(signatureBytes)`.

**Encoding API:** Use `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode` / `.Base64UrlDecode` on both sides. This is already the established convention in Quest Board's codebase (used for password-reset and email-change-confirmation tokens in `AdminController.cs`, 4+ call sites) — reuse it verbatim. **Do not hand-roll** a `+`/`-`, `/`/`_`, padding-strip base64url implementation on either side; a hand-rolled encoder risks subtly different padding/alphabet behavior between two independently-implemented .NET codebases, which is exactly the class of bug this contract exists to prevent.

The literal `.` separator is not itself encoded — it is a plain ASCII period joining the two Base64URL segments, mirroring the visual shape of a JWT (`header.payload.signature`) without adopting JWT's actual structure (there is no header segment here; the algorithm and key are fixed and out-of-band).

## 4. Signing Sequence (Phase 73 — Quest Board)

1. Build the payload object with the six fields from Section 2.
2. Serialize the payload object to JSON, then to UTF-8 bytes: `payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))`.
3. Compute the signature: `signatureBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payloadBytes)`.
4. Base64URL-encode both byte arrays: `encodedPayload = WebEncoders.Base64UrlEncode(payloadBytes)`; `encodedSignature = WebEncoders.Base64UrlEncode(signatureBytes)`.
5. Join with a literal `.`: `token = encodedPayload + "." + encodedSignature`.
6. Append/embed `token` in the redirect URL to Omphalos.

## 5. Verification Sequence (Phase 74 — Omphalos)

This is the section that makes JSON safe against the same class of canonicalization ambiguity that a pipe-delimited format would have had, and it must be followed exactly — **byte-exact verification, never re-serialization:**

1. Split the received token string on the literal `.` into two segments: `encodedPayload` and `encodedSignature`.
2. Base64URL-**decode both segments back to raw bytes**: `decodedPayloadBytes = WebEncoders.Base64UrlDecode(encodedPayload)`; `decodedSignatureBytes = WebEncoders.Base64UrlDecode(encodedSignature)`.
3. Recompute the HMAC over the **decoded payload bytes** (not over any re-serialized form): `expectedSignatureBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), decodedPayloadBytes)`.
4. Compare `expectedSignatureBytes` to `decodedSignatureBytes` using `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` — a **constant-time** comparison. **Never** use `==`, `.Equals()`, or `SequenceEqual` for this comparison; a variable-time comparison leaks signature bytes one at a time via response-timing side channels.
5. **Only after** the signature check in step 4 passes, deserialize: `JsonSerializer.Deserialize<TokenPayload>(decodedPayloadBytes)` — operating on the *same* decoded payload bytes just verified, not a re-fetched or re-encoded copy.
6. Validate `expiry`: reject if `expiry < DateTimeOffset.UtcNow.ToUnixTimeSeconds()`, allowing a small clock-skew tolerance of **±30 seconds** between the two hosts.
7. Validate `nonce`: check it against Omphalos's used-token store. If already present, reject as a replay. If not present, record it (with an expiry aligned to the token's own TTL window, so the replay-tracking store doesn't grow unbounded) and proceed.

**The critical invariant this whole sequence protects:** the verifier **never re-serializes** the parsed/deserialized fields to recompute the HMAC input, and it never computes the signature over anything other than the exact bytes transmitted in the `encodedPayload` segment. This is the same structural pattern JWT itself uses (sign over the literal `base64url(header).base64url(payload)` string, never a re-derived form) — applied here without adopting an actual JWT library. Two independently-implemented .NET codebases do not need to agree on `JsonSerializerOptions`, property ordering, or whitespace conventions, because the verifier's correctness depends only on decoding bytes and comparing them — not on re-deriving them.

A verifier that calls `JsonSerializer.Deserialize` **before** the signature check, or that re-serializes parsed fields to recompute the HMAC input, violates this contract even if it happens to "work" in casual testing — it reopens a subtler version of the canonicalization risk that motivated moving off the original pipe-delimited design.

## 6. Shared-Secret Provisioning

- The secret is generated **server-side** in Quest Board using a CSPRNG (`System.Security.Cryptography.RandomNumberGenerator`), via a "Generate Secret" action on the settings page. It is never generated client-side.
- An operator (SuperAdmin, or Group Admin for a group-level override) copies the generated value manually into Omphalos's own configuration (e.g. an `Sso:Secret` setting, distinct from any `Jwt:Secret` Omphalos already uses internally for its own purposes).
- **There is no server-to-server sync channel between Quest Board and Omphalos** for this secret or anything else (D-04). This is informational, not a gap: nothing is built for automatic secret propagation, and nothing should be — this is satisfied by the deliberate absence of such a channel, not by a plan task in either phase.
- Each Omphalos deployment verifies incoming tokens against exactly **one** configured secret. If multiple Quest Board groups are each configured (independently, via Quest Board's per-group settings override) to point at the same Omphalos instance, they may share that single secret — this is a valid, unremarkable configuration, not a security concern. Omphalos itself has no multi-tenant awareness of which Quest Board group a token came from; it only verifies the signature against its one configured secret and trusts the `userId`/`questId` claims inside the verified payload.

## 7. Cross-Repo Delivery

This file (`.planning/TOKEN-CONTRACT.md`) is the **single canonical copy** of this contract. Per decision D-06, it is intentionally **not** duplicated into the Omphalos repository (`C:\Repos\omphalos`), which has no `.planning/`/docs convention of its own (confirmed: only `README.md`/`CLAUDE.md` at its repo root). Phase 74's pull request description references this file by link back to this repository rather than restating or copying its contents. If this contract is revised after Phase 73 or Phase 74 has shipped, both sides must be updated in lockstep — there is exactly one source of truth for the format.

---

*Written: Phase 72 (Platform Settings + Token Contract). Implements TOKEN-02 (written contract only — no runtime token code lands in Phase 72). Consumed by Phase 73 (Quest Board, signs) and Phase 74 (Omphalos, verifies).*
