# Phase 78: Link Preview Foundation and Quest Cards - Research

**Researched:** 2026-08-26
**Domain:** ASP.NET Core Data Protection, reverse-proxy forwarded headers, Open Graph/Twitter Card unfurling (Discord/Slack/iMessage), EF Core cross-project wiring
**Confidence:** MEDIUM-HIGH (external crawler behaviour is externally-sourced and time-sensitive; internal codebase findings are VERIFIED by direct inspection)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: The signature carries no expiry.** A minted link works until the key ring is destroyed or the quest is gone. The metadata a token unlocks is deliberately thin — title, a ~200-character plain-text snippet, and a static branded image — and serve-time group scoping (D-12) is the real control, not link age. A time-limited token would mean a card that worked in a Discord channel last month silently stops resolving, which reads as a bug rather than a policy and is exactly the "card that quietly never appears" failure the ROADMAP tells this phase to avoid.

  **Accepted cost:** a forwarded link is a permanent bearer token for that quest's card metadata. Rejected `ITimeLimitedDataProtector` on those grounds; note it remains a cheap retrofit if the exposure ever matters.

- **D-02: There is no per-link revocation and no board-wide kill switch.** Deleting the quest is the only retraction, and it works because the serve path does a live lookup (D-04). This is the honest position: Discord and Slack cache an unfurl **server-side**, so no server-side action of any kind retracts a card already sitting in a channel. Building a revocation list would create the appearance of a control that does not actually do the thing users would assume it does.

  Rejected: a per-group signing-purpose version integer (blunt — one bad link kills every link on the board). Rejected: a minted-link table with a management UI (a new table, new UI, and a write on every mint, in service of a retraction that cannot retract).

- **D-03: The Data Protection key ring must be persisted to the database before anything else in this phase is meaningful — `AddDataProtection().PersistKeysToDbContext<QuestBoardContext>()`, via `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, plus a migration for the keys table.**

  **This corrects a factual error in the ROADMAP.** The scope note asserts "keys already survive container restarts the same way auth cookies do." They survive a *restart* — `restart: unless-stopped` restarts the same container with its filesystem intact — but not a *recreate*. There is no `AddDataProtection()` call anywhere in the solution, no `PersistKeysTo*` configuration, and the `questboard` service in `docker-compose.yml` mounts no volume (only `sqlserver` does). The key ring therefore lives in the container's ephemeral filesystem and is destroyed on every image update. Auth cookies already pay this cost (everyone is logged out on deploy). With D-01's no-expiry links, the cost becomes a card that worked yesterday and silently resolves to nothing today.

  The database was chosen over a mounted volume because the DB already has a durable named volume, this needs no compose or deploy-script change on the server, and it survives container recreation, image updates, and host reboots identically. Deployment is via an env file on a separate CT (see `docs/server-setup.md`), not the reference `docker-compose.yml`, which makes a volume-based answer more fragile than it looks.

  **Side benefit, not a new requirement:** users stop being logged out on every deploy.

- **D-04: The token payload is identifiers only — entity type, entity id, and group id — and every card render does a live, group-scoped read.** Nothing about the quest is embedded in the token. Editing a quest updates its card on the next crawler fetch; deleting the quest stops the card rendering. This is also precisely the shape Phase 79's serve-time `IsRevealed` gate requires, so locking it here means Phase 79 inherits the rule instead of relitigating it.

  Rejected: a self-contained token embedding title and snippet. With no expiry (D-01) that text would be frozen forever, quest edits would never reach the card, and deleting the quest would not stop it rendering.

  The type + id + group triple is signed **together**, per the ROADMAP's locked decision — never the id alone, which is replayable across boards the moment two boards both have a quest 47.

- **D-05: `Program.cs:103` gains `ForwardedHeaders.XForwardedProto` and `XForwardedHost` alongside the existing `XForwardedFor`.** Today only `XForwardedFor` is set, so behind Traefik every absolute URL the app generates is wrong in both scheme and host. This is the prerequisite the ROADMAP names, and it is correct for the whole application, not only for cards.

- **D-06: Host trust stays bounded by `ReverseProxy:KnownProxies`; `AllowedHosts` stays `"*"`.** `ForwardedHeadersOptions` only honours forwarded headers from IPs listed as known proxies, so a caller reaching Kestrel directly cannot inject a host. This keeps the Phase 32 decision — trust is config-driven, set per-environment via `ReverseProxy__KnownProxies__0` — rather than introducing a second, hardcoded trust mechanism.

  Rejected: tightening `AllowedHosts` to the production hostname. It is real defence in depth, but a wrong or unset value returns 400 for *every* request — a total outage rather than a degradation — and D-07 already removes host spoofing from the card path entirely.

  **Verify during planning, do not assume:** the Phase 32 UAT deferred confirming `ReverseProxy__KnownProxies__0` was actually set on the App CT, and it has never been confirmed since (2026-07-01). If it is unset, forwarded headers are silently ignored and D-05 alone would be a no-op. D-07 is what stops that from becoming a no-card failure.

- **D-07: `EmailSettings:AppUrl` is the canonical base URL for card metadata and for the copied share link, with request-derived values as the fallback when it is absent.** It is already the application's single answer to "what is my public URL", it is already set correctly in the server's env file, and working production email links are standing proof of that. Reading it makes `og:url`, `og:image`, and the copied link deterministic even if D-06's proxy trust is misconfigured.

  **Note on the config file:** `appsettings.json:30` shows `https://localhost:8001` and `docs/server-setup.md` does not list `EmailSettings__AppUrl` among the env vars. Neither is evidence the production value is wrong — the server env file overrides appsettings and is not fully mirrored in the doc. The doc is incomplete, not the config.

  Rejected: a new dedicated key such as `PublicBaseUrl`. Two config keys meaning the same thing is the near-identical-sources drift class this project has repeatedly been bitten by, and a forgotten second env var would let the two disagree. Rejected: renaming `AppUrl` into a shared key — it touches every email template and `EmailPreviewController`, and requires renaming the env var on the server in the same deploy or every email link breaks.

  **Naming smell, accepted:** a link-preview feature reading a key under `EmailSettings` is untidy. Deferred as a rename, not fixed here.

- **D-08: The signed link points at a dedicated, anonymous-allowed preview route (e.g. `/s/quest/{token}`), not at the quest URL with a query parameter.** `QuestController.Details` is not touched by the preview path at all, so no future change to that action can widen what an anonymous signed caller sees. The endpoint is small enough to audit in one sitting, and Phase 79 adds sibling routes alongside it rather than adding a second branch to a second controller.

  **Accepted cost:** the copied link is not the quest's own URL, so a member who clicks it takes one extra hop (D-11 makes that hop automatic).

- **D-09: `QuestController.Details` GET gains `[Authorize]`.**

  **This closes a real gap that this phase would otherwise open.** Today `Details` GET has no `[Authorize]` (`QuestController.cs:306-307`), `GroupSessionMiddleware` explicitly passes anonymous requests through, and the fail-closed query filter at `QuestBoardContext.cs:281` returns nothing — so an anonymous caller gets a 404. **That 404 is currently the page's only security boundary.** This phase deliberately sets group context from a verified signature; the moment it does, that protection evaporates and an anonymous holder of a signed link would render the entire quest page. `[Authorize]` makes the login requirement explicit and independent of group context.

  This also delivers ROADMAP success criterion 6 — a signed link opened logged-out lands on the login page — which the current 404 does not satisfy.

  **Known breakage the planner must handle:** `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs:37` (`Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow`) asserts an anonymous client receives **200 OK**. Adding `[Authorize]` changes that to a login redirect. The test must be updated to assert the redirect — it is a deliberate behaviour change, not a regression, and it must not be "fixed" by weakening the attribute.

  LINKPREV-04 still holds: an unsigned quest URL serves no quest data to an unauthenticated caller. Only the *shape* of the refusal changes, from 404 to a login redirect.

