# Phase 78: Link Preview Foundation and Quest Cards - Context

**Gathered:** 2026-08-26
**Status:** Ready for planning

<domain>
## Phase Boundary

A quest link, minted deliberately by a board member through a "Copy shareable link" control, unfurls as a rich Open Graph / Twitter card in Discord, Slack, and iMessage — while the quest page behind it stays locked, and an ordinary quest URL leaks nothing.

This phase also lays the foundation Phase 79 extends to characters and contacts: the signing scheme, the absolute-URL fix, the preview route, and the plain-text summarizer.

Out of scope: per-quest generated card images, an interactive Spotify-style embed (unreachable — those come from Discord's and Slack's hardcoded provider allowlists), and any change to character or contact sharing.

</domain>

<decisions>
## Implementation Decisions

### Signing scheme and link lifetime

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

### Absolute URLs behind the reverse proxy

- **D-05: `Program.cs:103` gains `ForwardedHeaders.XForwardedProto` and `XForwardedHost` alongside the existing `XForwardedFor`.** Today only `XForwardedFor` is set, so behind Traefik every absolute URL the app generates is wrong in both scheme and host. This is the prerequisite the ROADMAP names, and it is correct for the whole application, not only for cards.

- **D-06: Host trust stays bounded by `ReverseProxy:KnownProxies`; `AllowedHosts` stays `"*"`.** `ForwardedHeadersOptions` only honours forwarded headers from IPs listed as known proxies, so a caller reaching Kestrel directly cannot inject a host. This keeps the Phase 32 decision — trust is config-driven, set per-environment via `ReverseProxy__KnownProxies__0` — rather than introducing a second, hardcoded trust mechanism.

  Rejected: tightening `AllowedHosts` to the production hostname. It is real defence in depth, but a wrong or unset value returns 400 for *every* request — a total outage rather than a degradation — and D-07 already removes host spoofing from the card path entirely.

  **Required pre-flight verification, not a deferred item — upgraded by research.** From ASP.NET Core 8.0.17/9.0.6 onward, and therefore in .NET 10 from GA, `ForwardedHeadersMiddleware` **fails closed**: with `KnownProxies` and `KnownNetworks` both empty it silently drops *every* `X-Forwarded-*` header rather than trusting them. The Phase 32 UAT deferred confirming `ReverseProxy__KnownProxies__0` was actually set on the App CT and it has never been confirmed since (2026-07-01). If it is unset, then D-05 is a no-op **and the `X-Forwarded-For`-based forgot-password rate limiting Phase 32 shipped is already silently broken in production today** — a pre-existing defect this phase would otherwise inherit blind. Confirming it must be an explicit task, and it is cheap: one `curl -A Discordbot` against the deployed host settles it. D-07 is what keeps a wrong answer from becoming a no-card failure.

- **D-07: `EmailSettings:AppUrl` is the canonical base URL for card metadata and for the copied share link, with request-derived values as the fallback when it is absent.** It is already the application's single answer to "what is my public URL", it is already set correctly in the server's env file, and working production email links are standing proof of that. Reading it makes `og:url`, `og:image`, and the copied link deterministic even if D-06's proxy trust is misconfigured.

  **Note on the config file:** `appsettings.json:30` shows `https://localhost:8001` and `docs/server-setup.md` does not list `EmailSettings__AppUrl` among the env vars. Neither is evidence the production value is wrong — the server env file overrides appsettings and is not fully mirrored in the doc. The doc is incomplete, not the config.

  Rejected: a new dedicated key such as `PublicBaseUrl`. Two config keys meaning the same thing is the near-identical-sources drift class this project has repeatedly been bitten by, and a forgotten second env var would let the two disagree. Rejected: renaming `AppUrl` into a shared key — it touches every email template and `EmailPreviewController`, and requires renaming the env var on the server in the same deploy or every email link breaks.

  **Naming smell, accepted:** a link-preview feature reading a key under `EmailSettings` is untidy. Deferred as a rename, not fixed here.

### The preview route

- **D-08: The signed link points at a dedicated, anonymous-allowed preview route (e.g. `/s/quest/{token}`), not at the quest URL with a query parameter.** `QuestController.Details` is not touched by the preview path at all, so no future change to that action can widen what an anonymous signed caller sees. The endpoint is small enough to audit in one sitting, and Phase 79 adds sibling routes alongside it rather than adding a second branch to a second controller.

  **Accepted cost:** the copied link is not the quest's own URL, so a member who clicks it takes one extra hop (D-11 makes that hop automatic).

