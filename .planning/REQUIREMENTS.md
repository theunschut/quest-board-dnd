# Requirements: D&D Quest Board — v2.0 Omphalos Integration

**Defined:** 2026-07-02
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

This redoes the abandoned `milestone/3-omphalos-integration` attempt (old Phase 10-11, superseded in ROADMAP.md). Requirements below are informed by that attempt's real code review findings (see NAV-02) but independently re-verified against the current state of both repos — see `.planning/research/SUMMARY.md`.

## v1 Requirements

### Platform Settings (Phase 72 — Quest Board)

- [ ] **SETT-01**: SuperAdmin can navigate to an Omphalos Settings page from the `/platform` area
- [ ] **SETT-02**: Settings page has input fields for Omphalos URL and shared secret
- [ ] **SETT-03**: Shared secret field renders as `type="password"` (masked in the UI)
- [ ] **SETT-04**: Submitting the form with the secret field blank preserves the existing secret — empty input = keep existing value, never overwrite with empty string (matches the blank-preserves-existing-value guard used by `ContactsController`/`CharactersController`'s image-upload flows)
- [ ] **SETT-05**: An "Integration Enabled" toggle controls whether all Omphalos UI elements are visible and the SSO redirect is active; when disabled, no Omphalos buttons/links appear anywhere and the launch action is inert
- [ ] **SETT-06**: Settings are persisted in a generic key-value `PlatformSettingEntity` (`Key`, `Value`, nullable `GroupId`) — `GroupId = null` is the instance-wide default, `GroupId = <group>` is a group-specific override; lookups resolve the group-specific row first, falling back to the instance-wide default when no override exists
- [ ] **SETT-07**: The instance-wide settings page (`/platform` area) is protected by the `SuperAdminOnly` authorization policy
- [ ] **SETT-08**: An EF Core migration creates the `PlatformSetting` table
- [ ] **SETT-09**: A group Admin (`GroupRole.Admin`) can configure a group-specific Omphalos URL/secret override from the group's Admin area (alongside the existing `AdminController` group-scoped surface); the override applies only to that group and never affects the instance-wide default or other groups
- [ ] **SETT-10**: A group's DungeonMaster (not Admin) cannot configure the group's Omphalos override — write access is Group Admin only, matching the existing group-scoped authorization pattern

### Navigation + Token Generation (Phase 73 — Quest Board)

- [ ] **NAV-01**: DM navbar dropdown shows an "Open DM Tool" link when integration is enabled and configured
- [ ] **NAV-02**: Every Omphalos entry point — navbar link included — routes through the same signed-token generator; no surface ever links to Omphalos's raw base URL unsigned. (The old attempt's code review caught exactly this bug — WR-01 — on the navbar link; this requirement exists specifically to prevent a regression of it.)
- [ ] **NAV-03**: Quest Detail page shows an "Open Session Notes" button when integration is enabled, configured, and the current user is DM/Admin
- [ ] **NAV-04**: Quest Manage page shows the same "Open Session Notes" button under the same conditions as NAV-03
- [ ] **NAV-05**: When integration is disabled or unconfigured, neither the navbar link nor the quest-page buttons appear — no dead/erroring buttons
- [ ] **NAV-06**: The redirect shows a brief "Opening Omphalos…" loading state rather than an abrupt blank-then-jump transition
- [ ] **TOKEN-01**: `IIntegrationTokenService` (Domain layer) generates a signed redirect URL given a quest ID, quest title/date, and the current user's Quest Board `UserEntity.Id`
- [ ] **TOKEN-02**: The HMAC-SHA256 canonical message format (field order, encoding, delimiter, expiry inclusion) is written down as an explicit, unambiguous contract in Quest Board's `.planning/` (single canonical copy per D-06, not duplicated into the Omphalos repo) before either side's implementation starts
- [ ] **TOKEN-03**: Tokens expire 5 minutes (300s) after generation
- [ ] **TOKEN-04**: The token's identity claim is Quest Board's `UserEntity.Id` (int) — never `Name`, `UserName`, or email
- [ ] **TOKEN-05**: A `QuestController.LaunchOmphalos(int id)` action generates the signed URL and redirects; returns a graceful response (not a raw error) when integration is disabled; protected by `DungeonMasterOnly` (defense in depth — does not rely solely on the button being hidden)
- [ ] **TOKEN-06**: Quest title and date are included in the token payload for pass-through into Omphalos session naming

