# Feature Research

**Domain:** Cross-app SSO handoff (companion-app deep link, browser-redirect SSO between two self-hosted apps)
**Researched:** 2026-07-02
**Confidence:** HIGH — grounded in (1) direct inspection of both live codebases (Quest Board `.planning/PROJECT.md`, Omphalos `AuthService.cs`/`AuthEndpoints.cs`/`GameSession.cs`), (2) the abandoned `milestone/3-omphalos-integration` branch's completed Phase 10-11 code + its Phase 11 code review (`11-REVIEW.md`) which caught a real SSO-bypass bug, and (3) general websearch on HMAC handoff / replay-protection / graceful-degradation patterns (MEDIUM confidence on the general-industry portions, cross-checked against multiple sources).

## Project-Specific Grounding

This is not a greenfield "what does SSO look like" question — a prior attempt (`milestone/3-omphalos-integration`, phases 10-11) built and shipped the Quest Board side of this exact feature before being abandoned as unmergeable. That code is a real, reviewed prior implementation, not a hypothesis:

- **Phase 10** (Admin Settings) and **Phase 11** (Navigation + Token Generation) both completed and passed code review on the old branch.
- **Phase 20** (Omphalos-side SSO endpoint + session linking) was never started — no `ExternalQuestId` field exists on Omphalos's `GameSession` entity today, confirmed by direct grep of the current Omphalos repo. The Omphalos side of this milestone is genuinely greenfield.
- The old Phase 11 code review caught a real instance of the exact anti-pattern this research needs to flag: **WR-01** found the navbar "Open Omphalos" link pointing directly at the raw base URL with no token attached, bypassing SSO entirely and dropping the DM on Omphalos's login wall. This is now baked into the table-stakes list below as a named pitfall, not a hypothetical.
- The old design locked a canonical HMAC message format: `expiry={unix_ts}&questId={id}&questTitle={url_encoded_title}&username={lower}`, alphabetical key order, HMAC-SHA256, lowercase hex signature, 300-second TTL. This is a reasonable, well-scoped starting point and is referenced below where relevant, but the actual token contract is this milestone's design decision to re-verify, not something to blindly re-adopt (multi-tenancy didn't exist when it was designed).

Key facts about current Omphalos auth (confirmed from source, not assumed):
- Login issues a JWT via a **private** `AuthService.GenerateToken(User)` method — not on `IAuthService` today, so it isn't callable from an SSO service without either promoting it to the interface or duplicating token-issuance logic.
- The JWT is delivered as an httpOnly `omphalos_token` cookie, `SameSite=Lax`, with a `Secure=true` TODO still commented out in `AuthEndpoints.cs` — i.e. Omphalos isn't cookie-hardened yet regardless of this milestone.
- `GameSession.Id` is a **client-generated string** with no existing FK/link back to a Quest Board quest — session-quest linking requires a net-new field (e.g. `ExternalQuestId`) plus a lookup method.
- Omphalos has **no tenant/org concept** — every entity scopes to `UserId` only. This is why Quest Board's settings are instance-wide (Platform/SuperAdmin), not per-group — there is nothing on the Omphalos side for a per-group setting to bind to.
- CORS scaffolding (`AllowedOrigins` array) already exists in Omphalos config but is empty/inert — enabling it is a config change, not new code.

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = the "one-click SSO" pitch is broken or embarrassing.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Signed, short-lived, single-purpose redirect token | The entire pitch is "one click, already authenticated" — any link that isn't cryptographically bound to (user, quest, expiry) is either insecure or just a bookmark | MEDIUM | Old design (HMAC-SHA256 over canonical alphabetical query string, 300s TTL) is a solid baseline; BCL-only, zero new NuGet packages on either side |
| Auto-provision Omphalos user on first SSO click | DMs must never see Omphalos's own login/register form as part of this flow — that's a broken handoff, not a feature gap | MEDIUM | Needs a stable identity key to match Quest Board users to Omphalos users across two independent user tables (see Dependencies) |
| Auto-create-or-find the matching GameSession | "Land in the correct session" is the literal goal; if it silently creates duplicates on every click, the feature actively harms Omphalos's data (fragmented session notes per quest) | MEDIUM | Requires a net-new `ExternalQuestId` (or similar) column + find-or-create lookup — does not exist in Omphalos today |
| Every UI entry point (navbar + quest page buttons) routes through the SAME signed-token endpoint — no raw/unsigned links anywhere | Any link that bypasses the token issuance defeats the whole feature for that one entry point | LOW | Directly caught as a bug (WR-01) in the old implementation: the navbar link pointed at the raw base URL with no token, dropping DMs on a login wall. Explicitly re-verify every click surface routes through the signed-URL generator, including the navbar |
| Graceful hide when integration is unconfigured (no URL / no secret / disabled toggle) | A visible "Open Session Notes" button that 404s or errors is worse than no button — users don't distinguish "not configured" from "broken" | LOW | Single `IsConfigured` gate (`IsEnabled && URL set && secret set`) already proven in the old Phase 10 code; apply consistently everywhere the button/link can render |
| Expired-token error handling with an actionable retry, not a raw 401/500 | Users double-click, leave a tab open overnight then click it, etc. — an expired token is a routine, not exceptional, event at 300s TTL | LOW | Omphalos SSO endpoint should return a friendly "link expired, go back and click again" page/redirect, not a bare status code |
| Role mapping: only DM/Admin can reach the SSO launch action | Quest Board already gates this UI to DM/Admin (`DungeonMasterOnly` policy); the SSO endpoint must independently enforce it doesn't trust the client to have checked | LOW | Defense in depth — `[Authorize(Policy = "DungeonMasterOnly")]` on the Quest Board launch action AND signature validation + explicit role/claim on the Omphalos side; never rely on "the button was hidden" as the only control |
| Standalone-operation fallback when integration env/config is absent | Omphalos is maintained by someone else and used outside this pairing — the SSO endpoint must be inert, not crash-on-boot, when `QUEST_BOARD_SECRET` (or equivalent) isn't set | LOW | Confirmed as an explicit design goal in the old ROADMAP: "Omphalos starts and operates normally when `QUEST_BOARD_SECRET` is not set — only the SSO endpoint is affected" |
| Consistent identity across repeated SSO visits (same Quest Board user always lands as the same Omphalos user) | Breaking this means session notes/characters get split across "ghost" duplicate accounts, which is a data-integrity failure a small trusted group will notice immediately | MEDIUM | Needs a durable matching key — see Dependencies section; username string-matching is fragile (renames, case, uniqueness) |

