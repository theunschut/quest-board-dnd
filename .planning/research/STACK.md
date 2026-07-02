# Stack Research

**Domain:** Cross-app SSO bridge (HMAC-signed token handoff) between two independently-deployed .NET 10 apps
**Researched:** 2026-07-02
**Confidence:** HIGH

## Recommended Stack

### Core Technologies — Quest Board side (`C:\Repos\quest-board`)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `System.Security.Cryptography.HMACSHA256` (BCL) | Built into .NET 10 runtime | Sign the outgoing SSO token payload | Zero new dependency. Static `HMACSHA256.HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source)` has existed since .NET 6, confirmed current in .NET 10 docs. No instantiation, no `IDisposable` juggling. [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata?view=net-10.0) |
| `Microsoft.AspNetCore.WebUtilities.WebEncoders` (framework-provided) | Ships with `Microsoft.AspNetCore.App` (already a `<FrameworkReference>` in `QuestBoard.Domain.csproj`) | URL-safe base64 encode of the HMAC signature bytes | **Already the established pattern in this codebase** — `AccountController.cs` uses `WebEncoders.Base64UrlEncode`/`Base64UrlDecode` for password-reset and email-change confirmation tokens (lines 44, 86, 222, 250). Reusing it keeps token-encoding conventions consistent across the app instead of introducing a second technique. No new package reference needed. |
| EF Core 10.0.9 (existing) | Already in use | New `PlatformSettingEntity` (or similarly named) table to store the Omphalos base URL + shared secret | No existing key-value settings store exists in the repo (verified — no `Settings`/`Setting` entity anywhere in `QuestBoard.Repository`). This must be a new EF Core entity + migration, not an `IOptions` binding, because the value has to be **admin-editable at runtime** via the Platform Settings page — `IOptions<T>` (used by `EmailSettings`) is bound once from `appsettings.json`/env vars at startup and isn't meant for live admin edits without a restart or `IOptionsMonitor` reload plumbing that doesn't exist here. |

### Core Technologies — Omphalos side (`C:\Repos\omphalos`)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `System.Security.Cryptography.HMACSHA256` (BCL) | Built into .NET 10 runtime | Verify the incoming Quest Board token's signature | Same BCL type, verification side: recompute HMAC over the received payload with the shared secret and compare. No new package — Omphalos already references `Microsoft.AspNetCore.App` via `Sdk="Microsoft.NET.Sdk.Web"`. |
| `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` (BCL) | Built into .NET 10 runtime | Constant-time comparison of the recomputed HMAC vs. the token's signature bytes | A plain `==`/`SequenceEqual` byte comparison short-circuits on first mismatch, leaking timing information an attacker could use to brute-force the signature byte-by-byte. `FixedTimeEquals` is the documented BCL answer to this and has existed since .NET Core 2.1 — no reason to hand-roll it. [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals?view=net-10.0) |
| `Microsoft.AspNetCore.WebUtilities.WebEncoders` (framework-provided) | Ships with `Microsoft.AspNetCore.App` | Decode the URL-safe base64 signature/payload sent by Quest Board | Must match whatever Quest Board encodes with — see Integration Contract below. Already implicitly available (Omphalos is also `Sdk="Microsoft.NET.Sdk.Web"`), just unused today. |
| EF Core 10 / Npgsql (existing) | Already in use | Extend `IUserRepository` with a "get-or-create by external identity" method for auto-provisioning; no new settings table needed on this side | Omphalos needs to look up (or create) a `User` row keyed by something stable from Quest Board (email is the natural choice — see Integration Contract). The shared secret itself lives in Omphalos `appsettings.json`/env var (`Sso:Secret`), mirroring the existing `Jwt:Secret` pattern — it does **not** need to be admin-editable at runtime on the Omphalos side, since Omphalos has no per-tenant concept and Quest Board is the only caller. |
| `System.Text.Json` (BCL) | Built into .NET 10 runtime | Not required for the token payload itself (see contract — payload is pipe-delimited, not JSON) but already the default serializer for Minimal API request/response bodies if the SSO endpoint needs any JSON side-channel | Already used throughout Omphalos (`GameSession.SessionLog` is `JsonDocument`). No new dependency. |

