# Project Research Summary

**Project:** D&D Quest Board — v2.0 Omphalos Integration
**Domain:** Cross-app SSO bridge (signed-token redirect handoff between two independently-deployed .NET apps)
**Researched:** 2026-07-02
**Confidence:** HIGH

## Executive Summary

This milestone connects two independent, already-shipping apps — Quest Board (ASP.NET Core 10 MVC, SQL Server, multi-tenant since v5.0) and Omphalos (.NET 10 Minimal API, PostgreSQL, single-tenant, owned by another maintainer) — via a browser-redirect SSO flow using a short-lived HMAC-signed token. Neither app calls the other's API in this milestone; Quest Board generates a signed URL and issues a browser redirect, Omphalos validates it, auto-provisions the user, finds/creates the linked session, and issues its own JWT cookie. This is a redo of an abandoned attempt (`milestone/3-omphalos-integration`) whose code diverged too far from `main` to merge — all four research passes were run independently against the *current* state of both repos rather than trusting the old branch's conclusions, and in several places reached different (better-grounded) answers.

The work splits cleanly into Quest Board-side work (new Platform Settings entity/page, token-generation service, nav/button entry points — no external dependency, lowest risk) and Omphalos-side work (a new SSO endpoint, promoting a private `GenerateToken` method to the interface, a schema change to link sessions back to quests). The Quest Board side is technically simpler but the Omphalos side is the actual critical path, because it requires PR review on a repo this project doesn't own — the same failure mode ("branch diverged before merge") that killed the previous attempt. The token-format contract between the two repos must be locked in writing before either side's implementation starts, since nothing but a live end-to-end test would catch a mismatch afterward.

The highest-risk finding to come out of this research pass, not present in the old branch's research: **the identity-matching key must not be Quest Board's display name or a raw username string.** Quest Board's own `PROJECT.md` decision log records a real security bug (Phase 34.3) caused by exactly this class of mistake — `User.Name` has no uniqueness constraint. Two independent research passes (Architecture, Pitfalls) converged on the same fix: match on Quest Board's stable, immutable `UserEntity.Id`, not on `Name`/`Username`/email. See "Identity Matching — Resolved" below.

## Key Findings

### Recommended Stack

Zero new NuGet packages needed in either repo. Everything required is BCL or already present: `HMACSHA256.HashData`, `CryptographicOperations.FixedTimeEquals`, and `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode/Decode` (Quest Board already uses the latter for password-reset tokens in `AccountController.cs` — reuse that exact encoding convention rather than inventing a second one). Omphalos already has a JWT stack (`Microsoft.IdentityModel.Tokens`) for its *own* session cookie, but that's a separate trust boundary — the incoming SSO token needs its own `Sso:Secret` config value, distinct from Omphalos's `Jwt:Secret`.

**Core technologies:**
- `System.Security.Cryptography.HMACSHA256` (BCL): sign/verify the token — both repos, no package needed
- `Microsoft.AspNetCore.WebUtilities.WebEncoders`: Base64URL encode/decode the token — already used in Quest Board, add to Omphalos
- New EF Core entity (`IntegrationSettingEntity` or similar) + migration on Quest Board: `IOptions<T>` cannot satisfy a runtime-editable SuperAdmin setting — that pattern (used by `EmailSettings`) is startup-bound only

### Expected Features

**Must have (table stakes):**
- Every Omphalos entry point (navbar link, quest-page button) must route through the same signed-token generator — the old branch's Phase 11 code review caught a real bug (WR-01) where the navbar link pointed at Omphalos's raw base URL with no token, dropping DMs on a login wall
- Graceful degradation when integration is disabled/unconfigured — no dead buttons, no exceptions
- Role mapping into Omphalos on auto-provision (DM/Admin → Omphalos Admin, Player → Omphalos Player) — defaulting everyone to Player silently strips DM permissions

**Should have (differentiators):**
- Session find-or-create keyed by quest ID so re-opening the same quest lands in the same Omphalos session
- Foundation for a future bidirectional (Omphalos → Quest Board) API — no code this milestone, just don't architect anything that blocks it later

**Defer / anti-features (verified as wrong-fit for ~17 trusted self-hosted users, not generic SaaS advice):**
- OAuth2/OIDC — overkill for a shared-secret redirect between two apps you both control
- Nonce/replay-protection store — a 5-minute token window is an acceptable residual risk at this trust level; adds stateful complexity for no real benefit here
- Per-group Omphalos settings — Omphalos has no tenant concept to map a per-group setting onto (see Architecture Approach below)
- Self-service account-linking UI — auto-provisioning by stable ID is sufficient

### Architecture Approach

Quest Board-side work follows the existing `GroupEntity`-style shape exactly: `Entity → Repository → Domain Service → Controller`, landing under the `/platform` Area (`SuperAdminOnly`) alongside `GroupController` — confirmed correct because Omphalos has no org/tenant concept of its own (flat, `UserId`-scoped data model), so a per-group Quest Board setting would have nothing on the Omphalos side to attach to. Quest Board's repository files are flat at the project root (`QuestBoard.Repository/GroupRepository.cs`), not nested in a `Repositories/` folder — a real convention trap for anyone assuming otherwise.