- **D-10: The preview response is a standalone minimal HTML document that uses no application layout, and carries `<meta name="robots" content="noindex">`.**

  **This deliberately overrides the ROADMAP's stated mechanism** ("a shared partial rendered into `_Layout.cshtml`'s `<head>` through a section"). Two scouted facts make that mechanism hazardous: `_Layout.cshtml` has no head section at all (only `Scripts` at line 225), and `MobileDetectionMiddleware` selects `_Layout.Mobile.cshtml` on a User-Agent containing `iPhone`/`iPad` — plausibly what Apple sends when fetching an iMessage preview. Meta tags added to the desktop layout alone would render no card for one of the three explicitly named target clients, silently. That is the same "mobile markup that was never selected" bug class PROJECT.md already records against `_Layout.Platform.Mobile.cshtml`.

  The ROADMAP's *intent* — one shared markup surface that Phase 79 extends rather than copies — is fully preserved: Phase 79 extends this view. Only the host changes. As a bonus, no normal page carries a conditional meta block it never uses.

- **D-11: Every caller receives the identical response — HTTP 200, the meta tags, a `<meta http-equiv="refresh">` to the quest page, and a visible link as fallback.** Card presence is decided by the signature and nothing else; there is no User-Agent branching anywhere on this path, per the ROADMAP's locked decision. Crawlers read the tags and stop; browsers follow the refresh to `/Quest/Details/{id}`, where D-09 sends a logged-out visitor to the login page and a member to the quest.

  Rejected: a 302 straight to the quest page. A 302 carries no body, so a crawler that does not follow redirects gets no meta tags at all — the ROADMAP warns explicitly that crawlers do not reliably follow redirects.

- **D-12: The preview route scopes its single read by setting an in-memory group override — `ActiveGroupContextService.SetGroupId(...)` with the signature's verified group id — and never writes `ActiveGroupId` into Session.**

  `SetGroupId` already exists for exactly this shape: Hangfire jobs use it to scope a read with no HttpContext. The fail-closed query filter then does the work unchanged. **`IgnoreQueryFilters()` is forbidden on this path** — this app has shipped two real cross-tenant leaks (Phases 49/55) and the filter is the remedy.

  **Writing the group id into Session would be a genuine privilege escalation** — it would hand an anonymous visitor a live group context for the remainder of their session, converting a metadata token into board access. `SetGroupId` sets a scoped in-memory override that dies with the request; that distinction is the whole point.

  `IActiveGroupContext` may need widening to expose the setter, or the concrete service resolved directly — planner's choice.

- **D-13: A new branded 1200×630 image asset, composed from existing board art.** The existing art is all portrait — `wwwroot/images/Blanks/Poster1.png` and `Poster2.png` are 1000×1400 at 0.9–1.7 MB — and a portrait image typically demotes a Discord or Slack embed to a small side thumbnail rather than a large card. The new asset should draw on the poster texture, a wax seal, and the Cinzel face `_Layout.cshtml` already loads; export under ~200 KB; and use a hyphen-lowercase filename (every existing image filename contains spaces, which forces percent-encoding into the absolute `og:image` URL). `twitter:card` is `summary_large_image` on the strength of this ratio.

  **This is an asset deliverable, not only code** — the plan must account for producing the image, not just referencing it.

  It must be served unauthenticated at an absolute URL **with no redirect** (LINKPREV-08): crawlers send no cookies and do not reliably follow redirects, so either failure produces silence rather than an error.

- **D-14: The card description is `IMarkdownService.ExtractPlainText(quest.Description)`, whitespace-collapsed, truncated to ~200 characters on a word boundary with an ellipsis, and HTML-escaped.** `ExtractPlainText` is the project's single mechanism for every plain-text teaser surface (established Phase 66 D-06, reused Phase 70); a second text convention is the drift class PROJECT.md blames for four recorded bugs.

  **Truncation happens on the rendered plain text, never on the Markdown source** — truncating the source can strip a closing fence and expose text the author had hidden inside it.

  A quest with an empty description falls back to a fixed generic line rather than omitting `og:description`, so no card renders with a conspicuous blank where every other card has text.

- **D-15: Any board member sees the "Copy shareable link" control, and the signed URL is minted at page render and embedded in the view.** A quest is board-level information — the same reasoning Phase 74 D-05 used for events — and anyone who can open the page can already screenshot it, so a DM-only gate would add an authorization branch that protects nothing. Phase 79 is free to answer its own minting question differently for characters.

  Minting at render needs no new endpoint, no antiforgery wiring, and no round trip, and nothing is stored so re-rendering is free. The signed URL does sit in the page source — visible only to people already authorised to view that page, which is exactly the set permitted to mint it.

  Both `Views/Quest/Details.cshtml` and `Views/Quest/Details.Mobile.cshtml` get the control — the both-platforms-in-one-phase rule Phase 72 followed. Mobile markup must be verified with a **real mobile User-Agent**, not devtools emulation (Phase 74 D-16).

- **D-16: The permanence of external unfurl caches is stated both in the UI and in the docs** — a muted one-liner near the copy control or in its confirmation, plus a paragraph in the docs. The ROADMAP's wording for this phase is "belongs in the docs", but Phase 79's risk note says "UI or docs", and it matters far more there: un-revealing a contact cannot pull back a cached card. Establishing the UI pattern here means Phase 79 extends it rather than inventing it.

### Claude's Discretion

Not discussed — planner decides:
- The exact preview route path and token format (the `/s/quest/{token}` shape above is illustrative, not locked).
- The Data Protection purpose string, and whether the group id is part of the purpose or of the payload.
- Whether `IActiveGroupContext` is widened to expose `SetGroupId` or the concrete `ActiveGroupContextService` is resolved directly.
- Button placement, iconography, and wording on desktop and mobile, and the copy-confirmation mechanism (toast vs inline). The project's `_Toasts.cshtml` is available in both layouts.
- Exact fallback description wording, exact truncation length, and the ellipsis character.
- The meta-refresh delay, and the wording of the visible fallback link.
- Test structure beyond the required cross-group replay test and the `curl -A Discordbot` check.
- Where the docs paragraph lives (`README.md`, `docs/server-setup.md`, or a new doc).

### Deferred Ideas (OUT OF SCOPE)

- **Rename `EmailSettings:AppUrl`** to a properly-scoped public base URL key used by both emails and link previews. Correct, but it touches every email template, `EmailPreviewController`, and the server env var, and must be a coordinated deploy. Not this phase (D-07).
- **A time-limited or revocable share link.** Deliberately rejected for now (D-01, D-02); `ITimeLimitedDataProtector` remains a cheap retrofit if link exposure ever becomes a real concern.
- **Per-quest generated card images.** Explicitly out of scope in the ROADMAP; D-13 ships one static branded asset.
- **Tightening `AllowedHosts`** from `"*"` to the production hostname as defence in depth (D-06). Worth doing, but its failure mode is a total outage, so it wants its own change with its own verification.
- **Confirming `ReverseProxy__KnownProxies__0` is set on the App CT.** Outstanding since the Phase 32 UAT (2026-07-01). This phase should verify it as part of deployment acceptance rather than assume it. *(Research note: promoted to a required pre-flight step recommendation below — see Open Question 1 — because of the .NET 10 ForwardedHeaders breaking-change finding; still not itself an implementation task, this remains a verification action.)*
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| LINKPREV-01 | The app generates correct absolute URLs behind the reverse proxy, honouring forwarded scheme and host | Pitfall 1 (.NET 10 ForwardedHeaders trust semantics), Pattern 3 (`EmailSettings:AppUrl` fallback pattern), Open Question 1 |
| LINKPREV-02 | A "Copy shareable link" control on the quest Details page, desktop and mobile, mints a signed link and copies it to the clipboard | Pattern 1/2 (signing service, DI wiring), Anti-Pattern note on `_Toasts.cshtml` not being clipboard-event-wireable |
| LINKPREV-03 | A quest URL carrying a valid signature serves Open Graph and Twitter Card meta tags so Discord, Slack, and iMessage render a rich card | Summary points 1–2, Pitfalls 2–4 (crawler-specific tag/image requirements), Code Examples |
| LINKPREV-04 | A quest URL with no signature behaves exactly as it does today — no card, no quest data served to an unauthenticated caller | Validation Architecture (existing regression test, rewrite scope) |
| LINKPREV-05 | A tampered, malformed, or otherwise invalid signature is rejected and renders no card | Code Examples (`CryptographicException`/`FormatException` handling), Don't Hand-Roll |
| LINKPREV-06 | The card description is plain text derived from the quest's Markdown — syntax stripped, whitespace collapsed, truncated, HTML-escaped | Don't Hand-Roll (`ExtractPlainText` reuse; new truncator needed) |
| LINKPREV-07 | The signed preview read path is scoped to the signature's own verified group and cannot serve data from any other board | Summary point 3, Pattern 2 (`IActiveGroupContext` widening — the decisive finding) |
| LINKPREV-08 | A branded fallback card image is served unauthenticated at an absolute URL, with no redirect | Architectural Responsibility Map (CDN/Static tier), Package Legitimacy Audit N/A (no new package for this) |
| LINKPREV-09 | A valid signature grants card metadata only — never page access, never the ability to sign up or post | Security Domain (Known Threat Patterns), System Architecture Diagram |
</phase_requirements>