### SSO Endpoint + Auto-Provisioning (Phase 74 — Omphalos)

- [ ] **SSO-01**: Omphalos exposes a new SSO endpoint (working assumption: `GET /api/sso/login`, following its existing `/api/auth/*` convention — confirm exact path with the maintainer) accepting the signed token
- [ ] **SSO-02**: Endpoint validates the HMAC-SHA256 signature using a Quest Board shared-secret env var (distinct from Omphalos's own `Jwt:Secret`); invalid or missing signature returns HTTP 400
- [ ] **SSO-03**: Expired tokens are rejected with a friendly error/redirect explaining the link expired — not a bare 401/500 (expiry at a 5-minute TTL is a routine occurrence, not exceptional)
- [ ] **SSO-04**: Replay protection — a short-lived used-token tracking mechanism rejects a token that has already been consumed, layered on top of the TTL
- [ ] **SSO-05**: If no Omphalos user matches the incoming Quest Board identity, an account is auto-provisioned with a role mapped from the Quest Board role (DungeonMaster/Admin → Omphalos `Admin`, Player → Omphalos `Player`) and a randomly-generated, never-directly-usable password
- [ ] **SSO-06**: Existing accounts are matched deterministically by `QuestBoardUserId` on repeat visits — never re-provisioned or duplicated
- [ ] **SSO-07**: On success, the endpoint issues Omphalos's existing `omphalos_token` JWT cookie (reusing the existing cookie-issuance code path, not a second one) and redirects into the linked session
- [ ] **SSO-08**: `IAuthService.GenerateToken(User)` is promoted from a private `AuthService` method onto the `IAuthService` interface so the new `SsoService` can call it without duplicating JWT-building logic
- [ ] **SSO-09**: If the Quest Board shared-secret config is absent from the environment, the SSO endpoint returns a clear error (e.g. HTTP 503) while the rest of Omphalos continues operating normally — Omphalos must never fail to boot or break for its own users because this integration isn't configured
- [ ] **SSO-10**: The `omphalos_token` cookie's `Secure` flag is enabled (resolves the existing commented-out TODO in `AuthEndpoints.cs`) — this milestone is what first exercises that cookie across a cross-app, cross-domain redirect
- [ ] **SSO-11**: Omphalos independently enforces role/authorization on the SSO endpoint — it does not trust "the button was hidden on the Quest Board side" as its only access control

### Session Linking (Phase 74 — Omphalos)

- [ ] **LINK-01**: Omphalos's `User` entity gains a `QuestBoardUserId` column with a unique index — the authoritative cross-app identity match key (see TOKEN-04)
- [ ] **LINK-02**: `GameSession` entity gains an `ExternalQuestId` (or equivalent) column with a unique partial index (non-null values only) for find-or-create lookups
- [ ] **LINK-03**: The SSO endpoint finds an existing `GameSession` by `ExternalQuestId` first; only creates a new one (server-generated deterministic ID, e.g. derived from the quest ID) if no match exists, titled/dated from the token's payload (TOKEN-06)
- [ ] **LINK-04**: EF Core migrations add both new columns to Omphalos's PostgreSQL schema

## v2 Requirements (Future)

- **BIDI-01**: Bidirectional API (Omphalos → Quest Board reads) — this milestone lays the architectural seam only (don't design anything that blocks it later); no implementation now
- **IDP-01**: Shared identity provider (Authentik recommended; Keycloak as the safer industry-default alternative) replacing this milestone's bilateral HMAC bridge — triggered by a second/third app needing shared auth, not part of this milestone. This milestone's `IIntegrationTokenService`/`SsoService` seam is deliberately designed to make that migration cheap when it happens.
- **PK-01**: `int`-to-GUID primary key migration for Quest Board's Identity users — raised during this milestone's scoping, explicitly independent and deferred; does not affect the SSO identity-key design (TOKEN-04/LINK-01), since the ID travels inside a signed token rather than being exposed raw

## Out of Scope

| Feature | Reason |
|---------|--------|
| Full OAuth2/OIDC authorization-code flow | Solves delegated *third-party* client access — both apps here are first-party and under common operational control, a different trust boundary. Not a scale-based exclusion; would be the same answer at any user count |
| Self-service account-linking UI | Deterministic auto-provisioning by `QuestBoardUserId` (LINK-01) makes a manual linking flow unnecessary |
| Omphalos-side multi-tenancy / multi-secret verification | Per-group settings (SETT-06/09) work by letting each group point at its own independent Omphalos deployment — each deployment still only ever verifies against its own single configured secret. Omphalos itself never needs to know about Quest Board's groups |
| Token encryption (JWE) on top of signing | The token payload (quest ID, user ID, expiry) has no confidential content; encryption adds a key-management burden with no confidentiality benefit |
| Real-time bidirectional session sync | Explicitly out of scope per the original milestone ask — architectural seam only (BIDI-01), not built |

## Traceability

| Requirement | Phase | Repo | Status |
|-------------|-------|------|--------|
| SETT-01 | Phase 72 | Quest Board | Complete |
| SETT-02 | Phase 72 | Quest Board | Complete |
| SETT-03 | Phase 72 | Quest Board | Complete |
| SETT-04 | Phase 72 | Quest Board | Complete |
| SETT-05 | Phase 72 | Quest Board | Complete |
| SETT-06 | Phase 72 | Quest Board | Complete |
| SETT-07 | Phase 72 | Quest Board | Complete |
| SETT-08 | Phase 72 | Quest Board | Complete |
| SETT-09 | Phase 72 | Quest Board | Complete |
| SETT-10 | Phase 72 | Quest Board | Complete |
| NAV-01 | Phase 73 | Quest Board | Pending |
| NAV-02 | Phase 73 | Quest Board | Pending |
| NAV-03 | Phase 73 | Quest Board | Pending |
| NAV-04 | Phase 73 | Quest Board | Pending |
| NAV-05 | Phase 73 | Quest Board | Pending |
| NAV-06 | Phase 73 | Quest Board | Pending |
| TOKEN-01 | Phase 73 | Quest Board | Pending |
| TOKEN-02 | Phase 72 | Quest Board | Complete |
| TOKEN-03 | Phase 73 | Quest Board | Pending |
| TOKEN-04 | Phase 73 | Quest Board | Pending |
| TOKEN-05 | Phase 73 | Quest Board | Pending |
| TOKEN-06 | Phase 73 | Quest Board | Pending |
| SSO-01 | Phase 74 | Omphalos | Pending |
| SSO-02 | Phase 74 | Omphalos | Pending |
| SSO-03 | Phase 74 | Omphalos | Pending |
| SSO-04 | Phase 74 | Omphalos | Pending |
| SSO-05 | Phase 74 | Omphalos | Pending |
| SSO-06 | Phase 74 | Omphalos | Pending |
| SSO-07 | Phase 74 | Omphalos | Pending |
| SSO-08 | Phase 74 | Omphalos | Pending |
| SSO-09 | Phase 74 | Omphalos | Pending |
| SSO-10 | Phase 74 | Omphalos | Pending |
| SSO-11 | Phase 74 | Omphalos | Pending |
| LINK-01 | Phase 74 | Omphalos | Pending |
| LINK-02 | Phase 74 | Omphalos | Pending |
| LINK-03 | Phase 74 | Omphalos | Pending |
| LINK-04 | Phase 74 | Omphalos | Pending |

**Coverage:**
- v1 requirements: 35 total
- Mapped to phases: 35/35
- Unmapped: 0

---
*Requirements defined: 2026-07-02*
*Last updated: 2026-07-02 after initial definition*