Omphalos-side work follows its route-group-per-feature Minimal API pattern: a new `SsoEndpoints.cs` exposing `MapSsoEndpoints()`, registered in `Program.cs` alongside the existing `AuthEndpoints`/`SessionEndpoints`/`AdminEndpoints`/`SettingsEndpoints`. `AuthService.GenerateToken(User)` is currently `private` on the concrete class, not on `IAuthService` at all — it must be promoted to be reusable by a new `SsoService` without duplicating JWT-building logic. `GameSession.Id` is currently always client-generated (`session-${Date.now()}`) — the SSO flow is the first place a session gets created server-side, so it needs a deterministic ID scheme for idempotent find-or-create (e.g. derived from the Quest Board quest ID). Omphalos's React SPA has no client-side router — landing in the correct session on load needs only a small `AppContext.jsx` change reading a `?session=` query param, not a new routing dependency.

**Major components (new):**
1. Quest Board: `IntegrationSettingEntity` + repository + domain service + Platform Settings controller/view — instance-wide config, SuperAdmin-only
2. Quest Board: `IIntegrationTokenService` (Domain layer) — generates the signed redirect URL; a `LaunchOmphalos` controller action; a nav ViewComponent for the DM dropdown link
3. Omphalos: `SsoEndpoints.cs` + `SsoService` — validates the token, auto-provisions by Quest Board `UserId`, finds/creates the linked `GameSession`, issues the existing JWT cookie

### Identity Matching — Resolved

Three research passes proposed three different cross-app matching keys (username, email, numeric ID) before converging. The resolution, informed by all four:

- **Do not use Quest Board's `Name`/display string** — no uniqueness constraint; this exact mistake already caused a real authorization bug in this codebase (Phase 34.3)
- **Do not require an `Email` column on Omphalos's `User`** — it doesn't exist today, and adding it is unnecessary schema churn when a better key is available
- **Use Quest Board's stable, immutable `UserEntity.Id`** (the ASP.NET Identity primary key) as the token's identity claim, and add a new normalized `QuestBoardUserId` column with a unique index on Omphalos's `User` entity as the match key. Auto-provisioning still derives a human-readable `Username` for the new Omphalos account (from Quest Board's display name or email, cosmetic only), but the *authoritative* lookup on repeat logins is always by `QuestBoardUserId`, never by name or email string comparison — sidestepping Postgres case-sensitivity mismatches with SQL Server's case-insensitive Identity matching entirely.

### Critical Pitfalls

1. **HMAC canonical-message format** — the single highest-risk item. Both repos must agree on an identical, unambiguous field order/encoding/expiry-inclusion *before* either side writes code; a hand-rolled string concatenation is vulnerable to field-shifting collisions. Write the contract down and copy it verbatim into both repos' planning docs.
2. **Identity matching** — see above; resolved via `QuestBoardUserId`, not name/email string matching.
3. **Secret handling in the new Platform Settings page** — Quest Board's `AdminController.EditUser` already has an established blank-field-preserves-existing-value guard pattern for email-change confirmation; the new secret field must follow it exactly, or the first no-op save silently wipes the shared secret and breaks all subsequent SSO redirects with no obvious error.
4. **Omphalos's `Secure=true` cookie TODO is still uncommented** (`AuthEndpoints.cs:15`) — this milestone is what will actually exercise that cookie in a cross-app flow behind separate Traefik/reverse-proxy TLS termination, so it should be fixed as part of this work, not left as pre-existing tech debt.
5. **Cross-repo PR friction is concrete, not generic** — Omphalos is owned by a different GitHub identity, has had 3 migrations in one week (active concurrent development → real migration-snapshot conflict risk), and has zero CI test coverage (maintainer review is the only safety net). This exact dynamic already killed the previous integration attempt.

## Implications for Roadmap