### What NOT to add (either repo)

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| A JWT library (`System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Tokens`) **on the Quest Board side** | Quest Board has zero crypto/JWT dependencies today (verified — no `System.IdentityModel`, no `Microsoft.IdentityModel.Tokens` anywhere in the repo). Pulling in a JWT stack for a single internal signed-redirect token is disproportionate: JWT gives you a header/claims/registered-claim-names envelope, `alg` negotiation, and a spec surface (`kid`, `nbf`, audience validation edge cases) that solves problems this integration doesn't have. A raw HMAC-signed query string is simpler to reason about, simpler to debug (no base64url-JSON-decode step to eyeball a payload), and has no algorithm-confusion attack surface (JWT's `alg: none` / RS256-vs-HS256 confusion class of bugs literally cannot occur if there's no `alg` field to attack). | Flat delimited payload + HMACSHA256 signature, both base64url-encoded (see Integration Contract) |
| Reusing Omphalos's **existing** JWT machinery (`Microsoft.IdentityModel.Tokens`, already a dependency there) for the *incoming* SSO token | Tempting since the package is already referenced for Omphalos's own login flow, but it signs Omphalos's *own* session tokens with `Jwt:Secret` — reusing that secret/format for the cross-app handoff would conflate two different trust boundaries (Omphalos's own users' sessions vs. a federated-login assertion from an external system) and couple the two token formats together. A short-lived, single-purpose HMAC token with its own distinct secret (`Sso:Secret`, separate from `Jwt:Secret`) keeps the blast radius of a leaked SSO secret contained to the bridge, not to Omphalos's entire session-issuance capability. | A second, dedicated `Sso:Secret` config value; new endpoint that mints Omphalos's own `omphalos_token` cookie *after* verifying the bridge token — i.e., the JWT library still gets used, just one layer downstream, to issue the normal Omphalos session once SSO validation succeeds |
| OAuth2 / OIDC (`Duende.IdentityServer`, `Microsoft.AspNetCore.Authentication.OpenIdConnect`, or standing up Quest Board as an OIDC provider) | Massive overkill for a two-app, same-operator, shared-secret trust relationship. OIDC solves delegated-authorization-across-untrusted-parties problems (consent screens, token introspection endpoints, discovery documents, refresh token rotation) that don't exist here — both apps are operated by the same person/group, and the "client" (Quest Board) already knows the "user" is legitimately authenticated because it just checked its own Identity cookie. | Direct HMAC-signed redirect — the entire flow is: Quest Board signs a short-lived assertion, redirects the browser to Omphalos with it in the query string, Omphalos verifies and establishes its own session |
| A generic "webhook signature" NuGet package (e.g., wrapper libraries for HMAC webhook verification) | These exist for verifying *inbound webhook* signatures (Stripe/GitHub-style), which is a slightly different shape (header-based signature, request-body hashing, replay-window headers) from a redirect-carried token. Using one here would be forcing an ill-fitting abstraction onto a simpler problem, plus adding a dependency for what's ~15 lines of BCL code on each side. | Hand-rolled HMAC sign/verify helper class per repo, following the Integration Contract below |
| `Microsoft.Extensions.Caching.SqlServer`-style server-side token store / nonce table for replay protection | Possible future hardening, but not needed for a first cut given the trust model (same operator, short expiry window covers the realistic threat). Introduces a new table + cleanup job on one or both sides for marginal benefit given the token's short TTL already bounds replay risk. | Short expiry (recommend 60-120 seconds) baked into the signed payload, checked on verify; revisit a nonce/jti store only if logs later show token replay attempts |

## Integration Contract (must match exactly on both sides — no shared compile-time types exist)

Because Quest Board and Omphalos are separately deployed .NET solutions with no shared library, the wire format must be pinned down in prose, not code. This is the single most important part of this research — a one-byte difference in field order or encoding between the two independently-maintained implementations breaks SSO with no compiler to catch it.

**Recommended payload shape** — pipe-delimited fields, UTF-8 encoded, then HMAC-signed as a whole:

```
payload = "{email}|{issuedAtUnixSeconds}|{expiresAtUnixSeconds}|{questId}"
signature = Base64UrlEncode( HMACSHA256( key: UTF8(sharedSecret), data: UTF8(payload) ) )
token = Base64UrlEncode(UTF8(payload)) + "." + signature
```

Then Quest Board redirects the browser to:
```
{OmphalosBaseUrl}/sso?token={token}
```

**Exact rules both sides must agree on (write these into both repos' code/comments, since there's no shared package to enforce it):**