## Summary

This phase is mostly settled by CONTEXT.md's 16 locked decisions; what remained to research was everything CONTEXT.md itself flagged as unverifiable by reading the repo: crawler requirements, exact package/version facts, ASP.NET Core's forwarded-headers trust semantics, and — critically — how the existing integration-test harness actually wires group context, which turns out to change the correct implementation of D-12.

Three findings matter more than the rest and are surfaced up front:

1. **D-06's "verify during planning" note is not just prudent, it is load-bearing on .NET 10.** Starting in ASP.NET Core 8.0.17/9.0.6 — and baked into .NET 10 from GA — `ForwardedHeadersMiddleware` **ignores all `X-Forwarded-*` headers from any proxy not explicitly listed in `KnownProxies`/`KnownNetworks`**. If `ReverseProxy__KnownProxies__0` is unset in production (unconfirmed since the Phase 32 UAT, 2026-07-01), then **today**, on this app's .NET 10.0.9 runtime, `X-Forwarded-For` is *already* being silently dropped — not just the new `XForwardedProto`/`XForwardedHost` this phase adds. D-05 alone changes nothing without this. This must be confirmed as a pre-flight/deployment-acceptance step, not assumed. See Pitfall 1.
2. **D-10's premise about Apple's User-Agent is a plausible guess that the evidence does not support — but D-10 is still the right call, for a stronger reason.** Apple's iMessage/Messages link-preview fetcher does not send a User-Agent containing `iPhone`/`iPad`. It sends `Mozilla/5.0 (Macintosh; Intel Mac OS X ...) AppleWebKit/... facebookexternalhit/1.1 Facebot Twitterbot/1.0` — a desktop-Mac-spoofed string carrying Facebook/Twitter bot signatures, regardless of which physical device (iPhone, iPad, or Mac) the recipient is using. `MobileDetectionMiddleware`'s `iPhone`/`iPad` keyword match would **not** fire for this request, so the "Apple's crawler might get served the mobile layout" risk as stated in D-10 does not materialize the way described. However, this doesn't weaken D-10: it strengthens the case for the standalone-view approach, because User-Agent-based layout/markup selection is fundamentally the wrong mechanism for a crawler-facing response regardless of which UA string a given crawler happens to send today — that detail can and does change across vendors and over time. See Priority 1 detail below and Assumptions Log A1.
3. **D-12's open discretion question ("widen `IActiveGroupContext` or resolve the concrete `ActiveGroupContextService`?") has a single correct answer, provable from the test harness.** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` registers `services.AddSingleton<IActiveGroupContext>(TestGroupContext)` — a **different object** than the scoped `ActiveGroupContextService` that Program.cs's dual-registration factory delegates to in production. If the preview controller resolves the concrete `ActiveGroupContextService` type and calls `SetGroupId`, that call succeeds in production (same instance behind both registrations) but **silently does nothing in tests** (the query filter reads the singleton `MutableGroupContext`, an unrelated object). The only implementation that works identically in both environments is widening `IActiveGroupContext` with a `SetGroupId(int?)` method and calling it through the interface. See Priority 3 detail.

**Primary recommendation:** Confirm `ReverseProxy__KnownProxies__0` is set in production before relying on any of this phase's absolute-URL logic; implement D-03's Data Protection wiring entirely inside `QuestBoard.Repository/Extensions/ServiceExtensions.cs`'s existing `AddRepositoryServices` (no package reference needed in `QuestBoard.Service`); and widen `IActiveGroupContext` with `SetGroupId(int?)` rather than resolving the concrete service type.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Absolute URL generation (scheme/host) | Frontend Server (SSR/Program.cs) | — | `ForwardedHeadersMiddleware` + `EmailSettings:AppUrl` fallback are pipeline/config concerns, not per-request business logic |
| Link signing (Data Protection) | API/Backend (Domain or a new Service) | Database (key persistence) | Signing is a cross-cutting concern; key persistence is infrastructure owned by the DB via EF |
| Preview route rendering | Frontend Server (SSR) | — | A dedicated anonymous-allowed MVC action producing server-rendered HTML meta tags — no client-side involvement at all |
| Group-scoped read on signature verification | API/Backend (`IActiveGroupContext` + EF query filter) | — | Must reuse the existing fail-closed filter; this is exactly the tenant-isolation boundary Phases 49/55 exist to protect |
| Card description text (Markdown → plain text) | API/Backend (`IMarkdownService`) | — | Existing domain service; no new tier involved |
| Card image asset | CDN/Static (`wwwroot`) | — | A static file served by `UseStaticFiles()`, not database-backed |
| "Copy shareable link" control | Browser/Client (JS clipboard call) | Frontend Server (mints URL server-side, embeds in HTML) | Minting happens server-side at render (D-15); the copy action itself is a client-side `navigator.clipboard` call with no round trip |

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | NuGet | First shipped 2.2.0 (2018); current line 10.0.x | Extremely high (first-party ASP.NET Core component, millions/week across the DataProtection family) | `github.com/dotnet/aspnetcore` | OK | Approved |

**Packages removed due to `[SLOP]` verdict:** none.
**Packages flagged as suspicious `[SUS]`:** none.

This is a first-party Microsoft package shipped from the `dotnet/aspnetcore` monorepo alongside the EF Core and Identity packages already pinned in this solution at `10.0.9`. The `gsd-tools package-legitimacy check` seam does not support the `nuget` ecosystem (only `npm|pypi|crates`), so this verdict is manual, cross-checked against both `api.nuget.org`'s version index (confirms `10.0.9`/`10.0.10`/`10.0.11` exist, aligned to the .NET 10 release cadence) and the official Microsoft Learn API reference page for `EntityFrameworkCoreDataProtectionExtensions.PersistKeysToDbContext`. `[VERIFIED: nuget.org version index + learn.microsoft.com]`. No `[ASSUMED]` gate applies — this is not a community package with slopsquat risk.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | `10.0.9` (matches every other EF-family package already pinned in this repo; `10.0.11` is newer and also valid) | Persists the Data Protection key ring to `QuestBoardContext` via `IDataProtectionKeyContext` | The framework-native, zero-hand-rolled-crypto way to make keys durable across process/container recreation — exactly what D-03 locks in `[VERIFIED: nuget.org version index]` |
| `Microsoft.AspNetCore.WebUtilities` (`WebEncoders`) | Already transitively available (ships in the ASP.NET Core shared framework, no explicit package needed in `Sdk.Web` projects) | `Base64UrlEncode`/`Base64UrlDecode` for the signed token in the URL | This is the exact mechanism `AccountController.cs` already uses for password-reset/email-confirmation tokens (Phase 32) — reuse it rather than inventing a second URL-safe-encoding convention `[VERIFIED: QuestBoard.Service/Controllers/Admin/AccountController.cs:51,93,241,269]` |