- **D-09: `QuestController.Details` GET gains `[Authorize]`.**

  **This closes a real gap that this phase would otherwise open.** Today `Details` GET has no `[Authorize]` (`QuestController.cs:306-307`), `GroupSessionMiddleware` explicitly passes anonymous requests through, and the fail-closed query filter at `QuestBoardContext.cs:281` returns nothing — so an anonymous caller gets a 404. **That 404 is currently the page's only security boundary.** This phase deliberately sets group context from a verified signature; the moment it does, that protection evaporates and an anonymous holder of a signed link would render the entire quest page. `[Authorize]` makes the login requirement explicit and independent of group context.

  This also delivers ROADMAP success criterion 6 — a signed link opened logged-out lands on the login page — which the current 404 does not satisfy.

  **Known breakage the planner must handle:** `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs:37` (`Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow`) asserts an anonymous client receives **200 OK**. Adding `[Authorize]` changes that to a login redirect. The test must be updated to assert the redirect — it is a deliberate behaviour change, not a regression, and it must not be "fixed" by weakening the attribute.

  LINKPREV-04 still holds: an unsigned quest URL serves no quest data to an unauthenticated caller. Only the *shape* of the refusal changes, from 404 to a login redirect.

- **D-10: The preview response is a standalone minimal HTML document that uses no application layout, and carries `<meta name="robots" content="noindex">`.**

  **This deliberately overrides the ROADMAP's stated mechanism** ("a shared partial rendered into `_Layout.cshtml`'s `<head>` through a section"). `_Layout.cshtml` has no head section at all (only `Scripts` at line 225), so that mechanism is new plumbing in both layouts — and card presence would then depend on which layout `MobileDetectionMiddleware` picked from the caller's User-Agent, which is precisely the coupling the ROADMAP's own locked decision forbids ("card presence is decided by the signature, never by User-Agent"). A standalone view removes the coupling instead of mitigating it. Related bug class: the "mobile markup that was never selected" case PROJECT.md records against `_Layout.Platform.Mobile.cshtml`.

  **Correction (Phase 78 research, 2026-08-26):** the original rationale for this decision claimed Apple's iMessage fetcher plausibly sends an `iPhone`/`iPad` User-Agent and would therefore be served the mobile layout. **That premise is wrong** — Apple's fetcher presents a desktop Mac Safari string carrying `facebookexternalhit`/`Twitterbot` signatures, and `MobileDetectionMiddleware` would not match it. The decision stands unchanged on the stronger grounds above; only the worked example was mistaken. See `78-RESEARCH.md` § Assumptions Log.

  The ROADMAP's *intent* — one shared markup surface that Phase 79 extends rather than copies — is fully preserved: Phase 79 extends this view. Only the host changes. As a bonus, no normal page carries a conditional meta block it never uses.

- **D-11: Every caller receives the identical response — HTTP 200, the meta tags, a `<meta http-equiv="refresh">` to the quest page, and a visible link as fallback.** Card presence is decided by the signature and nothing else; there is no User-Agent branching anywhere on this path, per the ROADMAP's locked decision. Crawlers read the tags and stop; browsers follow the refresh to `/Quest/Details/{id}`, where D-09 sends a logged-out visitor to the login page and a member to the quest.

  Rejected: a 302 straight to the quest page. A 302 carries no body, so a crawler that does not follow redirects gets no meta tags at all — the ROADMAP warns explicitly that crawlers do not reliably follow redirects.

- **D-12: The preview route scopes its single read by setting an in-memory group override — `ActiveGroupContextService.SetGroupId(...)` with the signature's verified group id — and never writes `ActiveGroupId` into Session.**

  `SetGroupId` already exists for exactly this shape: Hangfire jobs use it to scope a read with no HttpContext. The fail-closed query filter then does the work unchanged. **`IgnoreQueryFilters()` is forbidden on this path** — this app has shipped two real cross-tenant leaks (Phases 49/55) and the filter is the remedy.

  **Writing the group id into Session would be a genuine privilege escalation** — it would hand an anonymous visitor a live group context for the remainder of their session, converting a metadata token into board access. `SetGroupId` sets a scoped in-memory override that dies with the request; that distinction is the whole point.

  **Settled by research, no longer discretionary: `IActiveGroupContext` must be widened with `SetGroupId(int?)`.** Resolving the concrete `ActiveGroupContextService` works in production but **silently no-ops under test** — `WebApplicationFactoryBase.cs` registers a singleton `MutableGroupContext` for `IActiveGroupContext`, a different object from the scoped service production delegates to. Widening the interface is the only shape that behaves identically in both, and the alternative fails in the worst possible way: green tests over a broken preview path.