### Differentiators (Competitive Advantage)

Not required for the core loop to work, but meaningfully improve the experience for this specific pairing. Worth doing because they're cheap given what already exists, not because they're expected.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Quest title/date passed through and shown on session creation | DM lands in Omphalos and the session is already titled/dated sensibly instead of "New Session" — saves a manual rename every time | LOW | Already in the old token contract (`questTitle` param); cheap to carry through to session creation |
| "Open Session Notes" surfaced at the point of use (quest Details + Manage pages), not just a generic navbar link | Matches the actual DM workflow — they're already looking at the quest when they want notes, not browsing a global menu | LOW | Already validated UX from the old Phase 11 design; button lives inside the existing "DM Controls" card, not a new page |
| Foundation seam for future Omphalos → Quest Board calls (e.g. reading session status back) without building it now | PROJECT.md explicitly calls this out as an architectural non-blocker to leave open | LOW | This is just "don't paint yourself into a corner" — e.g. keep the quest↔session mapping queryable from both sides, keep CORS/config seams named generically rather than one-directionally. Do not build the reverse API itself this milestone |
| SuperAdmin-configurable per-instance URL/secret via existing Platform Settings pattern | Reuses an established, already-reviewed area (`/platform`, key-value settings) instead of inventing new admin UI | LOW | Direct reuse of Phase 29's Platform Settings pattern from v5.0 — not new design work |

### Anti-Features (Commonly Requested, Often Problematic — Named Explicitly for This Project's Scale)

These are standard advice for public-facing multi-tenant SaaS SSO. At ~17 members, self-hosted, one collaborator owning both repos, and a trusted-group threat model, they add real implementation and maintenance cost for protection against attackers who realistically aren't in this system's threat model.