1. **Field order is fixed and positional** — `email`, then `issuedAt`, then `expiresAt`, then `questId` (nullable — empty string if the navigation didn't originate from a specific quest). Do not reorder; do not make fields optional/named — a positional format avoids JSON-canonicalization ambiguity (key ordering, whitespace, escaping) entirely.
2. **Delimiter is `|` (pipe)** — chosen because it cannot legally appear in an email address or a Unix timestamp, and `questId` is numeric (Quest Board's `QuestEntity.Id` is `int`), so it cannot contain a pipe either. **Deliberately excluding `questTitle` from the signed payload** avoids the one field (free-text) that could contain a pipe or need escaping — if Omphalos needs the quest title, it should be fetched via the "foundation for a future bidirectional API" mentioned in PROJECT.md, not embedded in the token. For v1, Omphalos can display a generic "Quest #{id}" label, or use the `Title` already stored on any existing `GameSession` if this session was linked before.
3. **Timestamps are Unix seconds (`long`, UTC)** — not ISO-8601 strings. `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` on the Quest Board side; parse as `long` on Omphalos. Avoids all timezone-string-parsing ambiguity between a Windows-dev/Linux-prod .NET app and a Linux-hosted Omphalos.
4. **Signature covers the raw pipe-delimited string, not the base64url-encoded payload** — sign before encoding, verify by decoding the payload segment first, then recomputing HMAC over those raw bytes.
5. **Encoding is Base64URL (RFC 4648 §5: `-`/`_`, no padding), not standard Base64** — because the token rides in a query string. Use `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode`/`Base64UrlDecode` on **both** sides for this — same BCL-adjacent function, same behavior, zero ambiguity about padding/alphabet edge cases that a hand-rolled `Replace('+','-')` might get subtly wrong.
6. **Key encoding is UTF-8** — the shared secret string is signed/verified as `Encoding.UTF8.GetBytes(secret)`, not `Convert.FromBase64String` or any other interpretation. State this explicitly in both admin UIs ("enter as plain text, minimum 32 characters") since Omphalos's existing `Jwt:Secret` convention is also a plain UTF-8 string, and mismatching this is the single easiest way for the two sides to silently disagree.
7. **Expiry window: short.** Recommend 90 seconds from `issuedAt` to `expiresAt` at signing time, and Omphalos independently checks `expiresAt` server-side (does not trust `expiresAt` blindly — also sanity-check `issuedAt` isn't in the future, to guard against clock skew being exploited rather than just clock skew being a bug). This is a redirect-driven, human-in-the-browser flow — 90 seconds comfortably covers redirect latency while keeping the replay window tight.
8. **HTTPS is mandatory in production for this to mean anything** — the token is a bearer credential for the duration of its short life; it must never traverse plaintext HTTP. Both apps' production deployment docs should call this out explicitly (Omphalos's own `Secure = true` cookie TODO in `AuthEndpoints.cs` is a related, currently-open gap worth fixing in the same milestone since the SSO cookie ends up in the same httpOnly cookie jar).

**What identifies the user across systems:** use **email**, not a numeric/GUID user ID — Quest Board's `UserEntity` extends `IdentityUser<int>` (int PK, not portable), Omphalos's `User.Id` is a `Guid` with no relationship to Quest Board's ID space. Email is the only field both systems already collect that can serve as a natural join key for auto-provisioning ("find Omphalos user by email — Omphalos's `User` entity has no `Email` column today, only `Username`, so this requires either adding an `Email` column to Omphalos's `User` or using the email's local part / a normalized form as the `Username` on auto-create; or create one with a random password hash and `Role = Player`, then log them in"). Confirm Quest Board's `IdentityUser<int>.Email` is populated/confirmed for every DM/Admin account expected to use this feature (it is, per existing email-confirmation flow — Phase 24/32).

**Note (Omphalos schema gap):** `Omphalos.Domain.Entities.User` (in `src/Omphalos.Domain/Entities/User.cs`) currently has `Id`, `Username`, `PasswordHash`, `Role`, `CreatedAt` — no `Email` field. Auto-provisioning-by-email requires either (a) adding an `Email` column + EF Core/Npgsql migration on the Omphalos side, or (b) treating the incoming email as the `Username` directly (simpler, no schema change, but collides if a manually-created Omphalos account already uses a different username for the same person). This is an implementation decision for the phase/plan stage, not a stack decision — flagged here because it affects whether Omphalos needs a migration at all.

## Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| None beyond BCL + already-referenced `Microsoft.AspNetCore.App` framework libs | — | — | This integration deliberately needs zero new NuGet packages in either repo. If `dotnet add package` shows up in either plan for this feature, that's a signal to re-read the "What NOT to add" table above before proceeding. |

## Installation

No new packages required in either repo.

```bash
# Quest Board — nothing to install; HMACSHA256, CryptographicOperations, and
# WebEncoders are all already reachable via existing FrameworkReference/PackageReference.

# Omphalos — nothing to install; same BCL/framework surface already available
# via Sdk="Microsoft.NET.Sdk.Web".
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Raw HMACSHA256 + pipe-delimited payload | JWT (`System.IdentityModel.Tokens.Jwt`) with `HmacSha256` signing | If a third consumer of this token ever appears (not just Quest Board → Omphalos), or if the payload needs to grow to many optional/nested fields — JWT's self-describing claims model starts paying for itself once you have more than ~2 producers/consumers or need standard claim semantics (`exp`, `iss`, `aud`) validated by off-the-shelf middleware instead of hand-written checks |
| EF Core-backed `PlatformSettingEntity` (admin-editable) | `IOptions<OmphalosSettings>` bound from `appsettings.json`/env vars | If the URL/secret only ever needs to change via redeploy (not through the running app's UI) — but PROJECT.md explicitly requires a SuperAdmin-editable Platform Settings page, so `IOptions` alone doesn't satisfy the requirement without adding `IOptionsMonitor` + a custom reload-on-write mechanism, which is more moving parts than one EF Core table |
| Email as the cross-system join key | A new `ExternalId`/`OmphalosUserId` column on Quest Board's `UserEntity` | If email addresses in the group are expected to change often, or if a user might want an Omphalos identity decoupled from their Quest Board email — not the case here (17-member trusted group, confirmed-email flow already enforced); email is simpler and needs no schema change on the Quest Board side at all (the schema gap is entirely on the Omphalos side — see note above) |
| 90-second token expiry | Longer-lived tokens (5-10 min) | If the redirect chain involves additional hops (e.g., an intermediate confirmation page) that could plausibly take longer than ~90 seconds for a real user — current design is a direct link/button → immediate redirect, so 90s is generous already |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `System.IdentityModel.Tokens.Jwt` / `Microsoft.IdentityModel.Tokens` newly added to Quest Board | See "What NOT to add" table above — zero justification for a JWT stack on a codebase that has never needed one, for a single-purpose internal redirect token | `System.Security.Cryptography.HMACSHA256` (BCL, already implicitly available) |
| Duende.IdentityServer / any OIDC provider package on either side | Solves a delegated-trust problem this integration doesn't have (both apps share one operator) | Shared-secret HMAC redirect per the Integration Contract |
| Hand-rolled Base64 URL-encoding (`Replace('+','-').Replace('/','_').TrimEnd('=')`) | Easy to get subtly wrong (padding edge cases, `-`/`_` typos) and would silently diverge between the two independently-written implementations since there's no shared compile-time contract to catch a copy-paste slip | `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode/Decode` — same framework-provided function on both sides |
| `==` or `Enumerable.SequenceEqual` for comparing the recomputed HMAC to the received signature | Not constant-time; leaks timing information usable for a byte-by-byte signature forgery attempt | `CryptographicOperations.FixedTimeEquals` (BCL) |

## Stack Patterns by Variant

**If a future milestone adds a third SSO-consuming app:**
- Migrate from the hand-rolled HMAC format to JWT (`System.IdentityModel.Tokens.Jwt` is already a Omphalos dependency, so it would only be new to Quest Board)
- Because: JWT's self-describing `iss`/`aud`/`exp` claims stop being boilerplate you write by hand once there's more than one producer/consumer pair to keep in sync

**If Omphalos ever gains its own multi-tenant/org concept:**
- Revisit the "instance-wide, not per-group" decision for the Quest Board-side settings (currently correct per PROJECT.md's Key Decisions table, since Omphalos is flatly `UserId`-scoped today)
- Because: a per-group Omphalos target would need a per-group secret too, changing the settings entity from a singleton row to a `GroupId`-keyed table

**If token replay logging ever shows real abuse attempts:**
- Add a server-side used-token/nonce store (e.g., a small Omphalos table keyed by a `jti`-equivalent random GUID embedded in the payload, purged on a schedule)
- Because: the current design accepts the residual risk of a token being replayed within its ~90s window, which is a reasonable tradeoff for a trusted-operator, low-token-volume internal tool — but isn't unconditionally safe if the redirect URL ever leaks (e.g., via browser history sync, referrer headers to a third-party resource on the Omphalos landing page)

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| `HMACSHA256.HashData` (static) | .NET 6.0+ | Both repos target `net10.0`; API has been stable since introduction, no breaking changes through .NET 10. Verified against the `net-10.0` Microsoft Learn doc revision. |
| `CryptographicOperations.FixedTimeEquals` | .NET Core 2.1+ | Stable, unchanged API surface; verified against the `net-10.0` doc revision. |
| `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode/Decode` | Ships with ASP.NET Core since 2.x, present in `Microsoft.AspNetCore.App` 10.0.9 (both repos' current framework version) | Already in active use in Quest Board (`AccountController.cs`); confirm Omphalos's `Sdk="Microsoft.NET.Sdk.Web"` + `TargetFramework net10.0` also has it — it does, since it ships with the shared framework, not a separate package. |
| Quest Board: EF Core 10.0.9 + SQL Server | New migration for `PlatformSettingEntity` | Follows the existing auto-migrate-on-startup pattern (`context.Database.Migrate()`); no new provider or version needed. |
| Omphalos: EF Core 10 + Npgsql | New repository method(s) on `IUserRepository`/`UserRepository`; possible migration if `Email` column is added to `User` | If auto-provisioning uses email-as-username (no schema change), no migration needed. If an `Email` column is added instead, one Npgsql migration is required, following Omphalos's own `db.Database.MigrateAsync()` auto-apply-on-startup pattern (already in `Program.cs`). |

## Sources

- [HMACSHA256.HashData Method — Microsoft Learn (net-10.0)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata?view=net-10.0) — confirmed static overloads, byte[]/Span variants, current for .NET 10
- [CryptographicOperations.FixedTimeEquals Method — Microsoft Learn (net-10.0)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals?view=net-10.0) — confirmed timing-safe comparison semantics, current for .NET 10
- [WebEncoders.Base64UrlEncode Method — Microsoft Learn (aspnetcore-10.0)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.webencoders.base64urlencode?view=aspnetcore-10.0) — confirmed availability and behavior for ASP.NET Core 10
- Direct source inspection, `C:\Repos\quest-board\QuestBoard.Service\Controllers\Admin\AccountController.cs` (lines 42-50, 84-88, 220-252) — confirmed existing `WebEncoders` usage pattern already established in this codebase
- Direct source inspection, `C:\Repos\quest-board\QuestBoard.Domain\Models\EmailSettings.cs` + `QuestBoard.Service\Program.cs` — confirmed `IOptions`-style static config pattern, and its unsuitability for admin-runtime-editable values
- Direct source inspection, `C:\Repos\quest-board\QuestBoard.Repository\Entities\*.cs` — confirmed no existing key-value settings entity (grep for `Setting`/`Settings` returned no matches)
- Direct source inspection, `C:\Repos\omphalos\src\Omphalos.Web\Program.cs`, `AuthEndpoints.cs`, `AuthService.cs` — confirmed existing JWT stack (`Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt`), cookie-based session delivery (`omphalos_token`, httpOnly, SameSite=Lax, `Secure=true` still a TODO), CORS config pattern (`AllowedOrigins` array, inert by default)
- Direct source inspection, `C:\Repos\omphalos\src\Omphalos.Web\Endpoints\SettingsEndpoints.cs`, `AdminEndpoints.cs`, `SessionEndpoints.cs` — confirmed Minimal API route-group-per-feature pattern, `RequireAuthorization("AdminOnly")`/`.AllowAnonymous()` conventions, `ClaimsPrincipal`-based user-ID extraction (`ClaimTypes.NameIdentifier`)
- Direct source inspection, `C:\Repos\omphalos\src\Omphalos.Domain\Entities\User.cs`, `GameSession.cs`, `IUserRepository.cs`, `UserRepository.cs` — confirmed `Guid` user PK, `UserRole { Player, Admin }` enum, no `Email` column (Username only), no existing external-identity link column, no username-uniqueness-independent "find or create" method (needed for auto-provisioning)
- Direct source inspection, `C:\Repos\quest-board\QuestBoard.Repository\Entities\UserEntity.cs`, `QuestEntity.cs` — confirmed `IdentityUser<int>` (int PK, has `Email`/`UserName` from base class), `QuestEntity.Id` (int), `.Title` (string), `.GroupId` (int) available for the token payload
- Both `.csproj` inspections (`QuestBoard.Domain.csproj`, `QuestBoard.Service.csproj`, `Omphalos.Web.csproj`) — confirmed zero existing crypto/JWT packages on the Quest Board side, confirmed `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9 already present on the Omphalos side

---
*Stack research for: HMAC-signed-token SSO bridge, Quest Board ↔ Omphalos*
*Researched: 2026-07-02*