Starting at **Phase 35** (continuing from v5.0's Phase 34.3; the old Phase 10/11 slots are marked superseded in ROADMAP.md).

### Phase 35: Platform Settings + Token Contract (Quest Board)
**Rationale:** No external dependency, lowest risk — build first. The token-format contract must be written down as part of this phase, even though the code that consumes it lands in Phase 36/37, because Omphalos-side work can start in parallel once the contract is agreed.
**Delivers:** `IntegrationSettingEntity` + migration, Platform Settings page (SuperAdmin, URL + shared secret + enabled toggle, blank-preserves-existing-value guard), the written HMAC token-format contract (field order, encoding, expiry, identity claim = Quest Board `UserId`)
**Addresses:** Admin Settings feature (table stakes)
**Avoids:** Pitfall 1 (HMAC format), Pitfall 3 (secret blank-overwrite)

### Phase 36: Navigation + Token Generation (Quest Board)
**Rationale:** Depends on Phase 35's settings service existing at compile time.
**Delivers:** `IIntegrationTokenService`, DM navbar link (ViewComponent), quest-page "Open Session Notes" button, `LaunchOmphalos` redirect action — every entry point routes through the same token generator (avoids the old branch's WR-01 bug)
**Uses:** BCL `HMACSHA256`, `WebEncoders.Base64UrlEncode`
**Implements:** Token-generation component from Architecture Approach

### Phase 37: Omphalos SSO Endpoint (Omphalos repo)
**Rationale:** Can start in parallel with Phase 36 once Phase 35's contract is written — no compile-time dependency on Quest Board's code, only on the agreed contract. This is the actual critical path due to external PR review latency; open the PR early.
**Delivers:** `IAuthService.GenerateToken` promoted from private to interface, `QuestBoardUserId` column + migration on `User`, `SsoEndpoints.cs` + `SsoService` (validate token → auto-provision by `QuestBoardUserId` → find/create `GameSession` → issue existing JWT cookie), `Secure=true` cookie fix, `AllowedOrigins` config entry for Quest Board's origin
**Addresses:** SSO Endpoint feature, Identity Matching resolution
**Avoids:** Pitfall 2 (identity), Pitfall 4 (cookie security), Pitfall 5 (cross-repo friction — budget review-latency slack)

### Phase Ordering Rationale

- Quest Board phases (35, 36) are sequential — 36 needs 35's settings service at compile time
- Phase 37 (Omphalos) has no compile-time coupling to 35/36, only to the written contract from Phase 35 — it can run concurrently with Phase 36 once that contract exists, which shortens the critical path given Omphalos's external review latency
- End-to-end verification requires all three phases complete

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 37:** Omphalos's actual Postgres collation and whether `Users.Username` already has a unique index weren't directly verifiable without a live DB connection — confirm before finalizing the auto-provisioning migration
- **Phase 37:** Exact SSO route path is Omphalos's call (working assumption: `/api/sso/login`, consistent with its `/api/auth/*` convention) — confirm with the Omphalos maintainer, not just assumed

Phases with standard patterns (skip research-phase):
- **Phase 35, 36:** Both follow established Quest Board conventions (`GroupEntity` shape, `WebEncoders` precedent, ViewComponent pattern) closely enough that no additional phase-level research is needed

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack (no new packages) | HIGH | Verified directly against both repos' `.csproj` files and current .NET 10 docs |
| Features (table stakes / anti-features) | HIGH | Grounded in the old branch's real, code-reviewed implementation plus this project's own PROJECT.md history |
| Architecture (Quest Board side) | HIGH | Read actual `GroupController`, `GroupRepository`, `ServiceExtensions.cs`, `Program.cs`, `_Layout.cshtml` |
| Architecture (Omphalos side) | HIGH | Read actual `AuthEndpoints.cs`, `AuthService.cs`, `SessionRepository.cs`, `UserRepository.cs`, EF configurations |
| Identity matching resolution | HIGH | Two independent research passes converged, directly citing this codebase's own prior bug as evidence |
| Pitfalls (codebase-specific) | HIGH | Verified by reading both repos' source, entity definitions, auth endpoints, config, CI workflows |
| Pitfalls (general HMAC/timing-attack guidance) | MEDIUM | Corroborated across 2+ external sources, not an official Microsoft doc for this exact pattern |
| Omphalos Postgres collation / existing unique index | LOW | Not directly queryable without a live DB connection — flagged as a Phase 37 gap |

**Overall confidence:** HIGH — the two gaps below are narrow and don't affect the overall design.

### Gaps to Address

- Omphalos's live Postgres collation and whether `Users.Username` already has a unique index — verify with a direct schema query at the start of Phase 37, before writing the migration
- Exact Omphalos SSO route path — confirm with the maintainer rather than assuming `/api/sso/login`
- Whether `AllowedOrigins`/CORS population is strictly required for a top-level redirect (not a fetch) — likely not blocking, confirm during Phase 37 implementation

## Sources

### Primary (HIGH confidence)
- Direct inspection of `C:\Repos\quest-board` source (current `main`/milestone branch) — controllers, entities, `Program.cs`, `.csproj` files, `PROJECT.md` decision log
- Direct inspection of `C:\Repos\omphalos` source (`origin/main` and `feature/shared-locations-library`, confirmed identical on all SSO-relevant files) — entities, endpoints, `Program.cs`, `appsettings.json`, CI config
- `origin/milestone/3-omphalos-integration` (abandoned branch) — reviewed as historical reference for the old requirements/HMAC design, not treated as ground truth
- Microsoft Learn (.NET 10-versioned docs) — `HMACSHA256`, `CryptographicOperations.FixedTimeEquals`, `WebEncoders`

### Secondary (MEDIUM confidence)
- Soatok's cryptography blog, Wikipedia, Paragon Initiative — HMAC canonicalization and timing-attack general guidance, cross-verified across 2+ sources
- Primer, AWS Well-Architected — graceful-degradation UX pattern guidance

---
*Research completed: 2026-07-02*
*Ready for roadmap: yes*