No new package is required in `QuestBoard.Service` for `IDataProtectionProvider`/`AddDataProtection()` itself — those types ship in the ASP.NET Core shared framework and are already used today (`Program.cs`'s `Configure<DataProtectionTokenProviderOptions>`). Only `PersistKeysToDbContext<TContext>()` and `IDataProtectionKeyContext` require the new NuGet package, and per the wiring below that package reference belongs in `QuestBoard.Repository`, not `QuestBoard.Service`.

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| (none new) | — | — | The plain-text truncation-to-~200-chars-with-ellipsis (D-14) and the branded-image composition (D-13) are both new C#/design-tool work, not library gaps — see Don't Hand-Roll below for what *is* reusable |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `PersistKeysToDbContext<QuestBoardContext>()` | A mounted Docker volume for the key ring | Rejected by D-03 — deployment here is systemd on a bare CT (`docs/server-setup.md`'s deploy script does `rm -rf /opt/questboard/* && unzip ...`), so a filesystem-based key ring is wiped on every deploy exactly like the ephemeral container case D-03 describes; the DB already has durable storage with zero deploy-script changes needed `[VERIFIED: docs/server-setup.md:72-100]` |
| `WebEncoders.Base64UrlEncode` | Raw `Convert.ToBase64String` + manual `+`/`/` replacement | Rejected — reinvents what the framework already does correctly and abandons the existing Phase-32 convention |

**Installation:**
```bash
# QuestBoard.Repository/QuestBoard.Repository.csproj — add alongside the other EF-family packages
dotnet add QuestBoard.Repository package Microsoft.AspNetCore.DataProtection.EntityFrameworkCore --version 10.0.9
```

**Version verification:** `api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.dataprotection.entityframeworkcore/index.json` lists `10.0.9`, `10.0.10`, `10.0.11` as current stable releases in the .NET 10 line (queried 2026-08-26). `10.0.9` was chosen to match every other package already pinned in this solution (`Microsoft.EntityFrameworkCore*` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` are all `10.0.9`) rather than introducing a version-skew data point; bumping all of them together is a separate, routine maintenance action. `[VERIFIED: nuget.org]`

## Architecture Patterns

### System Architecture Diagram

```
 Board member on Quest/Details
        │
        │ (render time, D-15)
        ▼
 QuestController.Details ──── mints signed token: Protect(type, id, groupId)
        │                     via IDataProtectionProvider.CreateProtector(purpose)
        │                     Base64Url-encoded (WebEncoders)
        ▼
 View embeds "Copy shareable link" = {AppUrl-or-forwarded-origin}/s/quest/{token}
        │
        │ (member clicks Copy → navigator.clipboard, client-side only)
        ▼
 Link pasted into Discord / Slack / iMessage
        │
        │ crawler fetch: GET /s/quest/{token}  (no cookies, UA = bot string)
        ▼
 New anonymous-allowed PreviewController/Action
        │
        ├─► Unprotect(token) ──► throws CryptographicException on tamper/malformed (D-05 rejection path, LINKPREV-05)
        │        │ success
        │        ▼
        │   type=quest, id, groupId extracted (never trust id alone — D-04)
        │        │
        │        ▼
        │   IActiveGroupContext.SetGroupId(groupId)  [scoped override, dies with request — D-12]
        │        │
        │        ▼
        │   QuestRepository.GetByIdAsync(id)  ── EF fail-closed query filter re-checks
        │        │                                 e.GroupId == ActiveGroupId (now == groupId)
        │        │                                 → returns quest, or null if quest/group mismatch/deleted
        │        ▼
        │   quest == null? → no card (404-equivalent silent response, LINKPREV-07/LINKPREV-04 shape)
        │        │ quest found
        │        ▼
        │   Render standalone minimal HTML (D-10, no _Layout):
        │     og:title, og:description (ExtractPlainText, truncated ~200 chars, D-14),
        │     og:image (branded static asset, D-13), og:url, twitter:card=summary_large_image,
        │     <meta http-equiv="refresh" content="N;url=/Quest/Details/{id}"> (D-11)
        │
        ▼
 Crawler reads meta tags, stops (never follows the refresh)
        │
 Human clicks the link instead
        ▼
 Browser follows <meta refresh> → /Quest/Details/{id}
        │
        ├─ logged out → [Authorize] redirects to /Account/Login (D-09, ROADMAP success criterion 6)
        └─ logged in, member of the quest's group → normal quest page renders
```

### Recommended Project Structure
```
QuestBoard.Domain/
├── Interfaces/
│   └── IActiveGroupContext.cs      # widened: + void SetGroupId(int? groupId)
├── Services/
│   └── LinkSigningService.cs       # or similar — wraps IDataProtectionProvider, Protect/Unprotect the (type,id,groupId) triple
QuestBoard.Repository/
├── Entities/
│   └── QuestBoardContext.cs        # : DbContext, IDataProtectionKeyContext — adds DbSet<DataProtectionKey> DataProtectionKeys
├── Extensions/
│   └── ServiceExtensions.cs        # AddRepositoryServices(...) gains .AddDataProtection().PersistKeysToDbContext<QuestBoardContext>()
├── Migrations/
│   └── <timestamp>_AddDataProtectionKeys.cs
QuestBoard.Service/
├── Controllers/
│   └── LinkPreview/
│       └── QuestPreviewController.cs   # [AllowAnonymous], the /s/quest/{token} route
├── Program.cs                      # ForwardedHeadersOptions gains XForwardedProto|XForwardedHost (D-05)
├── Views/
│   ├── LinkPreview/
│   │   └── QuestCard.cshtml         # standalone HTML doc, no _Layout (D-10) — Phase 79 adds sibling views alongside
│   └── Quest/
│       ├── Details.cshtml           # + Copy shareable link control (D-15)
│       └── Details.Mobile.cshtml    # + same control
wwwroot/images/
└── link-preview-card.png            # new 1200×630 branded asset (D-13), hyphen-lowercase filename
```

### Pattern 1: EF Core Data Protection wiring that respects the Repository/Service boundary
**What:** Register `AddDataProtection().PersistKeysToDbContext<QuestBoardContext>()` *inside* `QuestBoard.Repository/Extensions/ServiceExtensions.cs`'s existing `AddRepositoryServices` extension method — the same method that already registers `AddDbContext<QuestBoardContext>`.
**When to use:** Always, for this phase. This is the only wiring shape that satisfies CLAUDE.md's "EF packages belong only in `QuestBoard.Repository`" rule.
**Why it resolves the apparent tension:** `Program.cs` (in `QuestBoard.Service`) already calls `builder.Services.AddRepositoryServices(builder.Configuration).AddDomainServices(builder.Configuration);` with zero EF-specific code of its own — the EF wiring happens entirely inside the Repository-project extension method it calls. `PersistKeysToDbContext<TContext>()` and `IDataProtectionKeyContext` both live in the new NuGet package; since `Microsoft.EntityFrameworkCore.Design` is already referenced with `PrivateAssets="all"` in this same `.csproj` (proving the project's default behaviour is to flow package references transitively unless explicitly suppressed), adding `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to `QuestBoard.Repository.csproj` alone should make both `AddDataProtection()` (a transitive dependency of the `.EntityFrameworkCore` package) and `PersistKeysToDbContext<T>()` resolvable inside `ServiceExtensions.cs`, with zero package reference needed in `QuestBoard.Service`. `[CITED: learn.microsoft.com/dotnet/api/microsoft.aspnetcore.dataprotection.entityframeworkcoredataprotectionextensions.persistkeystodbcontext]` for the API shape; the transitive-reference behavior is `[VERIFIED: QuestBoard.Repository.csproj]` inference from the existing `PrivateAssets` usage, confirm at `dotnet build` time — if `AddDataProtection()` doesn't resolve, add `Microsoft.AspNetCore.DataProtection` explicitly to the same csproj (harmless, keeps the reference in Repository either way).
**Example:**
```csharp
// QuestBoard.Repository/Extensions/ServiceExtensions.cs
public static IServiceCollection AddRepositoryServices(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<QuestBoardContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

    // Persists the Data Protection key ring to the DB so it survives deploy-time recreation
    // (the App CT's deploy script wipes /opt/questboard on every release) — see 78-CONTEXT.md D-03.
    services.AddDataProtection().PersistKeysToDbContext<QuestBoardContext>();

    // ... existing repository registrations unchanged
    return services;
}
```
```csharp
// QuestBoard.Repository/Entities/QuestBoardContext.cs
public class QuestBoardContext(DbContextOptions<QuestBoardContext> options, IActiveGroupContext activeGroupContext)
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;
    // ... existing DbSets and OnModelCreating unchanged
}
```
`DataProtectionKey` carries no `GroupId` and is never referenced from `OnModelCreating`'s `HasQueryFilter` calls — it is a global, ungrouped infrastructure table, so it cannot interact with the existing tenant-isolation filters `[VERIFIED: QuestBoard.Repository/Entities/QuestBoardContext.cs:271-300]`.

### Pattern 2: Widen `IActiveGroupContext`, don't resolve the concrete type
**What:** Add `void SetGroupId(int? groupId);` to `IActiveGroupContext` itself; implement it on both `ActiveGroupContextService` (already has the method, just needs to satisfy the interface) and the test double `MutableGroupContext` (trivial — sets its existing settable property).
**When to use:** The preview controller must call `SetGroupId` through the injected `IActiveGroupContext`, never by resolving the concrete `ActiveGroupContextService` type.
**Why:** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:72` does `services.AddSingleton<IActiveGroupContext>(TestGroupContext)` — this REPLACES the `IActiveGroupContext` resolution for the whole DI graph in the `Testing` environment with a singleton `MutableGroupContext` instance, entirely separate from the scoped `ActiveGroupContextService` concrete-type registration that Program.cs's dual-registration comment describes (`AddScoped<ActiveGroupContextService>()` + `AddScoped<IActiveGroupContext>(sp => sp.GetRequiredService<ActiveGroupContextService>())`). In production these two registrations resolve to the *same instance within a request scope*, so resolving either the interface or the concrete type gives identical, mutually-visible state. In the test environment they do **not** — `ActiveGroupContextService` (concrete) is still registered and resolvable, but it is now a completely different object from whatever `IActiveGroupContext` resolves to. A controller that does `serviceProvider.GetRequiredService<ActiveGroupContextService>().SetGroupId(...)` would set state nobody reads in tests (the query filter always reads via `IActiveGroupContext`), making LINKPREV-07's cross-group replay test either fail unexpectedly or pass for the wrong reason. `[VERIFIED: QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:16,72-73; QuestBoard.Service/Program.cs:220-228; QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs]`
**Example:**
```csharp
// QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }

    // Sets a scoped/singleton-local override for the current execution context.
    // Never persists to Session — see 78-CONTEXT.md D-12 (Session write == privilege escalation).
    void SetGroupId(int? groupId);
}
```
```csharp
// QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs — add:
public void SetGroupId(int? groupId) => ActiveGroupId = groupId;
```
```csharp
// New preview controller
public class QuestPreviewController(IActiveGroupContext activeGroupContext, IQuestRepository questRepository, ...) : Controller
{
    [HttpGet("/s/quest/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Quest(string token, CancellationToken ct)
    {
        if (!linkSigningService.TryUnprotect(token, "quest", out var id, out var groupId))
            return NotFound(); // tampered/malformed — LINKPREV-05, rejected not degraded

        activeGroupContext.SetGroupId(groupId); // scoped override, D-12 — never Session

        var quest = await questRepository.GetByIdAsync(id, ct); // fail-closed filter re-applies with groupId now set
        if (quest is null)
            return NotFound(); // deleted, or groupId didn't match — LINKPREV-07

        return View("QuestCard", BuildCardModel(quest));
    }
}
```

### Pattern 3: Absolute URLs — `EmailSettings:AppUrl` with a request-derived fallback (D-07)
**What:** Reuse the exact `$"{emailSettings.AppUrl}/Quest/Details/{questId}"` string-interpolation pattern already used in every `*EmailJob.cs` file, rather than `Url.Action(..., protocol: ...)`.
**Example (fallback shape, new code this phase adds):**
```csharp
private string ResolveBaseUrl(HttpContext context, EmailSettings emailSettings)
{
    if (!string.IsNullOrWhiteSpace(emailSettings.AppUrl))
        return emailSettings.AppUrl.TrimEnd('/');

    // Fallback only exercised if AppUrl is unset — relies on D-05's ForwardedHeaders fix
    // and therefore on ReverseProxy:KnownProxies actually being configured (see Pitfall 1).
    var request = context.Request;
    return $"{request.Scheme}://{request.Host}";
}
```
`[VERIFIED: QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs:43, QuestDateChangedEmailJob.cs:29, SessionReminderJob.cs:43, QuestWaitlistPromotedEmailJob.cs:32]`

### Anti-Patterns to Avoid
- **Resolving `ActiveGroupContextService` by concrete type in the preview controller.** Works in production, silently no-ops in the test harness — see Pattern 2.
- **Using `_Toasts.cshtml`'s `TempData`-driven flash mechanism for the copy-confirmation.** It is entirely server-render/page-load driven (reads `TempData["Success"]` etc. at view-render time); a client-side `navigator.clipboard.writeText()` call has no server round trip to set `TempData` on. Wiring "Copied!" through this partial would require an unnecessary page reload after every copy, which is a worse UX than the existing add-character/edit flows this app otherwise favors. Use a small dedicated client-side toast (a new lightweight Bootstrap toast triggered from JS, or a transient button-text swap) instead — this is a genuine correction to the "Claude's Discretion" note in CONTEXT.md, not a re-litigation of the locked decision to use toast-or-inline (that choice remains open; only the *mechanism* changes). `[VERIFIED: QuestBoard.Service/Views/Shared/_Toasts.cshtml — entirely `@if (TempData[...] != null)` blocks, no JS-callable entry point]`
- **`Url.Action(..., protocol: Request.Scheme)` as the primary absolute-URL mechanism.** It would still be correct *if* forwarded headers are properly trusted, but D-07 already locked `EmailSettings:AppUrl` as canonical specifically because it is deterministic even when proxy trust (D-06) is misconfigured — don't reintroduce dependence on `Request.Scheme`/`Request.Host` as the primary path.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tamper-evident, symmetric-key signed token | A custom HMAC-over-JSON scheme | `IDataProtectionProvider.CreateProtector(purpose).Protect(...)`/`Unprotect(...)` | Already locked by D-03/ROADMAP; throws `CryptographicException` on tamper/malformed/wrong-purpose out of the box — LINKPREV-05 for free `[CITED: learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/overview]` |
| URL-safe token encoding | Manual `+`/`/`/`=` stripping on `Convert.ToBase64String` | `WebEncoders.Base64UrlEncode`/`Base64UrlDecode` | Already the project's convention (Phase 32); a second encoding convention is exactly the drift class PROJECT.md blames for repeated bugs |
| Plain-text extraction from Markdown | A second regex/HTML-stripping pass | `IMarkdownService.ExtractPlainText` | Locked by D-14; already unit-tested including word-boundary cases |
| Word-boundary truncation to ~200 chars + ellipsis | — (genuinely new, see below) | New small helper | No existing helper in the codebase does this — see note below |

**Key insight:** Almost everything this phase needs already exists in the codebase (Data Protection framework primitives, `WebEncoders`, `ExtractPlainText`, `SetGroupId`, `EmailSettings:AppUrl`). The one genuinely new piece of text-processing logic is the ~200-char word-boundary-with-ellipsis truncator (D-14) — `MarkdownService.cs`'s existing `TruncateAtBlockBoundary` operates on parsed HTML *block elements* and appends a "read more" *link*, which is a different mechanism for a different surface (long-form email truncation) and is not reusable here as-is. `[VERIFIED: QuestBoard.Domain/Services/MarkdownService.cs:262-310 — grep confirms no other word-boundary/ellipsis truncation helper exists in QuestBoard.Domain]` Write this as new, small, unit-testable code — do not try to force-fit `TruncateAtBlockBoundary`.

## Common Pitfalls

### Pitfall 1: `ForwardedHeadersMiddleware` silently ignores everything if `KnownProxies` is empty — and this is *already* true today, not just a risk this phase introduces
**What goes wrong:** D-05 adds `XForwardedProto`/`XForwardedHost` to `ForwardedHeadersOptions.ForwardedHeaders`, but if `ReverseProxy:KnownProxies` resolves to an empty list at runtime (i.e. `ReverseProxy__KnownProxies__0` is unset on the App CT), **none** of the forwarded headers are honored — not the two new ones, and not the pre-existing `XForwardedFor` either.
**Why it happens:** ASP.NET Core 8.0.17 and 9.0.6 shipped a security-hardening breaking change: "the forwarded headers middleware ignores all `X-Forwarded-*` headers from proxies that aren't explicitly configured as trusted." This is baked into .NET 10 from its initial release (the Microsoft Learn breaking-changes page's moniker range includes `aspnetcore-10.0` with no separate opt-in). Previously, headers were processed from any source when `KnownProxies`/`KnownNetworks` were unconfigured (the older, less-safe default); that legacy path requires either an `AppContext` switch documented as "applications that target .NET 9 or earlier" (may not apply to this .NET 10 app) or explicitly clearing the lists (defeats the purpose). `[CITED: learn.microsoft.com/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies]`
**How to avoid:** Before relying on any part of this phase's absolute-URL logic, run `curl -A Discordbot https://<prod-host>/s/quest/{token}` on the **deployed host** (ROADMAP success criterion 4) and independently confirm `X-Forwarded-For`-derived rate limiting still partitions by real client IP (the original reason `KnownProxies` was introduced in Phase 32). If `ReverseProxy__KnownProxies__0` turns out to be unset, this is not solely a link-preview bug — it means per-client rate limiting (`forgot-password`, `set-password` policies) has silently collapsed to a single shared bucket for all visitors since whichever .NET 10 servicing update this app picked up. Treat "confirm `ReverseProxy__KnownProxies__0` is set on the App CT" as a **blocking pre-flight step for this phase**, not a nice-to-verify.
**Warning signs:** `og:url`/`og:image` resolve to `http://localhost:8001` (the `appsettings.json` fallback) even when `EmailSettings:AppUrl` is correctly set for the *email* fallback path — or, if relying on the request-derived fallback at all, absolute URLs come back with the wrong scheme/host in production only.

### Pitfall 2: D-10's stated justification (Apple sends an iPhone/iPad UA) doesn't hold, but the risk class it worries about is real for a different reason
**What goes wrong:** Trusting a specific crawler's current User-Agent string as the basis for *any* branching decision (layout selection, feature gating) is fragile — vendors change these strings, and in this specific case Apple deliberately impersonates a desktop Mac Safari browser with Facebook/Twitter bot signatures spliced in (`facebookexternalhit/1.1 Facebot Twitterbot/1.0`), not an iPhone/iPad string, specifically so its true nature is disguised. `MobileDetectionMiddleware`'s `["Mobi","Android","iPhone","iPad","Windows Phone","BlackBerry"]` keyword list would not match this string.
**Why it happens:** This is a known, long-standing (documented since ~2015, still current per 2023-era security writeups) intentional design choice by Apple, not a bug that will necessarily be fixed. `[CITED: multiple independent sources — SecurityWeek "iMessage URL Preview Exposes User Data", two independent Medium writeups, rsmck.co.uk — cross-corroborated, no single official Apple statement documents the exact UA string]`
**How to avoid:** D-10's standalone-view approach already avoids this entire risk class regardless of which UA any crawler sends — no code change needed, but the CONTEXT.md rationale text should be understood as "any User-Agent-based branching is the wrong mechanism for a crawler response" rather than "Apple specifically spoofs iPhone/iPad." See Assumptions Log A1.
**Warning signs:** N/A — D-10 already sidesteps this; flagging only so the plan's rationale/commit messages don't repeat the unverified claim as fact.

### Pitfall 3: Apple/iMessage does not read `og:description` at all
**What goes wrong:** Even with a perfectly correct, truncated, HTML-escaped `og:description` (D-14/LINKPREV-06), iMessage's card will never show it — only `og:title` and `og:image` are used for iMessage's rich preview.
**Why it happens:** Documented directly by Apple's own TN3156 ("Create rich previews for Messages") per third-party summaries, and independently corroborated by a specific `mastodon/mastodon` GitHub issue (#22382) where the maintainers confirmed and closed as "not planned" (Apple's responsibility, not fixable server-side). `[CITED: developer.apple.com/documentation/technotes/tn3156-create-rich-previews-for-messages (indirectly, via search-result excerpt — the page itself did not return body content to WebFetch); github.com/mastodon/mastodon/issues/22382]`
**How to avoid:** Nothing to *fix* — this is a platform limitation, not a bug in this phase's implementation. Worth noting in the plan's UAT script so a reviewer pasting into iMessage doesn't mistake "no description shown" for a bug when Discord/Slack show it correctly.
**Warning signs:** None — expected behavior, document it so it isn't reported as a regression.

### Pitfall 4: iMessage's recommended image size (1200×1200+) diverges from Discord/Slack's 1200×630
**What goes wrong:** D-13 locks a 1200×630 asset (matching Discord/Slack/general OG convention and the ROADMAP's own assumption). Apple's TN3156 guidance (per search-result summary) recommends 1200×1200 *or larger* for Messages specifically, and iMessage title text is clipped at roughly 44 characters.
**Why it happens:** Different platforms optimized their card layouts differently; there is no single dimension that is simultaneously ideal everywhere.
**How to avoid:** This is informational, not a reason to reopen D-13 (which is locked and correctly optimizes for the ROADMAP's stated target of a large-format Discord/Slack card, the two platforms this app's community actually uses day-to-day per the phase goal). No action required unless UAT (pasting into a real iMessage thread, per the ROADMAP's own acceptance bar) shows the 1200×630 asset rendering unacceptably cropped/small on iMessage specifically — in which case it becomes a follow-up, not a Phase-78 blocker.
**Warning signs:** iMessage preview shows the image visibly cropped to a near-square center-crop of the 1200×630 asset (expected consequence of the aspect-ratio mismatch, not a bug).

### Pitfall 5: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`'s `PersistKeysToDbContext` requires the DbContext to already be constructible without circular dependency
**What goes wrong:** `QuestBoardContext`'s constructor already depends on `IActiveGroupContext` (for the query filter lambda). Data Protection's key-ring bootstrap happens very early in the host pipeline (potentially before a request scope exists), and `PersistKeysToDbContext` internally resolves `TContext` via DI when it needs to read/write keys.
**Why it happens:** `AddDbContext` registers `QuestBoardContext` as scoped by default; Data Protection's key manager needs to create a DbContext instance outside of a normal HTTP request scope (e.g., during host startup or a background key-generation operation).
**How to avoid:** This is a well-trodden path for ASP.NET Core apps (the whole point of `PersistKeysToDbContext` is to support exactly this), so it is expected to work with no special handling — `IActiveGroupContext`'s `ActiveGroupId` will simply resolve to `null` (no HttpContext, no override set) when `QuestBoardContext` is constructed for key-storage operations, which is harmless because `DataProtectionKey` carries no query filter. Flag for verification during implementation with a clean `dotnet ef migrations add` + a cold app start, not as a known blocker. `[ASSUMED — inferred from framework design intent, not verified by running the actual startup sequence in this session]`
**Warning signs:** A startup exception referencing `IActiveGroupContext` or `HttpContext` being null in a non-nullable-expecting path during Data Protection key initialization.

## Code Examples

### Signing and verifying the token (D-01, D-03, D-04)
```csharp
// Source: learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/purpose-strings
//         learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/overview
public class LinkSigningService(IDataProtectionProvider dataProtectionProvider)
{
    // Purpose string convention: planner's discretion per CONTEXT.md, but should be
    // stable across app restarts/deploys (it's part of what derives the actual key material
    // used, alongside the persisted key ring) — e.g. "QuestBoard.LinkPreview.v1"
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("QuestBoard.LinkPreview.v1");

    public string Protect(string entityType, int entityId, int groupId) =>
        WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(_protector.Protect($"{entityType}:{entityId}:{groupId}")));

    public bool TryUnprotect(string token, string expectedType, out int entityId, out int groupId)
    {
        entityId = 0; groupId = 0;
        try
        {
            var raw = _protector.Unprotect(
                Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)));
            var parts = raw.Split(':');
            if (parts.Length != 3 || parts[0] != expectedType) return false;
            return int.TryParse(parts[1], out entityId) && int.TryParse(parts[2], out groupId);
        }
        catch (CryptographicException)
        {
            // Tampered, malformed, or produced by a different purpose/key — reject, don't degrade.
            // LINKPREV-05.
            return false;
        }
        catch (FormatException)
        {
            // Malformed Base64Url input (not even a well-formed protected payload).
            return false;
        }
    }
}
```
`[CITED: learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/overview — CryptographicException on tamper/malformed/purpose-mismatch]`

### `ForwardedHeadersOptions` extended for D-05
```csharp
// Source: existing Program.cs pattern, extended per 78-CONTEXT.md D-05/D-06
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);
    }
    // If knownProxies ends up empty at runtime, ASP.NET Core 10 silently ignores ALL
    // forwarded headers for this app (see Pitfall 1) — this is not a no-op fallback to
    // "trust everyone", it fails closed to "trust no forwarded header at all".
});
```
`[VERIFIED: QuestBoard.Service/Program.cs:97-108, extended per CITED breaking-change behavior]`

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `ForwardedHeadersMiddleware` trusts any proxy when `KnownProxies`/`KnownNetworks` are empty | Trusts *nothing* when both are empty — headers from unconfigured sources are dropped entirely | ASP.NET Core 8.0.17 / 9.0.6 (baked into .NET 10 from GA) | This app's `X-Forwarded-For`-based rate-limit partitioning may already be silently broken in production if `ReverseProxy__KnownProxies__0` was never confirmed set — see Pitfall 1 |

**Deprecated/outdated:**
- The ROADMAP's scope note "keys already survive container restarts the same way auth cookies do" — D-03 already corrects this in CONTEXT.md; this research corroborates it independently via `docs/server-setup.md`'s deploy script (`rm -rf /opt/questboard/* && unzip ...` on every deploy — no volume, no persistence, identical to a container image update's effect).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | Apple's iMessage/Messages link-preview fetcher sends a `facebookexternalhit`/`Twitterbot`-spoofed, Mac-OS-identifying User-Agent (not an `iPhone`/`iPad` string) — cross-corroborated across several independent, non-official sources but not confirmed against an Apple-published UA string (Apple does not officially document this spoofing behavior) | Summary point 2, Pitfall 2 | Low — D-10 (standalone view, no `_Layout`) already makes this fact irrelevant to the implementation; the only risk is if the plan or its commit messages repeat the unverified "Apple sends iPhone/iPad" claim as settled fact |
| A2 | Adding `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to `QuestBoard.Repository.csproj` alone (no separate `Microsoft.AspNetCore.DataProtection` package reference) is sufficient for `AddDataProtection()` to resolve inside `ServiceExtensions.cs`, based on transitive-package-reference behavior inferred from this project's existing `PrivateAssets` usage pattern | Pattern 1 | Low — worst case is a `dotnet build` error requiring one additional explicit package reference, still inside `QuestBoard.Repository`; does not change the architecture or violate CLAUDE.md's EF-packages rule either way |
| A3 | `QuestBoardContext`'s constructor dependency on `IActiveGroupContext` will not cause a circular-dependency or null-reference failure when Data Protection's key manager constructs the context outside an HTTP request scope | Pitfall 5 | Medium if wrong — would surface immediately as a startup crash during `dotnet run`/first migration, easy to detect but would block the whole phase until resolved; recommend the first plan task be "add the package + migration + confirm clean app start" before building anything else on top |
| A4 | Apple's TN3156 recommends a 1200×1200-or-larger image and ignores `og:description` entirely — sourced from WebSearch result summaries of the Apple doc (the doc itself returned no extractable body to WebFetch, likely JS-rendered) and independently corroborated by a specific, closed GitHub issue against `mastodon/mastodon` | Pitfall 3, Pitfall 4 | Low — informational only; does not change any locked decision (D-13 stays 1200×630, D-14's description still renders correctly for Discord/Slack) |

**If this table is empty:** N/A — see entries above.

## Open Questions

1. **Is `ReverseProxy__KnownProxies__0` actually set on the production App CT right now?**
   - What we know: It was never confirmed at the Phase 32 UAT (2026-07-01); `docs/server-setup.md` shows it as a template value (`<TRAEFIK_CT_IP>`) in the env-file instructions, not a live value.
   - What's unclear: Whether the operator filled it in when the server was actually provisioned, independent of the doc.
   - Recommendation: This phase's deployment-acceptance step (already flagged in CONTEXT.md's Deferred list) should be promoted to a required pre-flight/verification task in the plan, not left purely deferred — because as of .NET 10, an unset value silently breaks more than the ROADMAP's scope note implies (see Pitfall 1). A single `curl -A Discordbot https://<prod-host>/s/quest/<a-valid-token>` against the deployed host, per ROADMAP success criterion 4, settles this definitively.

2. **Does `PersistKeysToDbContext` require any explicit `IServiceScopeFactory`/hosted-service handling to avoid a startup ordering issue with `QuestBoardContext`'s custom constructor?**
   - What we know: The package is designed for exactly this DbContext-backed-key-storage use case and is battle-tested across the ASP.NET Core ecosystem.
   - What's unclear: Whether *this specific* `QuestBoardContext` constructor (which takes `IActiveGroupContext` as a second constructor parameter, unlike the textbook single-`DbContextOptions`-parameter examples in Microsoft's docs) needs anything special.
   - Recommendation: First implementation task should be "wire the package + migration + verify `dotnet run` starts cleanly and a login round-trip still works" as an isolated, early checkpoint — before building the signing service or preview route on top of it.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET 10 SDK / runtime | Whole phase | ✓ | 10.0.9 (repo-pinned) | — |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` NuGet package | D-03 | Not yet installed — needs `dotnet add package` | 10.0.9 available on nuget.org | — |
| Reverse proxy (Traefik) forwarding `X-Forwarded-Proto`/`Host` | D-05, D-07 fallback path | Unconfirmed whether `KnownProxies` is set — see Open Question 1 | — | `EmailSettings:AppUrl` (D-07) is the primary path specifically because this fallback is unreliable |
| Real deployed host reachable from the internet | ROADMAP success criteria 1 and 4 | Not verifiable from this research session | — | None — this is inherently a deployment-time/human-UAT check, cannot be satisfied by CI |
| A real Discord/Slack/iMessage client to paste a link into | ROADMAP success criterion 1 | Not verifiable from this research session | — | None — see Validation Architecture |

**Missing dependencies with no fallback:**
- A live, publicly-reachable deployment and a real Discord channel/Slack workspace/iMessage thread to paste into — these cannot be simulated in CI or local dev; they are exactly what the ROADMAP names as the only valid acceptance bar for success criterion 1.

**Missing dependencies with fallback:**
- Reverse-proxy forwarded-header trust — `EmailSettings:AppUrl` fallback covers the card-metadata path even if proxy trust is misconfigured, per D-07's own stated rationale.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2, `Microsoft.NET.Test.Sdk` 18.7.0) — both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests` |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json`; `WebApplicationFactoryBase.cs` for the integration-test host wiring |
| Quick run command | `dotnet test QuestBoard.UnitTests` |
| Full suite command | `dotnet test` (runs Unit + Integration) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|---------------------|--------------|
| LINKPREV-01 | Absolute URLs behind reverse proxy honour forwarded scheme/host | integration (forwarded-header simulation) + human (real host `curl`) | `dotnet test --filter FullyQualifiedName~ForwardedHeaders` | ❌ Wave 0 — new test class needed |
| LINKPREV-02 | "Copy shareable link" mints a signed link, desktop+mobile | integration (HTML contains signed URL) + manual (real click-to-copy UX check) | `dotnet test --filter FullyQualifiedName~QuestDetailsCopyLink` | ❌ Wave 0 |
| LINKPREV-03 | Signed URL serves OG/Twitter tags | integration (assert response body contains `og:title`, `og:image`, `twitter:card=summary_large_image`) + human UAT (real Discord/Slack/iMessage paste, ROADMAP criterion 1 — cannot be automated) | `dotnet test --filter FullyQualifiedName~QuestPreview` | ❌ Wave 0 |
| LINKPREV-04 | Unsigned URL serves nothing new | integration (existing `QuestControllerAuthorizationRegressionTests`, rewritten per D-09) | `dotnet test --filter FullyQualifiedName~QuestControllerAuthorizationRegressionTests` | ✅ exists, needs rewrite (D-09 known breakage) |
| LINKPREV-05 | Tampered/malformed signature rejected | integration (flip one char of a valid token, assert no card) | `dotnet test --filter FullyQualifiedName~TamperedSignature` | ❌ Wave 0 |
| LINKPREV-06 | Card description is escaped, truncated plain text | unit (new truncator helper) + integration (end-to-end via preview route) | `dotnet test --filter FullyQualifiedName~CardDescriptionTruncation` | ❌ Wave 0 |
| LINKPREV-07 | Preview read scoped to signature's own group | integration (cross-group replay: sign for group A, request against group-B id) | `dotnet test --filter FullyQualifiedName~CrossGroupReplay` | ❌ Wave 0 |
| LINKPREV-08 | Branded image served unauthenticated, no redirect | integration (`GET` the image URL anonymously, assert 200 + no `Location` header) | `dotnet test --filter FullyQualifiedName~BrandedImageServing` | ❌ Wave 0 |
| LINKPREV-09 | Valid signature grants metadata only, never page access | integration (signed preview URL does not authenticate a subsequent POST/page GET) | `dotnet test --filter FullyQualifiedName~SignatureNotAuthentication` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests`
- **Per wave merge:** `dotnet test` (full suite, including `QuestBoard.IntegrationTests`)
- **Phase gate:** Full suite green before `/gsd-verify-work`, **plus** a mandatory human UAT step: paste a real signed quest link into an actual Discord channel (ROADMAP success criterion 1) and run `curl -A Discordbot https://<prod-host>/s/quest/{token}` against the deployed host (ROADMAP success criterion 4) — neither is satisfiable by the automated suite.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestPreviewControllerTests.cs` — covers LINKPREV-03, LINKPREV-05, LINKPREV-07, LINKPREV-09; must follow the `TenantIsolationTests.cs`/`ContactsControllerIntegrationTests.cs` pattern of `factory.TestGroupContext.ActiveGroupId = ...` for *seeding*, but must NOT set it before the preview-route request itself (the whole point is the anonymous caller starts with no group context and the route sets it from the token — see Pattern 2)
- [ ] `QuestBoard.UnitTests/Services/LinkSigningServiceTests.cs` — covers Protect/Unprotect round-trip, tamper rejection, cross-purpose rejection
- [ ] `QuestBoard.UnitTests/.../CardDescriptionTruncationTests.cs` (or wherever the new truncator lands) — word-boundary, HTML-escaping, empty-description-fallback cases
- [ ] `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs` — `Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow` must be rewritten to assert a login redirect, not 200 OK (D-09's known, deliberate breakage). **Additional finding beyond what D-09 already flags:** because `MutableGroupContext` defaults `ActiveGroupId = 1` for every test (including anonymous ones), this test's current 200-OK assertion was never actually exercising the production fail-closed-on-null-Session behavior it appears to describe — it was passing because the test harness always supplies *a* group context, unlike a real anonymous browser session. The rewritten test only needs to assert the `[Authorize]` redirect; it does not need to (and structurally cannot, given the harness) additionally re-prove the old Session-based 404 semantics. `[VERIFIED: QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:16, MutableGroupContext.cs — ActiveGroupId defaults to 1, not null]`

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | yes | Existing ASP.NET Core Identity cookie auth — untouched by this phase except D-09's `[Authorize]` addition |
| V3 Session Management | yes | D-12 explicitly forbids writing to `Session` from the preview path — the scoped `SetGroupId` override is the control that keeps this a metadata-only grant, not a session escalation |
| V4 Access Control | yes | Fail-closed EF query filter (`QuestBoardContext.cs:271-284`) is the standard control; `IgnoreQueryFilters()` is explicitly forbidden on this path per both ROADMAP and CONTEXT.md |
| V5 Input Validation | yes | The signed token is the only "input" on the anonymous path; `IDataProtector.Unprotect` is the validation control (throws `CryptographicException` on anything invalid) — no custom parsing of untrusted structure before that point |
| V6 Cryptography | yes | ASP.NET Core Data Protection (AES + HMAC under the hood, key-managed by the framework) — never hand-rolled, per Don't Hand-Roll above |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Cross-tenant data leak via a broadened read path (this app's own recorded history, Phases 49/55) | Information Disclosure | Fail-closed query filter re-applied via `SetGroupId`, never `IgnoreQueryFilters()` — D-12 |
| Token replay across tenants (signing an id without a group scope) | Spoofing / Elevation of Privilege | Signature covers `(type, id, groupId)` together — D-04, tested via LINKPREV-07's cross-group replay |
| Token accepted as authentication rather than metadata-only grant | Elevation of Privilege | LINKPREV-09 — the signed token must never satisfy `[Authorize]` or any POST; it only ever reaches the anonymous-allowed preview action |
| Unauthenticated image-serving endpoint as a content-sniffing vector | Tampering / Information Disclosure | Out of scope for Phase 78 (D-13's image is a static `wwwroot` file served by `UseStaticFiles()`, not a database-backed unauthenticated byte stream) — this becomes directly relevant in Phase 79 (LINKCARD-04/05) for character/contact portraits, not here |
| Reverse-proxy header spoofing (a caller reaching Kestrel directly and injecting `X-Forwarded-Host`) | Spoofing | `ForwardedHeadersOptions.KnownProxies` bounding — D-06, and per Pitfall 1, this is enforced even more strictly by default on .NET 10 than the CONTEXT.md text assumes |

## Sources

### Primary (HIGH confidence — direct codebase inspection)
- `QuestBoard.Service/Program.cs` — `ForwardedHeadersOptions`, DI registrations, pipeline order
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — query filters, constructor shape
- `QuestBoard.Service/Services/ActiveGroupContextService.cs`, `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs`
- `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs`, `TenantIsolationTests.cs`, `ContactsControllerIntegrationTests.cs`
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` (`WebEncoders.Base64UrlEncode/Decode`)
- `QuestBoard.Service/Jobs/*EmailJob.cs`, `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (`EmailSettings:AppUrl` pattern)
- `QuestBoard.Domain/Services/MarkdownService.cs`, `IMarkdownService.cs`
- `QuestBoard.Repository/QuestBoard.Repository.csproj`, `QuestBoard.Service/QuestBoard.Service.csproj`
- `docs/server-setup.md`, `QuestBoard.Service/appsettings.json`, `QuestBoard.Service/Views/Shared/_Layout.cshtml`, `_Toasts.cshtml`
- `wwwroot/images/Blanks/`, `Ruined Posters/`, `Wax Seals/` (source art paths, confirmed to exist)

### Secondary (MEDIUM confidence — official documentation)
- [Breaking change: Forwarded headers middleware ignores X-Forwarded-* headers from unknown proxies](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0) — Microsoft Learn, `defaultMoniker: aspnetcore-10.0`
- [Consumer APIs overview for ASP.NET Core Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/overview) — `CryptographicException` behavior
- [EntityFrameworkCoreDataProtectionExtensions.PersistKeysToDbContext](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.dataprotection.entityframeworkcoredataprotectionextensions.persistkeystodbcontext?view=aspnetcore-10.0)
- [NuGet: Microsoft.AspNetCore.DataProtection.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/) — version index confirmed via `api.nuget.org`
- [About Discord Link Previews and the Discordbot](https://support.discord.com/hc/en-us/articles/42500550752919-About-Discord-Link-Previews-and-the-Discordbot) (title/summary only — 403 on direct fetch, corroborated via search-result excerpt)
- [Slack Robots](https://api.slack.com/robots) (Slackbot-LinkExpanding UA, referenced via search)
- [TN3156: Create rich previews for Messages](https://developer.apple.com/documentation/technotes/tn3156-create-rich-previews-for-messages) (title confirmed; body not directly retrievable — see Assumptions Log A4)

### Tertiary (LOW confidence — WebSearch aggregation, cross-corroborated where noted)
- Discord `og:image` size guidance (1200×630, `twitter:card=summary_large_image` required for large embed) — multiple SEO/OG-tooling blog sources, consistent with each other and with the ROADMAP's own pre-existing assumption
- Apple iMessage User-Agent spoofing (`facebookexternalhit`/`Twitterbot`/Mac-OS string) — SecurityWeek, two independent Medium articles, rsmck.co.uk — mutually corroborating, no official Apple confirmation
- `mastodon/mastodon` GitHub issue #22382 — confirms Messages ignores `og:description`, closed as "not planned"
- `discord/discord-api-docs` discussion #6385 — Discord's image-proxy User-Agent behavior (tangential; confirms Discord does cache/proxy fetched images server-side, corroborating D-02's "external caches are permanent" premise)

## Metadata

**Confidence breakdown:**
- Standard stack (Data Protection package, `WebEncoders`): HIGH — verified against both the actual codebase and nuget.org/Microsoft Learn
- Architecture (EF wiring across the Repository/Service boundary, `IActiveGroupContext` widening): HIGH — directly derived from reading the actual DI registration code and test harness, not inferred
- Crawler requirements (Discord/Slack/iMessage): MEDIUM — official Discord support article and Apple TN3156 exist and were located, but neither returned full body content to automated fetch; findings rest on WebSearch-aggregated summaries cross-checked across multiple independent sources, not on a single authoritative primary read
- Pitfalls: HIGH for the ForwardedHeaders/.NET 10 finding (directly sourced from an official, dated Microsoft Learn breaking-changes page with the correct moniker); MEDIUM for the Apple UA/description/image-size findings (cross-corroborated but not officially confirmed)

**Research date:** 2026-08-26
**Valid until:** 30 days for the internal-codebase findings (stable until the code changes); 90 days for the ASP.NET Core/.NET 10 breaking-change finding (a documented, dated platform behavior, unlikely to change again soon); 14 days for the Discord/Slack/iMessage crawler-behavior findings (these vendors change unfurl behavior without notice — re-verify via the mandatory human-UAT step at execution time regardless of how "current" this research is)
