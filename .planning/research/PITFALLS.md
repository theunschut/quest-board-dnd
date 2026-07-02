# Pitfalls Research

**Domain:** Lightweight signed-token SSO bridge between two independently-deployed, pre-existing production apps (Quest Board → Omphalos)
**Researched:** 2026-07-02
**Confidence:** HIGH (grounded in direct reading of both codebases) / MEDIUM (general HMAC/SSO web-security claims, corroborated against 2+ authoritative sources each)

## Codebase Facts That Drive Every Pitfall Below

These aren't pitfalls themselves, but every pitfall below is a direct consequence of one of these facts, verified by reading both repos:

| Fact | Quest Board | Omphalos |
|---|---|---|
| Identity store | ASP.NET Core Identity, `UserEntity : IdentityUser<int>` | Hand-rolled `User` entity, `Guid Id`, bcrypt hash |
| Username lookup | Identity's `NormalizedUserName` — **uppercased**, SQL Server default collation is case-insensitive anyway | `db.Users.FirstOrDefaultAsync(u => u.Username == username)` — **exact C# string equality translated to Postgres `=`, which is case-sensitive under default `en_US.UTF8` collation** |
| Display name uniqueness | `UserEntity.Name` has **no uniqueness constraint** (confirmed by PROJECT.md decision log: a prior bug let one user impersonate another via display-name collision — fixed by switching ownership checks to `User.Id`) | `Username` has no explicit unique index visible in the entity (needs migration check before relying on it) |
| Auth transport | Cookie auth (ASP.NET Identity), `SameSite`/`Secure` managed by framework defaults | `omphalos_token` httpOnly cookie, **`SameSite=Lax`, `Secure` is commented out** (`AuthEndpoints.cs:15`, literal TODO) |
| Deployment topology | Traefik → App CT, own subdomain, Let's Encrypt TLS | Separate Docker Compose stack, own reverse proxy termination — "No HTTPS in the container; terminate TLS at the reverse proxy" (CLAUDE.md) — **different domain, different cookie jar** |
| JWT signing secret | N/A (not yet built) | `Jwt:Secret` in `appsettings.json` defaults to a **placeholder string committed to source** (`"change-me-in-production-minimum-32-chars!!"`), overridden by `Jwt__Secret` env var in Docker Compose with a hard-fail `:?` guard |
| Session entity | N/A | `GameSession.Id` is a **client-generated string**, no server-side identity/sequence — find-or-create semantics are entirely the caller's responsibility |
| Repo ownership | Quest Board author is primary maintainer | Quest Board author is a **collaborator**, not the owner (`ghcr.io/jasonjamesk/omphalos`); PRs go through someone else's review, someone else's CI, someone else's release cadence |
| CI on Omphalos | N/A | `dotnet build` (Release) + `npm run build` on every push/PR — **no test suite currently runs in CI**, so a broken SSO endpoint can merge without an automated safety net catching it |

---

## Critical Pitfalls

### Pitfall 1: HMAC canonical message is ambiguous or under-scoped, letting tokens be forged, replayed, or reinterpreted across fields

**What goes wrong:**
The Quest Board side builds a token as `{payload} + HMAC-SHA256(secret, someString)`. If `someString` is built by naive concatenation (`userId + username + timestamp`), an attacker (or just a coincidence) can shift a character from one field to the next and produce the *same concatenated string* with *different field values* — e.g. `userId="12" username="3abc"` collides with `userId="123" username="abc"`. This is a classic canonicalization attack: the MAC is computed over the *serialized bytes*, not over the *semantic fields*, so anything that maps two different field-sets to the same byte string breaks the "the MAC proves these exact field values" guarantee. Related failure modes in the same family:
- Field order not fixed → producer and consumer canonicalize differently → intermittent verification failures that look like "flaky" bugs, not security bugs, and get silenced with retries instead of fixed
- Optional/nullable fields (e.g. `groupId` might be absent for some DMs pre-multi-tenancy edge cases) serialized inconsistently (`""` vs omitted vs `null`) between Quest Board's serializer and Omphalos's parser
- Expiry timestamp included in the payload but **not included in the MAC input** — attacker can extend a token's life by editing the plaintext expiry claim while the signature still validates
- Redirect target / intended-audience not bound into the MAC — a token minted for "open DM Tool" and a token minted for "open session notes for quest 42" become interchangeable if the MAC doesn't cover which action/resource the token authorizes

**Why it happens:**
Both codebases already have a battle-tested JWT stack (Omphalos: `System.IdentityModel.Tokens.Jwt` + `Microsoft.IdentityModel.Tokens`, used for its own login flow). It's tempting to hand-roll a leaner token *specifically because* JWT feels like overkill for "just open this URL" — but hand-rolling reintroduces exactly the canonicalization bugs that JWT's structured, delimiter-safe encoding (base64url segments joined by literal `.`) was designed to avoid. The old abandoned `milestone/3-omphalos-integration` branch reportedly had an HMAC token-format design already (per PROJECT.md) — reusing its *design* without re-verifying the canonical string construction against the current field set (which now includes `groupId` post-multi-tenancy) is a live risk.