### The card itself

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
- ~~Whether `IActiveGroupContext` is widened to expose `SetGroupId` or the concrete service is resolved directly.~~ **Settled by research — the interface must be widened; see D-12.**
- Button placement, iconography, and wording on desktop and mobile, and the copy-confirmation mechanism (toast vs inline). The project's `_Toasts.cshtml` is available in both layouts.
- Exact fallback description wording, exact truncation length, and the ellipsis character.
- The meta-refresh delay, and the wording of the visible fallback link.
- Test structure beyond the required cross-group replay test and the `curl -A Discordbot` check.
- Where the docs paragraph lives (`README.md`, `docs/server-setup.md`, or a new doc).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` § "Phase 78: Link Preview Foundation and Quest Cards" — goal, success criteria, scope notes, locked decisions, and the risk list this phase must actively avoid. Note D-03 and D-10 above deliberately correct/override two of its scope notes.
- `.planning/ROADMAP.md` § "Phase 79: Character and Contact Link Cards" — the phase that inherits this signing scheme, preview view, absolute-URL helper, and summarizer. Read it to avoid building something Phase 79 cannot extend.
- `.planning/REQUIREMENTS.md` — LINKPREV-01 through LINKPREV-09.

### Deployment and reverse proxy
- `docs/server-setup.md` §3 and line 213 — App CT env file, `ReverseProxy__KnownProxies__0`, and the note on what breaks without it. Incomplete relative to the live env file on the server; treat it as a guide, not an inventory.
- `.planning/codebase/INTEGRATIONS.md:144` — environment variable reference.

### Prior decisions this phase must honour
- `.planning/PROJECT.md` § Key Decisions (line 194) — ForwardedHeaders trust is config-driven, set at deploy time.
- `.planning/PROJECT.md` § Known Issues — the `_Layout.Platform.Mobile.cshtml` dead-code case and the drift bugs blamed on duplicated markup; both inform D-10.
- `.planning/phases/74-event-schema-crud-and-calendar-display/74-CONTEXT.md` — D-04 (fail-closed group filters), D-06 (`ExtractPlainText` as the single plain-text mechanism), D-09 (one partial, not a copy), D-16 (verify mobile with a real UA).
- `.planning/phases/72-change-character-on-an-existing-signup/72-CONTEXT.md` — D-13 (defence in both layers for group scoping).
- `.planning/milestones/v5.0-phases/32-first-login-password-flow/32-HUMAN-UAT.md:45` — the original `ForwardedHeaders` fix and the deferred deployment-time confirmation D-06 flags.

### Code the plan will touch or depend on
- `QuestBoard.Service/Program.cs:101-110` — `ForwardedHeadersOptions`; `:289` — `UseForwardedHeaders()` position in the pipeline.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:271-284` — the fail-closed group filter and the "do not capture ActiveGroupId into a local" rule.
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — `SetGroupId` and the Session-backed read path.
- `QuestBoard.Domain/Interfaces/IMarkdownService.cs:26` and `QuestBoard.Domain/Services/MarkdownService.cs:160` — `ExtractPlainText`.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:306` — `Details` GET, the action gaining `[Authorize]`.
- `QuestBoard.IntegrationTests/Controllers/QuestControllerAuthorizationRegressionTests.cs:37` — the test D-09 knowingly breaks.
- `CLAUDE.md` — no planning/tracking IDs in source comments; modern card UI pattern; EF packages belong only in `QuestBoard.Repository`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`ActiveGroupContextService.SetGroupId(int?)`** — already exists for Hangfire's request-less, group-scoped reads. This is the exact seam D-12 needs, and it is what makes "set the group context from the signature" possible without ever reaching for `IgnoreQueryFilters()`.
- **`IMarkdownService.ExtractPlainText`** — LINKPREV-06's tool already exists and is already unit-tested (`MarkdownServiceTests.cs`), including word-boundary and deeply-nested-input cases.
- **`EmailSettings:AppUrl`** — an existing, production-set absolute base URL, consumed today by every email template and `EmailPreviewController`.
- **`_Toasts.cshtml`** — present in both layouts; the established copy-confirmation surface (Phase 72 D-14).
- **`wwwroot/images/Blanks/`, `Ruined Posters/`, `Wax Seals/`** — source art for D-13's composite. All portrait or square, all 0.15–2.4 MB, all with spaces in their filenames.

### Established Patterns
- **Fail-closed group query filters** (`QuestBoardContext.cs:271-284`) — a null `ActiveGroupId` returns zero rows, never every group's rows merged. Two real cross-tenant leaks (Phases 49/55) are why.
- **Config-driven proxy trust** — `ReverseProxy:KnownProxies`, empty by default, set per-environment via env var.
- **Desktop/mobile view pairs** — `Details.cshtml` + `Details.Mobile.cshtml`, selected by `MobileDetectionMiddleware` on a User-Agent keyword match.
- **Per-action authorization** — `QuestController` has no class-level `[Authorize]`; each action carries its own.

### Integration Points
- `Program.cs` — `ForwardedHeadersOptions` (D-05) and a new `AddDataProtection()` registration (D-03).
- A new anonymous-allowed preview controller/route (D-08), plus its standalone view (D-10).
- `QuestController.Details` — gains `[Authorize]` (D-09) and the render-time signed URL for the view (D-15).
- `Views/Quest/Details.cshtml` and `Details.Mobile.cshtml` — the copy control (D-15).
- A new EF migration for the Data Protection keys table (D-03).

### Landmines
- **No `AddDataProtection()` exists**, and the app service mounts no volume — the key ring is ephemeral across container recreation. D-03 exists because of this.
- **`MobileDetectionMiddleware` switches layout on `iPhone`/`iPad`** — so any markup placed in a layout has its presence decided by the caller's User-Agent. Research confirmed Apple's iMessage fetcher does *not* match these keywords (it sends a desktop Mac Safari string), so no currently-known target crawler trips this. D-10 removes the coupling anyway rather than depending on that staying true.
- **The integration-test harness does not reproduce production group scoping** — `WebApplicationFactoryBase.cs` registers a singleton `MutableGroupContext` defaulting `ActiveGroupId = 1` for every test regardless of auth state. This is why `Details_Anonymous_...` currently returns 200, and it means a preview path that resolved the concrete `ActiveGroupContextService` would pass its tests while doing nothing. See D-12.
- **`_Layout.cshtml` has no head/`Styles` section** — only `Scripts` at line 225. `_Layout.Mobile.cshtml` has both.
- **`Details` GET is not `[Authorize]`d** — anonymous access is blocked only by the query filter returning nothing. D-09 exists because this phase removes that accidental protection.
- **`ReverseProxy__KnownProxies__0` has never been confirmed set in production** (deferred at the Phase 32 UAT, 2026-07-01). If unset, forwarded headers are silently ignored.

</code_context>

<specifics>
## Specific Ideas

- Target clients are Discord, Slack, and iMessage. Acceptance must be an actual paste into a real Discord channel, not markup inspection or a local `curl` — a wrong scheme, a relative `og:image`, or a redirect on the image URL each produce silence rather than an error.
- `curl -A Discordbot` against a signed URL on the **deployed host** must return `og:url` and `og:image` as absolute `https://` URLs on the real hostname.
- An integration test must prove a signature minted for a quest in group A yields nothing when replayed against a quest id in group B.
- Changing a single character of the signature must render no card — rejected outright, not degraded to a generic card.

</specifics>

<deferred>
## Deferred Ideas

- **Rename `EmailSettings:AppUrl`** to a properly-scoped public base URL key used by both emails and link previews. Correct, but it touches every email template, `EmailPreviewController`, and the server env var, and must be a coordinated deploy. Not this phase (D-07).
- **A time-limited or revocable share link.** Deliberately rejected for now (D-01, D-02); `ITimeLimitedDataProtector` remains a cheap retrofit if link exposure ever becomes a real concern.
- **Per-quest generated card images.** Explicitly out of scope in the ROADMAP; D-13 ships one static branded asset.
- **Tightening `AllowedHosts`** from `"*"` to the production hostname as defence in depth (D-06). Worth doing, but its failure mode is a total outage, so it wants its own change with its own verification.
- **Confirming `ReverseProxy__KnownProxies__0` is set on the App CT.** Outstanding since the Phase 32 UAT (2026-07-01). This phase should verify it as part of deployment acceptance rather than assume it.

</deferred>

---

*Phase: 78-link-preview-foundation-and-quest-cards*
*Context gathered: 2026-08-26*