| Feature | Why Requested | Why Problematic (at this scale) | Alternative |
|---------|---------------|------------------------------------|-------------|
| Full OAuth2/OIDC authorization-code flow between the two apps | "Real" SSO between two apps sounds like it should use the industry-standard protocol | Needs an authorization server, redirect URI allowlisting, client registration, token introspection or JWKS endpoint, refresh-token handling — multi-week build for two apps run by the same person/small group, replacing a link click with infrastructure | Signed short-lived HMAC redirect URL (already proven in the old Phase 11 code) — same security property (can't be forged without the shared secret) at a fraction of the code |
| Nonce store / used-token tracking table to prevent replay | Textbook advice for preventing token reuse ("what if someone intercepts the URL and replays it") | Requires a persistent store (DB table or cache) shared or duplicated across two apps on two different databases (SQL Server + PostgreSQL), plus cleanup/expiry jobs — for a link that (a) travels over HTTPS between a DM's own browser tabs, (b) is scoped to a specific quest+user+300s window, and (c) even if replayed, only grants access to a session that user's own DM role already permits them to see | Rely on the 300-second TTL alone. A replayed token is only exploitable by someone who already had network access to intercept an HTTPS request from a trusted group member within a 5-minute window — not a realistic threat for 17 people on a self-hosted deployment. If this needs revisiting, the trigger is a first real incident, not a checklist item |
| Real-time bidirectional session-state sync (Omphalos pushes live updates back to Quest Board, or vice versa) | Feels natural once both apps are "connected" — why not keep them in sync live? | Needs websockets/SignalR or polling infrastructure, conflict resolution for concurrent edits, and cross-repo coordination on two apps with independent release cadences and different owners | PROJECT.md already scopes this correctly: leave an architectural seam (don't block it), but don't build it. If Omphalos data needs to inform Quest Board later, a simple polled read-only API call is enough — not a sync engine |
| Full user account linking / identity federation UI (let users manually link/unlink their Quest Board and Omphalos accounts, view linked-account status, etc.) | Common in consumer SaaS ("Connect your Google account") | For 17 trusted users where auto-provisioning by a stable key is deterministic and admin-configured, a self-service linking UI is a feature nobody asked for solving a problem (ambiguous identity) that a well-chosen provisioning key avoids entirely | Auto-provision deterministically off a stable identifier (see Dependencies) with no user-facing linking screen; if a mismatch ever happens, it's an admin/DB fix, not a self-service flow to build |
| Per-group Omphalos configuration (multiple Omphalos URLs/secrets, one per Quest Board group) | Quest Board is now multi-tenant (v5.0), so it's tempting to make every integration setting per-group for consistency | Omphalos itself has no tenant/org concept — it's flat, `UserId`-scoped. A per-group Quest Board setting would have nothing on the Omphalos side to bind to; it would just be unused configuration surface | Instance-wide setting under `/platform`, configured once by SuperAdmin — already the direction PROJECT.md has settled on |
| Building a "reverse" full REST API (Omphalos → Quest Board) as part of this milestone, even just the read side | Once the token contract exists, extending it both ways feels like completing the picture | Doubles the design surface (auth, versioning, error handling) for a direction nothing in this milestone's goal requires; the stated goal is one-click navigation into Omphalos, not data sync | Leave the seam (e.g., don't hardcode assumptions that make a future reverse call awkward) but scope this milestone strictly to the outbound Quest Board → Omphalos handoff |
| Token encryption (JWE) on top of signing | "Signed AND encrypted" sounds more secure | The token payload (quest ID, quest title, username, expiry) contains nothing sensitive — no passwords, no PII beyond a display name already visible to any logged-in user. Encrypting adds a second secret/key-management burden for no confidentiality benefit; HMAC already prevents tampering | Signing only (HMAC-SHA256), as already proven in the old implementation. If quest titles could ever contain sensitive content, address that narrowly, not by encrypting the whole envelope |

## Feature Dependencies

```
Instance-wide Platform Settings (URL + shared secret)
    └──requires──> Existing /platform SuperAdmin area (v5.0, already built)

Signed redirect token generation (Quest Board side)
    └──requires──> Instance-wide Platform Settings (needs URL + secret to sign against)
    └──enables───> "Open Session Notes" buttons (quest Details/Manage pages)
    └──enables───> Omphalos SSO validation endpoint

Omphalos SSO endpoint: token validation
    └──requires──> Shared-secret contract agreed with Quest Board (same HMAC message format both sides)
    └──enables───> Auto-provision Omphalos user
    └──enables───> Find-or-create GameSession

Auto-provision Omphalos user
    └──requires──> Stable cross-app identity key (see note below — NOT yet decided)
    └──requires──> AuthService.GenerateToken(User) promoted from private to IAuthService
                       (or an equivalent cookie-issuing path added) — current method is private,
                       not callable from a new SsoService without this change

Find-or-create GameSession linked to a Quest Board quest
    └──requires──> Net-new ExternalQuestId (or equivalent) column/index on Omphalos's GameSession
                       (does not exist today — confirmed by source inspection)

"No manual login" end-to-end UX
    └──requires──> Signed redirect token generation
    └──requires──> Omphalos SSO endpoint (validation + auto-provision + session find-or-create)
    └──requires──> Omphalos issuing its normal omphalos_token cookie as the final step of the SSO
                       endpoint (reuse existing cookie-issuance code path, do not invent a second one)

Graceful degradation (hidden buttons, standalone Omphalos operation)
    └──enhances──> Every UI entry point above (LOW cost, must be threaded through consistently)
    └──conflicts with──> None — this is purely defensive and should never be skipped for time

Full OAuth2/OIDC ──conflicts──> Signed short-lived HMAC redirect URL
    (mutually exclusive design choices solving the same problem; do not attempt both)

Nonce/replay store ──conflicts with project scale──> 300-second TTL alone
    (the TTL is the entire replay-protection surface being recommended; a nonce store is
    redundant defense-in-depth this project's threat model doesn't justify)
```

### Dependency Notes

- **Auto-provision requires a stable cross-app identity key — this is an open design decision, not yet made.** The old implementation used `currentUser.Name` (a free-text display name), explicitly choosing it over `User.Identity.Name` because Quest Board's `UserName` is the email address and would leak email into the Omphalos username. But Quest Board's own `CLAUDE.md` decision log (see PROJECT.md Key Decisions) records that `User.Name` has **no uniqueness constraint** — any user can rename freely via `AccountController.Edit`, and this already caused a real ownership-check impersonation bug fixed in Phase 34.3 ("Ownership checks standardized on `User.Id` comparison, not `User.Name`"). Reusing a non-unique display name as the cross-app matching key for Omphalos user provisioning risks the same class of bug: two Quest Board users with colliding names would provision as (or hijack) the same Omphalos account. This needs to be resolved during phase design — likely by minting a stable, non-PII identifier (e.g. Quest Board's `User.Id` GUID, or a normalized+de-duplicated username) rather than reusing the mutable display name verbatim.
- **`AuthService.GenerateToken(User)` promoted from private to `IAuthService` requires** whoever builds the Omphalos-side SSO endpoint to touch `IAuthService`/`AuthService` — flagged in the old ROADMAP as "IAuthService.GenerateToken promotion," confirmed still true today since the method is still private in the current codebase.
- **Find-or-create GameSession requires** a schema change (EF Core migration) on the Omphalos side (PostgreSQL) — this is cross-repo coordination through normal PR review on that repo, not Quest Board's branch protection, per PROJECT.md's Constraints section.
- **Graceful degradation enhances every table-stakes UI entry point** and should not be treated as a separate late-stage feature — bake the `IsConfigured` check into each surface as it's built, the way Phase 10/11 already did successfully.

## MVP Definition

### Launch With (v1 — this milestone, per PROJECT.md's Active requirements)

- [ ] Instance-wide Platform Settings page (Omphalos URL + shared secret) — table stakes, reuses existing `/platform` pattern
- [ ] Signed, short-lived (5 min or similar) redirect token generation on Quest Board, applied consistently to every entry point (navbar + quest Details + quest Manage) — table stakes; explicitly re-verify no surface links the raw base URL unsigned (this exact bug shipped once already)
- [ ] Omphalos SSO endpoint: validate token → auto-provision user (with a decided, unique matching key) → find-or-create linked GameSession → issue normal `omphalos_token` cookie → redirect into the session — table stakes, this is the actual goal of the milestone
- [ ] Graceful hide of all Omphalos UI when unconfigured/disabled — table stakes, cheap, must not be skipped
- [ ] Expired/invalid-token friendly error handling on the Omphalos side — table stakes, routine occurrence at a short TTL
- [ ] Role enforcement independently on both sides (Quest Board gates the button; Omphalos independently validates the signature — never trust "the button was hidden") — table stakes
- [ ] Standalone-operation fallback: Omphalos boots and runs normally with the SSO env/config absent — table stakes, protects the other maintainer's ownership of that repo

### Add After Validation (v1.x)

- [ ] Quest title/date passed through into initial session naming — differentiator, cheap, but not required for the core loop to be considered done; sequence after the SSO round-trip works end-to-end at all
- [ ] Any UX polish on the redirect experience (loading state, "opening Omphalos..." interstitial) — only worth doing once the underlying flow is proven reliable

### Future Consideration (v2+ — explicitly out of scope this milestone per PROJECT.md)

- [ ] Bidirectional API (Omphalos → Quest Board reads) — PROJECT.md explicitly defers this; only the architectural seam should exist, not the implementation
- [ ] Any self-service account-linking UI — not needed while provisioning is deterministic and the group is small/trusted
- [ ] Per-group Omphalos configuration — defer indefinitely unless Omphalos itself grows a tenant concept

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Platform Settings (URL + secret) | HIGH | LOW | P1 |
| Signed token generation, all entry points consistent | HIGH | MEDIUM | P1 |
| Omphalos SSO validation endpoint | HIGH | MEDIUM | P1 |
| Auto-provision Omphalos user (stable key) | HIGH | MEDIUM | P1 |
| Find-or-create GameSession + ExternalQuestId | HIGH | MEDIUM | P1 |
| Graceful hide when unconfigured | HIGH | LOW | P1 |
| Expired-token friendly handling | MEDIUM | LOW | P1 |
| Role enforcement on both sides | HIGH | LOW | P1 |
| Standalone-operation fallback | MEDIUM | LOW | P1 |
| Quest title/date passthrough to session naming | MEDIUM | LOW | P2 |
| Bidirectional API seam (not implementation) | LOW (now) | LOW | P2 |
| OAuth2/OIDC full flow | LOW (at this scale) | HIGH | P3 — do not build |
| Nonce/replay store | LOW (at this scale) | MEDIUM | P3 — do not build |
| Real-time bidirectional sync | LOW (not asked for) | HIGH | P3 — do not build |
| Self-service account linking UI | LOW (at this scale) | MEDIUM | P3 — do not build |
| Per-group Omphalos config | LOW (nothing to bind to) | MEDIUM | P3 — do not build |
| Token encryption (JWE) on top of signing | LOW (no sensitive payload) | LOW-MEDIUM | P3 — do not build |

**Priority key:**
- P1: Must have — this is the milestone's actual scope per PROJECT.md
- P2: Should have if time allows, sequenced after P1 works end-to-end
- P3: Explicitly out of scope — named here so it doesn't get re-proposed mid-milestone as "obviously needed"

## Sources

- Direct inspection: `C:\Repos\quest-board\.planning\PROJECT.md` (current milestone scope, constraints, Omphalos facts, key decisions log including the `User.Name` uniqueness bug)
- Direct inspection: `C:\Repos\omphalos\src\Omphalos.Services\Implementations\AuthService.cs`, `AuthEndpoints.cs`, `Omphalos.Domain\Entities\GameSession.cs` (current auth/session model, confirmed no `ExternalQuestId` exists)
- Prior implementation (real code, not hypothesis): `origin/milestone/3-omphalos-integration` branch, Phase 10 (Admin Settings) and Phase 11 (Navigation + Token Generation) — completed and code-reviewed; `11-REVIEW.md` WR-01 finding (unsigned navbar link bypassing SSO) directly informed the table-stakes list; `11-RESEARCH.md`/`11-CONTEXT.md` for the locked HMAC canonical message format and TTL; old `ROADMAP.md` for the never-built Phase 20 (Omphalos SSO endpoint) scope and its "standalone operation when env var absent" success criterion
- [HMAC Secrets Explained: Authentication You Can Actually Implement](https://blog.gitguardian.com/hmac-secrets-explained-authentication/) — HMAC as the right tool for 1:1 controlled-both-ends integrations (MEDIUM confidence, cross-checked against Tyk/Okta HMAC docs)
- [Tyk Documentation — Sign Requests with HMAC](https://tyk.io/docs/basic-config-and-security/security/authentication-authorization/hmac-signatures) — canonical-string signing pattern
- [Okta — HMAC Definition](https://www.okta.com/identity-101/hmac/) — HMAC fundamentals
- [Curity — JWT Security Best Practices Checklist](https://curity.io/resources/learn/jwt-best-practices/) — short-lived token as primary replay mitigation (MEDIUM confidence)
- [GitHub node-jsonwebtoken — Prevention against replay attacks discussion](https://github.com/auth0/node-jsonwebtoken/issues/36) — nonce stores as one option among several, not universally required (LOW-MEDIUM confidence, community discussion not official doc)
- [Primer (GitHub Design System) — Degraded Experiences](https://primer.style/product/ui-patterns/degraded-experiences/) — hide UI elements rather than show broken/error states when a dependency is unavailable (MEDIUM confidence, official design-system documentation)
- [AWS Well-Architected — REL05-BP01 Graceful Degradation](https://docs.aws.amazon.com/wellarchitected/latest/reliability-pillar/rel_mitigate_interaction_failure_graceful_degradation.html) — soft-dependency pattern (HIGH confidence, official AWS docs)

---
*Feature research for: Cross-app SSO handoff (Quest Board → Omphalos)*
*Researched: 2026-07-02*