**How to avoid:**
- Do not hand-roll delimiter-based concatenation. Use a format with **unambiguous, length-prefixed or structurally-delimited encoding** — either (a) reuse JWT (`System.IdentityModel.Tokens.Jwt`, already a dependency on the Omphalos side and trivially added on Quest Board's side) with `HmacSha256` signing so the canonical form is JWT's own compact serialization, not custom string-building, or (b) if staying with a custom format, serialize the payload as JSON with a **fixed, documented property order and explicit UTF-8 encoding**, then MAC the raw JSON bytes — never MAC a manually concatenated string of field values.
- Include **every field that affects authorization decisions** inside the MAC'd payload: user identity, expiry, intended action/resource (quest ID or "open DM tool" sentinel), and issue time. Nothing outside the MAC should be trusted by the verifier.
- Add an explicit **audience/purpose claim** (e.g. `"aud": "omphalos-sso"`) so a token can't be replayed for a different purpose even if the same secret is reused elsewhere later.
- On the Omphalos side, verify the MAC using a **constant-time comparison** (`CryptographicOperations.FixedTimeEquals` in .NET, not `==` or `string.Equals`) to avoid timing side-channels leaking the correct signature byte-by-byte. This is a one-line fix but easy to miss when hand-rolling verification instead of using `JwtSecurityTokenHandler.ValidateToken`, which already does this internally.
- Write the canonical-format spec down as a shared artifact (a short doc or even just a code comment with an example token) *before* implementing both sides independently — since Quest Board and Omphalos are different codebases maintained with different review processes, an informal "I'll just match what the other side does" approach is how the two implementations silently drift.

**Warning signs:**
- Token verification works in local dev (same machine, same .NET version, same string encoding defaults) but fails intermittently in production
- Any code that does `string.Join("", field1, field2, ...)` or `field1 + field2` before hashing
- Expiry or audience checked *after* signature verification passes, using values read from the same untrusted payload the signature was supposed to protect — if those values aren't part of the MAC input, they're attacker-controlled
- Signature comparison uses `==`, `.Equals()`, or `SequenceEqual` on a hash instead of `CryptographicOperations.FixedTimeEquals`

**Phase to address:**
Whichever phase defines the token format (should be the **first** Omphalos-integration phase, before either side writes code) — this is a shared-contract design decision, not an implementation detail, and must be pinned down and written into both codebases' plans identically before Quest Board starts minting tokens or Omphalos starts validating them.

---

### Pitfall 2: Cross-store identity matching silently creates duplicate or unmatchable accounts due to case-sensitivity mismatch

**What goes wrong:**
Quest Board's user lookup goes through ASP.NET Core Identity's `NormalizedUserName`, which is **always uppercased** before comparison — so `TheDM`, `thedm`, and `THEDM` are the same user in Quest Board. Omphalos's `UserRepository.GetByUsernameAsync` does `db.Users.FirstOrDefaultAsync(u => u.Username == username)`, translated to Postgres's `=` operator, which under the default `en_US.UTF8`/`C` collation is **case-sensitive**. If Quest Board sends `"TheDM"` in the token on one login and `"thedm"` on another (e.g. because the DM's Quest Board display name changed case, or because the token uses `Email` on one code path and `UserName` on another), Omphalos's auto-provisioning will create **two separate `User` rows** for the same person — each with their own `GameSession` history, meaning the DM's session notes silently fork and appear to "disappear" depending on which row gets matched that day.

A second, distinct failure mode: Quest Board's `Name` field (display name) has **no uniqueness constraint** — this was an actual bug fixed in Phase 34.3 where display-name collisions allowed impersonation. If the SSO token uses `Name` instead of `UserName`/`Email` as the identity key sent to Omphalos, two different Quest Board users could collide into the same Omphalos account, or worse, one user could rename themselves to match another DM's display name and hijack that DM's Omphalos session history.

**Why it happens:**
The two systems were built independently with different identity primitives (`int` IdentityUser vs `Guid` hand-rolled `User`), and "just pass the username across" looks like the obvious, low-effort bridge. The case-sensitivity mismatch is invisible in typical manual testing because most people type their own username consistently — it only surfaces when a user's stored casing differs from what gets typed/displayed somewhere in the token-minting code path, or when Omphalos's Postgres collation is anything other than a case-insensitive `ICU` collation (which is not the Postgres default).

**How to avoid:**
- Use a **stable, immutable, already-unique identifier** as the cross-system key — not `Name` (no uniqueness constraint) and not raw `UserName` (mutable, case-ambiguous across stores). `Email` is the best available candidate on the Quest Board side since ASP.NET Identity normalizes and typically enforces email uniqueness — confirm `RequireUniqueEmail` is actually set in Identity options before relying on this.
- On the Omphalos side, **normalize the incoming identity value before lookup and before insert** — lowercase (or uppercase, but pick one and be consistent) both at write time and read time, so `GetByUsernameAsync`/auto-provision logic is doing a normalized comparison regardless of Postgres collation. Do not rely on database collation to do this for you — it's inconsistent across Postgres versions/locales and the current schema doesn't declare one.
- Add a **unique index** on the normalized identity column on the Omphalos side as a hard backstop (migration required) — this turns a silent duplicate-row bug into a loud constraint-violation exception the first time it would have happened, which is far cheaper to debug.
- Decide explicitly whether the mapping key is the Quest Board `UserId` (int) embedded as an opaque external ID, or the email/username string, and store whichever is chosen as a **dedicated column** on the Omphalos `User` entity (e.g. `QuestBoardUserId` or `ExternalId`) rather than overloading `Username` — this also sidesteps the case-sensitivity problem entirely for subsequent lookups after first provisioning, since the second and later logins match on the immutable external ID, not on a re-parsed username string.

**Warning signs:**
- Omphalos admin panel (`GetAllAsync`, ordered by username) shows two near-identical usernames differing only by case
- A DM reports "my session notes are empty" after previously having content — likely landed on a freshly auto-provisioned duplicate account instead of their original one
- No unique index exists on `Users.Username` in the Omphalos migrations (verify directly — `20260612120704_InitialSchema.cs` is the place to check before building on top of it)

**Phase to address:**
The phase that implements Omphalos's auto-provisioning endpoint — must decide and document the cross-system identity key **before** writing the "find or create user" logic, and should add the backstop unique index in the same migration that adds any new mapping column.

---

### Pitfall 3: Shared secret stored/rotated insecurely, including a blank-field-overwrite bug in the new Platform Settings admin UI

**What goes wrong:**
Two distinct secret-handling risks converge here:

1. **Committed placeholder secret on the Omphalos side.** `appsettings.json` ships `"Jwt:Secret": "change-me-in-production-minimum-32-chars!!"` as a literal, committed default. The production Docker Compose correctly requires `JWT_SECRET` via `:?` (hard-fails if unset) — but if the *new* SSO shared secret is added the same way as a second `appsettings.json` placeholder without an equivalent `:?` guard in `docker-compose.prod.yml`, a production deploy could silently run with the placeholder value, and Quest Board's SuperAdmin might never notice because the SSO flow would still "work" — tokens signed with the placeholder secret would verify fine locally but be trivially forgeable by anyone who has read the public Omphalos repo's `appsettings.json` history.
2. **Blank-field-overwrite bug in the new Platform Settings page.** Quest Board is building a brand-new key-value settings store for this milestone (no existing pattern to copy — `Areas/Platform` currently only has `GroupController`). A very common admin-UI bug: the edit form pre-fills the secret field with the *current* value (or leaves it blank for security, showing only "•••• configured"), and on save, a blank field is naively treated as "the new value is empty" and **overwrites the working secret with an empty string** — silently breaking the SSO integration until someone notices tokens stopped validating. Quest Board's own codebase has prior art for getting this right elsewhere (`AdminController.EditUser` explicitly checks `emailChanged` before touching `user.Email`, avoiding a similar clobber) — but that pattern must be deliberately carried into the new Settings controller, not assumed.

**Why it happens:**
Secret-bearing admin forms are usually built by copy-pasting a normal "edit these text fields" CRUD form, and the natural default behavior of `model.Field = postedValue` doesn't distinguish "admin wants to clear this" from "admin left it alone because the field is masked/empty by design." Because there's no existing Platform Settings precedent in this codebase, whoever implements it has no established convention to follow and has to get this right from scratch under time pressure.

**How to avoid:**
- On the Quest Board settings form: **never pre-populate the secret field with the actual value.** Show a masked placeholder ("configured, unchanged" / a fixed dot pattern) and only overwrite the stored secret if the submitted field is non-empty **and** the admin has explicitly interacted with it (e.g. a separate "Change secret" checkbox/button that unmasks an empty input, mirroring how password-change forms typically work) — same "don't touch what wasn't changed" logic as the existing `EditUser` email-change guard.
- Store the secret using the **same encryption-at-rest posture as other production secrets** in this deployment (the codebase's existing pattern is plain environment variables outside source control, e.g. `EmailSettings__SmtpPassword` in `/etc/questboard/env`) — if the new key-value settings store persists to the database instead, that's a meaningfully different security posture (DB backups now contain the shared secret in plaintext) and should be a deliberate, documented decision, not an accident of "it was easy to add a DB table."
- On the Omphalos side, require the SSO shared secret via the same `:?`-guarded env var pattern already used for `JWT_SECRET`/`POSTGRES_PASSWORD` — fail loud at startup if missing, don't fall back to a committed placeholder.
- Plan for **rotation** even if not implemented immediately: document the manual rotation procedure (update both sides' config, restart both services) since there's no automated key-exchange mechanism in a lightweight HMAC bridge. A rotation with only one side updated causes 100% SSO failure until both are in sync — this should be a known, written-down runbook step, not discovered during an incident.
- Never log the secret or the full token. Confirm any new logging added for debugging the SSO flow follows the codebase's existing "Resend Bearer-token and secret-safe logging patterns" (already documented per PROJECT.md's Phase 34.1 entry) rather than reinventing logging for this feature.

**Warning signs:**
- Settings edit form shows the actual secret value in a visible/editable input on page load
- Saving the settings form with an unrelated field change (e.g. just updating the Omphalos URL) also silently changes or clears the secret
- No unique/required validation preventing the secret field from being saved as empty
- `docker-compose.prod.yml` style `:?` guard exists for other Omphalos secrets but not for the new SSO one

**Phase to address:**
The phase building the Platform Settings admin page (Quest Board side) for the blank-overwrite bug; the phase adding the SSO secret to Omphalos's config/env plumbing for the committed-placeholder risk. Both should be verified in the same milestone since they're two halves of the same secret's lifecycle.

---

### Pitfall 4: Cookie SameSite/Secure assumptions break because Quest Board and Omphalos are on different domains

**What goes wrong:**
This is a **token handoff via redirect**, not a shared-cookie SSO — Quest Board and Omphalos are deployed as entirely separate services behind (likely) separate Traefik-routed subdomains with independent TLS termination (confirmed: Quest Board's `server-setup.md` shows its own Traefik router/domain; Omphalos's CLAUDE.md says "No HTTPS in the container; terminate TLS at the reverse proxy" — implying its own, separate reverse-proxy entry). Cookies set by Quest Board are **never visible** to Omphalos and vice versa — this is correct and expected, not a bug to fix. The actual risk is the opposite direction: developers reach for `SameSite=None` or loosen cookie policy on one side "to make SSO work" without realizing the token is carried in the URL/query string across the redirect, not via a cookie at all. Loosening `SameSite` unnecessarily widens CSRF exposure on unrelated parts of each app.

The concrete, already-visible landmine: Omphalos's own `omphalos_token` cookie is `SameSite=Lax` with **`Secure` commented out** (`AuthEndpoints.cs:15`, literal `// Secure = true ← enable once behind HTTPS` TODO still unresolved). If the SSO-provisioned session relies on this cookie being set after the handoff completes, and production Omphalos is served over HTTPS (which it should be, behind Traefik) but the `Secure` flag is still off, the cookie is sent in cleartext-vulnerable configurations and — depending on browser version and exact `SameSite=Lax` behavior — may not reliably persist across the top-level navigation the SSO redirect performs. This is a pre-existing bug unrelated to Quest Board's changes, but the SSO milestone is what will finally exercise this exact cookie-issuance code path in earnest (previously only hit via direct `/api/auth/login` on Omphalos's own login form) and is therefore where it will actually get noticed and must be fixed as a blocking prerequisite.

**Why it happens:**
"SSO" strongly primes engineers to think in terms of shared session cookies (that's how same-site SSO usually works), but this integration is explicitly NOT that — it's a one-time signed handoff followed by Omphalos establishing its own independent session. Conflating the two models leads to wasted effort trying to make cookies cross domains (which correctly-configured browsers will block regardless of `SameSite` settings, since it's not just a `SameSite` issue but full third-party-cookie/domain isolation) instead of focusing on what actually needs to work: token-in-URL → validate → mint Omphalos's own first-party cookie.

**How to avoid:**
- Treat the handoff as: Quest Board redirects the browser to `https://omphalos.example.com/sso?token=...` (full top-level navigation, GET). Omphalos validates the token server-side, auto-provisions if needed, and **sets its own first-party `omphalos_token` cookie** scoped to its own domain — exactly like a normal login, just triggered by token validation instead of a password check. No cross-domain cookie sharing is needed or possible.
- Fix the `Secure` flag TODO on Omphalos's cookie as an explicit **prerequisite** for this milestone, gated on confirming production Omphalos is actually served over HTTPS (it should be, per its own "terminate TLS at reverse proxy" note) — flip `Secure = true` unconditionally in production config, not just for the new SSO-issued cookie but for all Omphalos auth cookies, since this SSO milestone is what finally puts real, security-conscious eyes on that code path.
- Put the token in the URL as a **short-lived, single-use** query parameter — never in a cookie during the handoff itself, since cookies can't cross the domain boundary anyway.
- Confirm Quest Board's redirect uses a real `Location:` redirect (full navigation) and not an AJAX/fetch call, since a fetch call would be subject to CORS and would need Omphalos's `AllowedOrigins` CORS config (currently empty/inert) — full-page redirects sidestep CORS entirely, which is simpler and is very likely the intended design already implied by "DMs click a button... get redirected."
- Double-check `SameSite=Lax` (not `Strict`) is correct for Omphalos's own cookie once set — `Lax` allows the cookie to be included on top-level GET navigations arriving from another site, which matters if Omphalos ever needs to read its own just-set cookie immediately after a cross-site redirect lands (it does, for the subsequent page load). `Strict` would be actively wrong here and cause the classic "logged in but every page looks logged out until you click something" symptom.

**Warning signs:**
- Any code attempting to read a Quest Board cookie value from Omphalos, or pass Quest Board's ASP.NET Identity auth cookie in the redirect
- `SameSite=None; Secure` added to a cookie "to fix" the handoff — a sign the token-in-URL model was abandoned in favor of trying to share cookies, which will not work across distinct domains and unnecessarily weakens CSRF protection
- Testing only done over `http://localhost` where `Secure` cookie issues are invisible (cookies work fine on `http://localhost` even with `Secure=true` in some browsers due to the localhost exemption — this can mask a `Secure` misconfiguration that then breaks in real production HTTPS-to-HTTPS flows, or more commonly, mask the *absence* of `Secure` never being caught because localhost testing "just works" either way)

**Phase to address:**
The phase implementing the Omphalos-side SSO validation endpoint should fix the `Secure` cookie flag as a blocking prerequisite change (small, isolated, but touches shared auth code — needs its own PR and careful review from the Omphalos maintainer since it changes existing login behavior, not just new SSO code). The redirect-construction logic on the Quest Board side should be reviewed to confirm it never touches or forwards Quest Board's own auth cookie.

---

### Pitfall 5: Auto-provisioning creates orphaned or mis-privileged Omphalos accounts

**What goes wrong:**
Omphalos's `User.Role` is a simple `enum { Player, Admin }`, and `AdminOnly` policy gates Omphalos's own admin endpoints. Quest Board's roles are `Admin`, `DungeonMaster`, `Player`, now further complicated by the v5.0 multi-tenancy work (group-scoped roles, `SuperAdmin`). When Omphalos auto-provisions a user from an incoming SSO token, the role-mapping decision is a legitimate design question with several wrong answers:
- **Defaulting every auto-provisioned user to Omphalos `Admin`** because "they're a DM, DMs need full access" — this is over-broad if Omphalos's `Admin` role controls things like deleting other users' data or global settings that a Quest Board DM shouldn't touch, especially since Omphalos has **no tenant/group concept** (flat, `UserId`-scoped data) — an over-privileged auto-provisioned account could see/manage things belonging to the Omphalos maintainer's own unrelated use of the app.
- **Defaulting to `Player`** silently blocks the DM from features they need, producing a confusing "SSO worked but nothing works" experience with no clear error.
- **Provisioning happens on every SSO click**, not just the first — if the provisioning logic re-runs `CreateAsync` (or an unguarded upsert) instead of a proper find-then-create-else-noop, repeated logins could reset role/settings back to defaults each time, clobbering any role changes Omphalos's own maintainer made manually to that account afterward.
- **Access control drift**: since Quest Board's own PROJECT.md history includes a real bug where role checks were bypassed via display-name collision (Phase 34.3 fix) and another where `RequireActiveGroupId()` guards were incompletely wired across ~13 call sites — this project has a demonstrated pattern of authorization logic being easy to under-apply consistently. The new Omphalos-side "does this token grant access" check needs the same scrutiny: confirm it's applied at every new SSO-reachable endpoint, not just the primary one.

**Why it happens:**
Auto-provisioning is usually designed around the happy path (first login, sensible defaults) and under-tested for the update path (Nth login, role already diverged, what happens to an existing manually-edited Omphalos account that happens to share the matched identity).

**How to avoid:**
- Map role explicitly and conservatively: only Quest Board `Admin`/`DungeonMaster` (or `SuperAdmin`) should map to Omphalos `Admin`; everyone else should probably not even be provisioned at all if Omphalos has no meaningful `Player`-level use case reachable via this SSO flow — confirm with the actual feature scope ("Open DM Tool" / "Open Session Notes" — both DM-facing) whether Player-role Quest Board users should ever reach this flow in the first place. If the button is only ever shown to DMs/Admins on the Quest Board side, the Omphalos endpoint should still independently enforce that the token's role claim is DM/Admin-equivalent — never trust that the UI-level "button is hidden for players" is the only enforcement.
- Make provisioning **idempotent and additive-only on re-login**: find-by-external-identity first; if found, do not overwrite `Role` (an Omphalos-side manual promotion/demotion by its maintainer should stick); if not found, create with the mapped role from the token.
- Since `GameSession.Id` is client-generated with no server sequence, the "find or create the matching quest session" logic must use a **deterministic, collision-safe ID derivation** from the Quest Board quest ID (e.g. a stable prefixed string like `"qb-{questId}"`) — not a random ID — so repeated clicks on "Open Session Notes" for the same quest land on the same session instead of spawning duplicates. Verify this against `SessionService.UpsertAsync`'s actual upsert semantics (keyed on `Id`) before assuming it's safe.
- Log (without secrets) every auto-provisioning event on the Omphalos side during initial rollout — this is a new, security-relevant code path that has zero production history; a short observation period after shipping catches role-mapping mistakes before they compound.

**Warning signs:**
- A DM reports Omphalos features are missing/greyed out after SSO login despite having full DM rights in Quest Board
- Omphalos admin user list shows more `Admin`-role users than expected after the SSO feature ships
- Duplicate `GameSession` rows for what should be the same quest, distinguishable only by creation timestamp

**Phase to address:**
The phase implementing Omphalos's auto-provisioning and session find-or-create logic. Role-mapping policy should be written down explicitly (a small table: Quest Board role → Omphalos role) as part of that phase's plan, not left as an implicit code-level decision.

---

### Pitfall 6: Cross-repo collaboration friction turns a small feature into a stalled PR — migration conflicts, breaking changes to shared entities, and review latency on someone else's timeline

**What goes wrong:**
This milestone requires shipping real, non-trivial changes to a repository the Quest Board author does not own (`ghcr.io/jasonjamesk/omphalos` — a different GitHub identity than this project's own `theunschut/dnd-quest-board`). Concretely, based on what's actually in the Omphalos codebase:
- **New EF Core migration conflicts.** Omphalos has exactly 3 migrations to date, all closely spaced (`InitialSchema`, `AddEncounterEnemies`, `AddGlobalLocations`/`AddGlobalCharacters` — all mid-June). If the maintainer is independently adding their own schema changes (new features unrelated to SSO) while this integration branch is in flight, **migration ordering conflicts** are likely — EF Core migrations are strictly ordered by timestamp/history, and two branches both adding migrations off the same base will conflict on `OmphalosDbContextModelSnapshot.cs` (a single generated file both migrations must agree on) even if the actual schema changes don't logically overlap. This is a well-known EF Core multi-branch pain point, worse than typical merge conflicts because the snapshot file is derived, not hand-editable in a sane way — resolving it usually means regenerating migrations against latest `main`, not a manual line-level merge.
- **Breaking changes to `User` or `GameSession`, entities the maintainer's own code depends on everywhere.** Adding an external-identity column to `User` (Pitfall 2) or changing `GameSession.Id` semantics (Pitfall 5) touches entities referenced throughout `Omphalos.Services`, `Omphalos.Repository`, and the React frontend's `db/index.js` API layer. A change that looks self-contained from Quest Board's side ("just add one nullable column") can still require the maintainer to reason about every existing code path that constructs a `User` or `GameSession`, increasing review scope and the chance of requested changes.
- **PR review happens on someone else's schedule, using someone else's judgment.** Unlike Quest Board's own branch-protected `main` (where the author controls merge timing), Omphalos PRs are subject to normal review by the actual owner, who may want different tradeoffs (e.g. a different role-mapping default, a different secret-storage location, objections to adding an SSO-specific dependency). CLAUDE.md's constraint already states this plainly: "changes there go through normal PR review on that repo, not this one's branch protection." No test suite runs in Omphalos's CI today — meaning the maintainer's review is the *only* safety net for this change, raising the bar for how carefully the PR needs to be scoped and explained.
- **Divergence risk repeats the exact failure mode that caused this milestone to be "redone from scratch."** PROJECT.md documents that the *previous* Omphalos integration attempt (`milestone/3-omphalos-integration`) was abandoned because its branch "diverged too far from `main` to merge" after other milestones landed. A long-lived Omphalos-side branch sitting unreviewed while the maintainer ships unrelated work risks the exact same fate on the Omphalos side this time.

**Why it happens:**
It's easy to plan Omphalos-side work using the same assumptions as Quest Board-side work (author has merge authority, can iterate freely, controls timing) when in fact every Omphalos-side phase has an external dependency: another person's availability and judgment. This mismatch tends to surface late — after code is written — rather than being planned for up front.

**How to avoid:**
- **Scope Omphalos-side changes to the absolute minimum needed**, and prefer additive, backward-compatible schema changes (new nullable columns, new endpoints) over anything that changes existing entity shape or behavior — this reduces both migration-conflict surface and review burden. Do not bundle unrelated cleanup/refactoring into the same PR.
- **Open the Omphalos PR early, in small increments**, rather than one large PR at the end of the milestone — e.g. a first PR that just adds the CORS origin config value and the new nullable identity column (pure additive, trivially reviewable), followed by a second PR for the SSO endpoint itself once the maintainer has had a chance to weigh in on the schema shape. This directly mitigates the "diverged too far to merge" failure mode by keeping the branch short-lived.
- **Rebase/regenerate the Omphalos migration immediately before opening the PR**, not once at the start of the milestone — check `git log` on Omphalos's `main` for new migrations right before finalizing, since the 3-migrations-in-a-week pace suggests active development that could add a conflicting migration at any time.
- **Communicate the design decisions in Pitfall 1, 2, and 5 (canonical token format, identity-matching key, role mapping) to the maintainer before writing Omphalos-side code**, not after — these are exactly the kind of decisions a maintainer is likely to have opinions about, and discovering disagreement after code is written costs a full rewrite-and-re-review cycle instead of a short conversation.
- **Since no tests run in Omphalos CI**, add focused tests for the new SSO endpoint as part of the PR itself (even if the rest of the repo has none) — this both protects the change going forward and demonstrates rigor that speeds up review from a maintainer who has no existing test-suite safety net to lean on.
- Treat "PR review friction" as a **schedule risk to plan around**, not an engineering problem to solve — build slack into the milestone's expectations for the Omphalos-side phases specifically, since their completion depends on someone else's response time.

**Warning signs:**
- Omphalos-side branch has been open/unmerged for more than a few days while Quest Board-side work continues in parallel, without a check-in on review status
- `git fetch` on Omphalos shows new commits on `main` that weren't authored by this milestone's work
- The PR diff touches `User.cs`, `GameSession.cs`, or `OmphalosDbContext.cs` in ways beyond strictly additive columns/tables
- No response/review activity on the PR after the initial open — treat silence as a signal to proactively follow up, not as tacit approval

**Phase to address:**
Should be an explicit, separate phase (or the first sub-step of the first Omphalos-touching phase): "design review with Omphalos maintainer" before implementation begins. Every subsequent Omphalos-side phase should budget for PR-review turnaround as part of its own timeline, not assume same-day merge like Quest Board's own branch-protected flow.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Hand-roll HMAC string concatenation instead of using JWT | Feels "lighter weight," fewer dependencies | Canonicalization bugs (Pitfall 1) are subtle and can sit unnoticed until exploited or until a field is added later and breaks the format silently | Never — the dependency (`System.IdentityModel.Tokens.Jwt`) is already present on the Omphalos side and trivial to add on Quest Board's; there is no real savings |
| Use `Username` string as the cross-system join key instead of a dedicated external-ID column | One less migration, ships faster | Silent duplicate accounts on case mismatch (Pitfall 2); expensive to detect and merge after the fact once both systems have diverging data under the duplicate rows | Only acceptable for a throwaway prototype never exposed to real users — not acceptable here since both apps are already in production use |
| Store the shared secret as a DB-backed Platform Setting instead of an env var | Matches the "key-value store" UX described in the milestone goal, editable without a redeploy | Plaintext secret now lives in DB backups, accessible to anyone with DB access (wider blast radius than env-var-only secrets in this codebase's existing pattern) | Acceptable only if paired with the blank-overwrite-safe UI (Pitfall 3) and the team accepts DB backups as a secret-storage boundary already covered by existing backup security controls — otherwise store the secret in the env-file pattern and keep only the URL in the DB-backed setting |
| Auto-provision every SSO login with an unconditional upsert | Simple to implement, one code path | Clobbers manual role/data changes made directly in Omphalos between logins (Pitfall 5) | Never for `Role` or other admin-mutable fields; acceptable only for fields that are meant to always mirror Quest Board's current state (e.g. display name sync, if desired) |
| Skip writing Omphalos-side tests for the SSO endpoint since the rest of the repo has none | Matches existing repo convention, less work | The one new code path most likely to be security-sensitive ships with zero automated safety net, and sets a bad precedent right as a security-relevant feature is added | Never — this is exactly the code that most needs tests, precedent or not |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|-------------------|
| Omphalos CORS (`AllowedOrigins`) | Assuming CORS needs to be configured for the SSO flow at all | Full-page redirect handoffs don't need CORS — only configure `AllowedOrigins` if a *future* bidirectional API (already flagged as "foundation, not built now" in PROJECT.md) needs browser-side fetch calls between the two origins |
| Omphalos JWT auth (`JwtBearerEvents.OnMessageReceived`) | Assuming the new SSO token can piggyback on the existing JWT bearer scheme unmodified | The existing scheme reads `omphalos_token` from a cookie already issued by Omphalos's own login; the new SSO flow needs a distinct code path (a dedicated endpoint that validates the *inbound* Quest Board token and then *issues* Omphalos's own cookie) — these are two different tokens with two different secrets and must not be conflated |
| Omphalos `AllowedHosts: "*"` | Assuming host-header validation is handled | `"AllowedHosts": "*"` in `appsettings.json` means Omphalos does no host-header restriction at the app level — if the SSO endpoint constructs any redirect or callback URL from request data, validate it doesn't trust an attacker-controlled `Host` header, since ASP.NET Core itself won't reject unexpected hosts here |
| Quest Board `ReverseProxy:KnownProxies` | Assuming the new Platform Settings/SSO endpoints inherit correct client-IP handling automatically | If the SSO redirect endpoint needs any per-client rate limiting (e.g. to prevent token-minting abuse), it must go through the same `ForwardedHeaders`/`KnownProxies` config already established for the Forgot Password limiter — a new endpoint added without checking this collapses all clients into one shared bucket, exactly the bug already documented and fixed for the password-reset flow |
| Omphalos Postgres collation | Assuming Postgres behaves like SQL Server for case-insensitive matching | Explicitly verify the collation Omphalos's Postgres database and the `Username` column use — do not assume; write the normalization logic in application code regardless (Pitfall 2), since relying on DB collation for a security-relevant identity match is fragile and non-obvious to future maintainers |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| MAC input omits expiry/audience/purpose claims | Token replay across purposes, or expiry bypass by editing untrusted plaintext | Include every authorization-relevant field inside the signed payload (Pitfall 1) |
| Signature comparison via `==`/`string.Equals` | Timing side-channel leaks the correct signature byte-by-byte over many requests | Use `CryptographicOperations.FixedTimeEquals`, or rely on `JwtSecurityTokenHandler.ValidateToken` which does this internally |
| Long-lived SSO token | A leaked/logged token (e.g. in browser history, server access logs, a referrer header) remains exploitable long after issuance | Keep the token short-lived (single-digit minutes) and ideally single-use — track consumed token IDs (`jti` claim) server-side on the Omphalos side for the token's short validity window if replay matters |
| Token passed as a query string parameter | Query strings land in server access logs, browser history, and `Referer` headers if the destination page then makes outbound requests | Accept this as a known, bounded tradeoff given the short-lived/single-use mitigations above (there's no realistic cookie-based alternative across domains) — but do ensure the destination page doesn't leak the URL via an outbound `Referer` on its first render (e.g. strip the token from the URL via `history.replaceState` immediately after consuming it) |
| Committed placeholder secret in `appsettings.json` | Anyone with read access to the (presumably public, or at least multi-collaborator) Omphalos repo can see the fallback value; a misconfigured deploy that doesn't override it is trivially forgeable | Require the secret via a hard-fail env var pattern (`:?` in Compose), matching `JWT_SECRET`'s existing treatment |
| Role/authorization decisions trusted from token payload without also being enforced by an independent Omphalos-side check | If any single canonicalization or validation bug lets a claim be forged (Pitfall 1), it directly grants privilege escalation since there's no second layer of defense | Don't treat "the signature validated" as sufficient alone for high-privilege actions — cross-check the mapped role against a sane allowlist server-side, and log provisioning/role-grant events for post-hoc audit |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Token expires mid-click (network latency, slow page load) with no clear error | DM sees a generic error page on Omphalos with no indication of what went wrong or how to retry | Omphalos's SSO endpoint should return a clear, branded error state on expired/invalid tokens with an obvious "go back to Quest Board and try again" affordance, not a raw 401/exception page |
| "Open Session Notes" always creates a *new* session instead of finding the existing one for that quest | DM's session notes appear to reset every time they click the button | Deterministic session-ID derivation (Pitfall 5) so repeated clicks land on the same session |
| SuperAdmin configures Omphalos URL/secret but never tests the connection before DMs try it live | First real user of the feature hits a broken integration in front of everyone, with no easy way for the admin to have caught it earlier | Add a "test connection" action on the Platform Settings page that round-trips a validation call to Omphalos before saving/announcing the feature as live |

## "Looks Done But Isn't" Checklist

- [ ] **HMAC token format:** Often missing explicit expiry/audience binding inside the MAC — verify by attempting to forge a token with an extended expiry using only the plaintext payload and confirming the signature check rejects it
- [ ] **Identity matching:** Often missing case-normalization on both write and read paths — verify by provisioning with `TestUser` then attempting SSO login as `testuser` and confirming it resolves to the same account, not a new one
- [ ] **Secret rotation:** Often missing any documented procedure — verify a written runbook exists describing exactly which env vars/settings to update on both sides and in what order
- [ ] **Settings form:** Often missing blank-overwrite protection — verify by saving the Platform Settings form with the secret field left blank/masked and confirming the stored secret is unchanged afterward
- [ ] **Cookie `Secure` flag:** Often left as a TODO past the point where it matters — verify Omphalos's `AuthEndpoints.cs` `Secure = true` is uncommented and active in the production config before shipping SSO
- [ ] **Auto-provisioning idempotency:** Often only tested for the first-login path — verify a second SSO login for the same user doesn't reset a manually-changed Omphalos-side role or setting
- [ ] **Omphalos PR:** Often assumed mergeable on the same timeline as Quest Board's own branch — verify the maintainer has actually reviewed and approved the design (not just the code) before Quest Board-side phases that depend on it are marked complete

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|-----------------|
| Canonicalization bug found post-ship (Pitfall 1) | MEDIUM | Rotate the shared secret immediately (invalidates all outstanding tokens), fix the canonical format, redeploy both sides in lockstep, monitor for forged-token attempts in the interim via access logs |
| Duplicate accounts from case mismatch (Pitfall 2) | HIGH | Requires a manual data-merge on the Omphalos side (`GameSession` rows reassigned from the duplicate `User.Id` to the canonical one) — write and dry-run a merge script against a Postgres backup before touching production data; coordinate timing with the Omphalos maintainer since it's their database |
| Secret blanked via admin UI bug (Pitfall 3) | LOW | Re-generate and re-enter the secret on both sides; add the missing blank-overwrite guard before reopening the settings page to admins |
| Cookie `Secure`/`SameSite` misconfiguration breaks the post-redirect session (Pitfall 4) | LOW | Config-only fix (flip `Secure = true`, confirm HTTPS termination), no data recovery needed — but requires an Omphalos-side deploy, so still subject to Pitfall 6's review latency |
| Over-privileged auto-provisioned accounts (Pitfall 5) | MEDIUM | Audit all Omphalos `Admin`-role users against the expected DM/Admin list from Quest Board, manually demote any incorrect grants, then fix the role-mapping logic before re-enabling SSO |
| Stalled/abandoned Omphalos PR (Pitfall 6) | HIGH | Same failure mode that killed the previous integration attempt — mitigate by keeping branches short-lived (see Pitfall 6's prevention); if it does stall, prefer closing and reopening a smaller, easier-to-review PR over letting divergence compound |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| HMAC canonical message ambiguity | Token-format design phase (first, shared-contract phase, before either side codes) | Round-trip test: mint a token with a known payload, tamper with one byte of any field, confirm verification fails; confirm expiry/audience are inside the MAC by attempting to forge an extended-expiry token |
| Cross-store identity duplication | Omphalos auto-provisioning phase | Provision via mixed-case username twice, assert single `User` row; confirm a unique index exists on the normalized identity column |
| Secret storage / blank-overwrite | Platform Settings UI phase (Quest Board) + Omphalos config/env plumbing phase | Save settings form with secret field blank, assert stored value unchanged; confirm Omphalos fails to start with a missing/placeholder SSO secret in production config |
| Cookie SameSite/Secure across domains | Omphalos SSO validation endpoint phase | Confirm `Secure=true` active in production Omphalos config; manually verify the post-redirect session persists on first load, not just on a subsequent click |
| Auto-provisioning role/session mapping | Omphalos auto-provisioning phase | Write down the explicit role-mapping table as part of the phase plan; test second-login does not reset a manually-changed role; test repeated "Open Session Notes" clicks land on one session, not duplicates |
| Cross-repo PR friction | Design-review-with-maintainer phase (before Omphalos implementation starts) + budgeted review time on every Omphalos-touching phase | Confirm design sign-off exists before implementation; confirm each Omphalos PR is scoped small/additive and merged before the next depends on it |

## Sources

- Direct codebase inspection: `C:\Repos\quest-board\.planning\PROJECT.md`, `QuestBoard.Repository\Entities\UserEntity.cs`, `QuestBoard.Service\Controllers\Admin\AdminController.cs`, `QuestBoard.Service\Program.cs`, `docs\server-setup.md`
- Direct codebase inspection: `C:\Repos\omphalos\src\Omphalos.Web\Endpoints\AuthEndpoints.cs`, `src\Omphalos.Services\Implementations\AuthService.cs`, `src\Omphalos.Web\Program.cs`, `src\Omphalos.Domain\Entities\User.cs`, `src\Omphalos.Domain\Entities\GameSession.cs`, `src\Omphalos.Repository\Repositories\UserRepository.cs`, `src\Omphalos.Repository\OmphalosDbContext.cs`, `docker-compose.prod.yml`, `.github\workflows\ci.yml`, `CLAUDE.md`
- [Canonicalization Attacks Against MACs and Signatures](https://soatok.blog/2021/07/30/canonicalization-attacks-against-macs-and-signatures/) — MEDIUM confidence, single-author security blog but technically well-regarded and cross-checked against Wikipedia's HMAC article
- [HMAC — Wikipedia](https://en.wikipedia.org/wiki/HMAC) — HIGH confidence, corroborates length-extension immunity claim
- [Preventing Timing Attacks on String Comparison with a Double HMAC Strategy — Paragon Initiative Enterprises](https://paragonie.com/blog/2015/11/preventing-timing-attacks-on-string-comparison-with-double-hmac-strategy) — MEDIUM confidence, reputable applied-cryptography vendor blog, corroborates constant-time comparison requirement
- [Timing Attacks against String Comparison](https://www.e-dna.co/security/blog/timing-attacks-against-string-comparison/index.html) — MEDIUM confidence, corroborates the general timing-attack mechanism independently of the Paragon source

---
*Pitfalls research for: Quest Board ↔ Omphalos signed-token SSO bridge*
*Researched: 2026-07-02*
