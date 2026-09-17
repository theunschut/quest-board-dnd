# Phase 84: Calendar Feed Foundation and Event Subscription - Research

**Researched:** 2026-09-17
**Domain:** RFC 5545 iCalendar generation, anonymous token-bearer HTTP endpoints, ASP.NET Core tenant-isolation patterns
**Confidence:** MEDIUM — the codebase-grounded findings (query filters, middleware, pipeline order, entity shapes) are HIGH confidence because every claim was read from the source this session. The client-behaviour findings (X-WR-CALNAME, refresh cadence, VALARM, SEQUENCE-on-refetch) are LOW-to-MEDIUM confidence because Apple, Google and Microsoft do not publish this behaviour — every source is third-party observation, not vendor documentation, and it can change without notice.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Which events reach the feed**
- **D-17**: The feed is scoped to events the viewer holds an `EventSignup` row on — deliberately diverges from My Agenda (Phase 82 D-01, which is board-membership scoped). Structural consequence: the query starts from `EventSignups` filtered to the viewer, not from `Events`. This is a *third* query shape, not a variant of either existing one. It still carries Phase 82's membership scoping, the `IgnoreQueryFilters()` + pinned-set pattern, and a second in-memory re-check.
- **D-18**: Every event with a row appears regardless of the vote; the answer is carried as a title suffix, only for `(maybe)` and `(declined)`. A plain title means either "answered yes" or "never answered" (a campaign auto-row must not be marked "Yes" since `HasAnswered == false` there).

**Time, duration and all-day entries**
- **D-01**: Floating local time — no `TZID`, no `VTIMEZONE`. `DTSTART:20260920T190000` with no zone.
- **D-02**: A timed event is a fixed one-hour block (`DTSTART`/`DTEND` an hour apart). A null `StartTime` is a true all-day entry (`VALUE=DATE`, exclusive next-day `DTEND`). The hour is invented and must not be presented in the UI as though the board knows how long an event runs.
- **D-03**: `TRANSP:TRANSPARENT` on every entry — never marks the reader busy.
- **D-04**: No `VALARM`. Research should confirm client treatment before anyone relies on its absence *or* presence (see Common Pitfalls).

**Token lifecycle and revocation**
- **D-05**: A dedicated token table — many named, independently revocable subscriptions per user. Not a column on `UserEntity`, not a signed stateless payload.
- **D-06**: The token is stored as-is (not hashed) and its full address stays visible on Profile, re-copyable at any time. Serving the feed is a direct indexed lookup.
- **D-07**: Each row shows name, address, created date, last-fetched timestamp; supports rename and delete. The fetch-time write must be throttled so a hammered address cannot become a write storm.
- **D-08**: A revoked subscription answers `410 Gone`; an address that never existed answers `404 Not Found`. This forces a tombstone (`RevokedAt` column) — there is no hard `DELETE` on this table. "Delete" in the UI means revoke.

**What each entry says**
- **D-09**: The title is `[Board] Title`.
- **D-10**: No `DESCRIPTION` at all. Retires the ROADMAP's "Markdown leaking into DESCRIPTION" risk — no `IMarkdownService` plain-text target needed.
- **D-11**: No `URL` and no link of any kind. The cross-board deep-link problem is removed from this phase, not solved.
- **D-12**: A cancelled event is dropped from the feed entirely (reuses `CancelledAt == null` verbatim), not marked `STATUS:CANCELLED`.

**Feed window and the subscribe surface**
- **D-13**: A rolling date window — a few months of history, roughly a year ahead — recomputed on every fetch, exact numbers are the planner's to fix, configurable in the manner of `AgendaOptions`. No `Take`, no roster join (no `Include(Signups).ThenInclude(User)`).
- **D-14**: Copy button, a `webcal://` link, and a QR code. Adds the phase's only new dependency (QR generation).
- **D-15**: A section on the existing Profile page, on both `Profile.cshtml` and `Profile.Mobile.cshtml` — not its own page. Both layouts ship together.
- **D-16**: Nothing is minted until the reader presses Add.

### Claude's Discretion
- Absolute base URL comes from `EmailSettings:AppUrl`, following Phase 78 D-07's reasoning (request-derived URLs are wrong until the forwarded-header fix ships in the unshipped Phase 78).
- The all-day `VALUE=DATE` mapping and its exclusive next-day `DTEND`.
- `UID` scheme — stable across polls, namespaced by source (a quest and an event can share an integer id; Phase 85 adds the second source).
- Rate limiting on the anonymous endpoint, patterned from `Program.cs`'s `forgot-password`/`set-password` policies.

### Deferred Ideas (OUT OF SCOPE)
- Quests in the feed — Phase 85 (one-shot boards only, only quests the viewer is signed up for, only once finalized).
- A per-board feed URL (rejected in favour of one personal cross-board feed).
- Marking a never-answered campaign event — D-18 dropped `(no answer)`; revisit only if it proves misleading.
- Renaming `EmailSettings:AppUrl` — deferred in Phase 78 D-07, not this phase's concern.

### Open for research (this document resolves these below)
- What the calendar is called on the reader's phone (`X-WR-CALNAME`) — see Common Pitfalls, Pitfall 1.
- How fast a change reaches a device (`X-PUBLISHED-TTL`/`REFRESH-INTERVAL`, `ETag`/conditional requests) — see Common Pitfalls, Pitfall 2.
- The feed route's exemption from `GroupSessionMiddleware` — see Architecture Patterns, Pattern 3.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs exist yet for this phase — ROADMAP.md lists `**Requirements**: TBD` for Phase 84, to be minted during planning (matching the pattern Phases 80–82 used: mint the family into REQUIREMENTS.md as the first plan of wave 1). This research is organized around the ROADMAP goal and the 18 CONTEXT.md decisions instead of REQ-IDs; the planner should mint requirement IDs (a natural prefix would be `CALFEED-*`) covering, at minimum: token mint/rename/revoke, the ICS writer's per-decision output shape (D-01 through D-13), the anonymous feed endpoint's response codes (D-08) and tenant isolation, the Profile UI on both layouts (D-14/D-15/D-16), and rate limiting.
</phase_requirements>

## Summary

This phase is a hand-rolled RFC 5545 writer serving a `text/calendar` feed from a brand-new, fully anonymous, token-authenticated endpoint — the first anonymous, no-active-group, machine-polled read surface this application has ever shipped. Every decision that would normally make this phase risky has already been resolved in CONTEXT.md: no timezone handling, no recurrence expansion, no description, no link, no alarms. What is left is mechanically precise but genuinely dangerous in two specific ways that this research grounds in the actual codebase.

First, **every tenant query filter in this application fails closed on a null `ActiveGroupId`**, confirmed by reading `QuestBoardContext.cs` directly: `EventEntity` (line 530-533), `EventSignupEntity` (line 544-547), and every other group-scoped entity return **zero rows**, not "every group's rows," when no group is active. Because the feed endpoint has no session, no cookie, and by construction no active group, any query that does not explicitly `IgnoreQueryFilters()` and re-pin the group predicate from a freshly-read membership list will silently return an empty feed — indistinguishable from "no events scheduled." Phase 82's `EventService.GetCrossBoardAgendaAsync` (`QuestBoard.Domain/Services/EventService.cs:82-126`) is the direct precedent for the pattern (pin by `memberGroupIds`, second-layer re-check, `LogError` on any surviving foreign row) but its underlying repository read (`GetUpcomingAcrossGroupsWithSignupsAsync`, `EventRepository.cs:160-190`) starts from `Events`. D-17 requires starting from `EventSignups` instead — a new repository method is required; neither existing method can be widened into it.

Second, **`GroupSessionMiddleware` passes anonymous requests through untouched** (confirmed by reading the middleware directly — the very first check at line 101 is `if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }`), so a real calendar client (which never sends a cookie) is unaffected regardless of where the new controller lives. The only open question is what an *authenticated* user sees opening the same URL in a browser with no active board: unless the new controller's route is added to `ExemptPathPrefixes` (`GroupSessionMiddleware.cs:59-84`), that GET is redirected to `/groups/pick`. This is a real, observable behavior difference worth a deliberate decision, not a silent side effect.

**Primary recommendation:** hand-roll the ICS writer (it is genuinely tiny after D-10/D-11 dropped `DESCRIPTION` and `URL` — five fields per VEVENT), do not add Ical.Net; use QRCoder's `SvgQRCode` renderer for D-14 (zero native dependencies, works in the project's Debian-based container with no image-encoder or libgdiplus install step); scope the endpoint under a brand-new, small, `[AllowAnonymous]` controller outside `/Account` so its exemption from `GroupSessionMiddleware` is a deliberate, visible line rather than inherited by accident; and treat every client-behaviour claim below (refresh cadence, `X-WR-CALNAME`, `VALARM`, `SEQUENCE`) as LOW-confidence UI-copy guidance, never as a promise.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| ICS document generation (VEVENT writer) | API / Backend | — | Pure formatting logic over already-loaded domain models; no view, no client code involved |
| Feed authentication (token lookup) | API / Backend | Database / Storage | Direct indexed lookup against the token table; the token itself is the credential, not a session |
| Tenant-safe event read (D-17 query) | API / Backend | Database / Storage | `IgnoreQueryFilters()` + pinned membership predicate lives in the Repository layer; the safety invariant is enforced in the Domain service's second-layer re-check |
| Subscription management UI (add/rename/revoke) | Frontend Server (SSR) | Browser / Client | Razor views + POST actions on Profile, both layouts; no client-side state beyond the existing modal/form idioms already in this codebase |
| QR code rendering | API / Backend | — | Server-generates the SVG at request time from the already-known feed URL; no client-side QR library needed |
| Rate limiting | API / Backend | — | ASP.NET Core's built-in `RateLimiter` middleware, same mechanism as `forgot-password`/`set-password` |
| `webcal://` / `https://` link presentation | Browser / Client | Frontend Server (SSR) | The server emits both URL forms as plain markup; the browser/OS decides which app handles the `webcal:` scheme click |

## Standard Stack

### Core

No new *required* package for the ICS writer — see "Hand-rolled vs a library" below, a build decision rather than a stack recommendation.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| QRCoder | 1.8.0 `[ASSUMED — WebSearch, not yet confirmed via `npm view`-equivalent `dotnet` command in this session]` | Server-side QR code generation for D-14 | Most-downloaded .NET QR package (~80M downloads), zero dependencies, its `SvgQRCode` renderer needs no `System.Drawing`/`libgdiplus`, matching this project's Debian-slim container with no image-processing apt layer today |

### Supporting

None required beyond the above. `Microsoft.AspNetCore.WebUtilities.WebEncoders` (already referenced by `AccountController.cs` for password-reset tokens) supplies `Base64UrlEncode` for the bearer token — no new package needed for token generation, `System.Security.Cryptography.RandomNumberGenerator` is part of the BCL.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled ICS writer | Ical.Net (v5.2.3, `github.com/ical-org/ical.net`, ~36.3M downloads) | Ical.Net is mature and RFC-5545-correct out of the box (folding, escaping, CRLF all handled), but this feed emits a fixed five-field VEVENT with no recurrence, no timezone, no attendees — the entire feature surface Ical.Net exists to manage is explicitly out of scope per D-01/D-10/D-11/D-17-emits-one-VEVENT-per-occurrence. A ~40-line hand-rolled writer with a unit test asserting exact byte output (folding, CRLF, escaping) is more auditable than learning and pinning a library API for five fields, and matches this project's stated pattern of taking on a package only where "the format was the risk" (Markdig/HtmlSanitizer/AngleSharp) — here the format risk is small enough to own directly. **This is a build decision, not a locked one — flag it to the operator at plan-review if there is any appetite to reconsider**, since the codebase's precedent cuts both ways (it *has* taken dependencies for format risk before). |
| QRCoder's `SvgQRCode` | QRCoder's `PngByteQRCode` | `PngByteQRCode` also avoids `System.Drawing` and would work, but SVG scales cleanly at any Profile-page zoom/DPI with a smaller markup footprint and no separate image-encoding step; prefer SVG per the roadmap's implicit "no image encoder needed" framing |
| QRCoder | `Net.Codecrete.QrCodeGenerator` | A smaller, pure-C# alternative with no dependencies either; QRCoder is preferred only because of its dramatically larger install base and the project's existing bias toward well-known, high-download packages (Markdig, HtmlSanitizer, AngleSharp are all >1M+ downloads) |

**Installation:**
```bash
dotnet add QuestBoard.Service package QRCoder --version 1.8.0
```
QRCoder is a rendering/presentation concern (SVG markup for a view), so it belongs in `QuestBoard.Service`, not `QuestBoard.Domain` or `QuestBoard.Repository` — consistent with CLAUDE.md's "EF packages belong only in `QuestBoard.Repository`" rule generalized to "presentation packages belong in `QuestBoard.Service`."

**Version verification:** `npm view`-equivalent for NuGet is `dotnet list package` (post-install) or a direct registry query. This session confirmed both packages' current versions directly against the NuGet v3 flat-container API:
```
curl -s https://api.nuget.org/v3-flatcontainer/qrcoder/index.json   # latest: 1.8.0
curl -s https://api.nuget.org/v3-flatcontainer/ical.net/index.json  # latest: 5.2.3 (not used — see Alternatives)
```
`[VERIFIED: api.nuget.org/v3-flatcontainer/qrcoder/index.json and .../ical.net/index.json — queried directly this session]` for version-list existence and latest version number. Download counts, ownership, and GitHub links are `[CITED: nuget.org/packages/QRCoder, nuget.org/packages/Ical.Net]` (WebFetch of the package pages), not independently cross-checked against a second source, so treat those specific figures as approximate.

## Package Legitimacy Audit

The seam's `package-legitimacy check` command only supports `--ecosystem npm|pypi|crates` and rejected `nuget` as an ecosystem this session — there is no automated SLOP/SUS/OK verdict available for NuGet packages in this toolchain. The table below is a manual assessment using the same signals (registry existence, age, download count, source repo) gathered via direct NuGet API queries and `WebFetch` of the package pages.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| QRCoder | nuget.org, confirmed via `api.nuget.org/v3-flatcontainer` | First published ~2013 (`[CITED: nuget.org/packages/QRCoder]`) | ~80M total (`[CITED]`) | github.com/Shane32/QRCoder — active, ownership transferred from original author Raffael Herrmann to maintainer Shane32 in 2025 with continued releases (`[CITED]`) | OK (manual assessment — automated `package-legitimacy` tool does not support NuGet) | Approved |

**Packages removed due to a SLOP-equivalent verdict:** none.
**Packages flagged as suspicious:** none by the manual signals above, but note the ownership transfer to a new maintainer in 2025 — worth a `checkpoint:human-verify` before first install anyway, since the automated legitimacy gate could not run for this ecosystem. Ical.Net was considered and is *not* being added (see Alternatives Considered) — this is a build-shape decision, not a legitimacy rejection; Ical.Net's own signals (36.3M downloads, `github.com/ical-org/ical.net`, active since 2007) would also clear a legitimacy check.

*QRCoder and Ical.Net were both discovered via WebSearch/training knowledge, not an authoritative source read directly in this session, so both packages' identities are tagged `[ASSUMED]` for provenance even though registry existence was independently confirmed. The planner must add a `checkpoint:human-verify` task before `dotnet add package QRCoder` runs.*

## Architecture Patterns

### System Architecture Diagram

```
Phone calendar app (Apple/Google/Outlook)
        │
        │  GET https://<AppUrl>/calendar/{token}.ics   (webcal:// or https://, polled on the client's own schedule)
        ▼
[New anonymous controller]  ── outside GroupSessionMiddleware's active-group gate (anonymous
        │                       requests already pass through untouched; see Pattern 3)
        │
        ├─ 1. Token lookup: CalendarSubscriptionRepository.GetByTokenAsync(token)
        │      → not found            → 404 Not Found
        │      → found, RevokedAt set → 410 Gone
        │      → found, live          → continue, throttled last-fetched write (D-07)
        │
        ├─ 2. Resolve viewer's UserId + fresh membership list
        │      IGroupService.GetGroupsForUserAsync(subscription.UserId)  → memberGroupIds, board names
        │      (NOT IActiveGroupContext — there is no active group on this request)
        │
        ├─ 3. New repository method, EventSignups-rooted (D-17):
        │      DbContext.EventSignups
        │        .IgnoreQueryFilters()
        │        .Where(es => es.UserId == subscription.UserId
        │                   && memberGroupIds.Contains(es.Event.GroupId)
        │                   && es.Event.CancelledAt == null
        │                   && es.Event.Date within rolling window (D-13))
        │        .Include(es => es.Event)          // no .Include(Signups).ThenInclude(User) — no roster (D-13)
        │
        ├─ 4. Second-layer re-check: drop + LogError any row whose Event.GroupId
        │      is not in memberGroupIds (mirrors EventService.GetCrossBoardAgendaAsync)
        │
        ├─ 5. ICS writer: one VEVENT per row
        │      (UID, DTSTART/DTEND or DTSTART;VALUE=DATE, SUMMARY "[Board] Title (maybe|declined)?",
        │       TRANSP:TRANSPARENT — no DESCRIPTION, no URL, no VALARM)
        │
        └─ 6. Response: Content-Type: text/calendar; charset=utf-8, body = CRLF-joined, folded ICS text
                (optionally X-WR-CALNAME, X-PUBLISHED-TTL/REFRESH-INTERVAL, ETag/Last-Modified — see Pitfalls)

Profile page (authenticated, browser)
        │
        ├─ GET  /Account/Profile  → existing action, extended with subscription list section (D-15)
        ├─ POST /Account/AddCalendarSubscription     → mints token (D-16: nothing minted until Add)
        ├─ POST /Account/RenameCalendarSubscription
        └─ POST /Account/RevokeCalendarSubscription  → sets RevokedAt (tombstone, D-08)
                (all under /Account, already exempt from GroupSessionMiddleware's active-group gate)
```

### Recommended Project Structure
```
QuestBoard.Repository/
├── Entities/CalendarSubscriptionEntity.cs        # Id, UserId, Name, Token, CreatedAt, LastFetchedAt, RevokedAt
├── CalendarSubscriptionRepository.cs             # GetByTokenAsync, GetForUserAsync, touch-last-fetched (throttled)
└── Migrations/<timestamp>_AddCalendarSubscriptions.cs

QuestBoard.Domain/
├── Models/CalendarSubscription.cs
├── Models/CalendarFeedOptions.cs                 # window-back/window-ahead months, mirrors AgendaOptions
├── Interfaces/ICalendarSubscriptionRepository.cs / ICalendarSubscriptionService.cs
├── Interfaces/ICalendarFeedWriter.cs (or similar) # pure ICS text formatting, no I/O — unit-testable in isolation
└── Services/CalendarSubscriptionService.cs        # mint/rename/revoke + the D-17 tenant-safe read + second-layer re-check

QuestBoard.Service/
├── Controllers/CalendarFeedController.cs          # [AllowAnonymous], the .ics GET, [Route] attribute
├── Controllers/Admin/AccountController.cs         # extended: Add/Rename/Revoke POST actions
├── Views/Account/Profile.cshtml / .Mobile.cshtml   # extended: subscription table + QR + copy + webcal link
```

### Pattern 1: Tenant-safe cross-board read rooted at a non-Event entity (D-17)

**What:** A query that starts from `EventSignups` (not `Events`), bypasses the ambient tenant filter with `IgnoreQueryFilters()`, and re-pins scope with an explicit `memberGroupIds.Contains(...)` predicate supplied from a membership read taken in the same request.
**When to use:** Any read that must cross tenant (board) boundaries deliberately and safely — this is the third instance of the pattern in this codebase (after `EventSeriesGenerationJob`'s per-group `SetGroupId()` iteration and Phase 82's `GetUpcomingAcrossGroupsWithSignupsAsync`).
**Example (grounded in the actual repository file read this session):**
```csharp
// QuestBoard.Repository/EventRepository.cs:160-190 — GetUpcomingAcrossGroupsWithSignupsAsync
// is the existing precedent for THIS SHAPE OF SAFETY, not for this phase's query root.
// Phase 84 needs a NEW method rooted at EventSignups, e.g. on IEventSignupRepository:
public async Task<IList<EventWithSignup>> GetSubscriptionFeedRowsAsync(
    int userId, IReadOnlyCollection<int> memberGroupIds,
    DateOnly windowStart, DateOnly windowEnd, CancellationToken token = default)
{
    // Mirrors EventRepository.cs:172's comment: this bypass is strictly narrower than the
    // ambient filter for any single board, never broader, because memberGroupIds is supplied
    // from a fresh per-request membership read.
    var entities = await DbContext.EventSignups
        .IgnoreQueryFilters()
        .Where(es => es.UserId == userId
            && memberGroupIds.Contains(es.Event.GroupId)
            && es.Event.CancelledAt == null
            && es.Event.Date >= windowStart && es.Event.Date <= windowEnd)
        .Include(es => es.Event)
        .AsNoTracking()
        .ToListAsync(token);
    // ... map, return
}
```
`[VERIFIED: QuestBoard.Repository/EventRepository.cs:160-190]` for the existing precedent's shape; the new method above is this session's synthesis from that precedent plus D-17/D-13, not itself verified against a file (no such method exists yet).

### Pattern 2: Second-layer re-check with fail-loud logging

**What:** After the tenant-scoped query returns, re-filter the results in memory against the same `memberGroupIds` set and `LogError` if any row would have been dropped.
**When to use:** Every cross-tenant read in this codebase; mandatory here because — per CONTEXT.md's own framing — "a feed is read by a machine, so a leak has no reader to notice it."
**Example:**
```csharp
// QuestBoard.Domain/Services/EventService.cs:82-126 — GetCrossBoardAgendaAsync
// This exact pattern (fetch, re-check, LogError on mismatch, THEN trim/window) is the model.
var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();
if (checkedRows.Count != fetched.Count)
{
    logger.LogError(
        "Calendar feed dropped {DroppedCount} of {FetchedCount} row(s) falling outside the " +
        "subscription owner's board set. The query is built from the same set, so this " +
        "indicates a lost or mistranslated board predicate.",
        fetched.Count - checkedRows.Count, fetched.Count);
}
```
`[VERIFIED: QuestBoard.Domain/Services/EventService.cs:82-126]` — quoted logic reproduced from the file read this session; adapt the log message text for the new call site.

### Pattern 3: Anonymous-route placement relative to `GroupSessionMiddleware`

**What:** `GroupSessionMiddleware.InvokeAsync` (`QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:99-105`) checks `context.User.Identity?.IsAuthenticated != true` **first**, before consulting `ExemptPathPrefixes`. An anonymous request — which is what every real calendar client sends — passes through untouched regardless of the controller it targets.
**When to use:** This means the feed-serving action needs **no exemption entry** to work for its actual audience (calendar clients). The open design question is what an authenticated browser session sees hitting the same URL with no active board: `ExemptPathPrefixes` (`GroupSessionMiddleware.cs:59-84`) is a hardcoded list of controller-name-derived prefixes (`/GroupPicker`, `/Account`, `/platform`, `/Error`, `/Agenda`) plus the literal `/groups/pick`. A new controller not named `Account` and not added to this list will redirect an authenticated no-active-board GET to `/groups/pick?returnUrl=...`.
**Recommendation:** Place the feed-serving action on its own small controller (e.g. `CalendarFeedController`) rather than folding it into `AccountController`, since it is conceptually and security-wise distinct (anonymous, token-authenticated, machine-consumed) from every other `/Account` action (session-authenticated, human-consumed). Then make a **deliberate choice, recorded at plan time**, about whether to add it to `ExemptPathPrefixes`:
- **Add it:** an authenticated user who clicks their own subscription link in a browser gets the raw ICS text/download instead of a confusing redirect to the board picker.
- **Don't add it:** the redirect is arguably correct anyway, since a browser is not the tool this URL is meant for, and every real subscriber will be anonymous regardless.
Either is defensible; CONTEXT.md left it explicitly open for research and this document surfaces the concrete mechanism rather than picking for the planner. `[VERIFIED: QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:59-105]`, quoted logic: `if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }` (line 101-105) runs before the `ExemptPathPrefixes.Any(...)` check (line 107).

### Anti-Patterns to Avoid
- **Reaching for `IActiveGroupContext` anywhere in this feed's read path.** It will be `null` on every request to this endpoint (no session, no cookie), and every entity filtered through it (`EventEntity`, `EventSignupEntity`, and transitively anything scoped through `.Event.GroupId`) returns **zero rows** rather than throwing — this fails silently as "no events," not loudly as an error. `[VERIFIED: QuestBoard.Repository/Entities/QuestBoardContext.cs:530-547]`, quoted: `.HasQueryFilter(e => activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId)` (EventEntity, line 531-533) and `.HasQueryFilter(es => activeGroupContext.ActiveGroupId != null && es.Event.GroupId == activeGroupContext.ActiveGroupId)` (EventSignupEntity, line 545-547).
- **Widening `GetUpcomingAcrossGroupsWithSignupsAsync` or `GetUpcomingWithSignupsAsync` to cover this case.** Both are rooted at `Events`; D-17 requires rooting at `EventSignups`. `[VERIFIED: QuestBoard.Repository/EventRepository.cs:132-190]`.
- **Assuming `EventSignupEntity` is "unused" per its own doc comment.** The entity's file comment (`This table carries no GroupId of its own... No code reads or writes it yet`) is now stale — Phases 75 and 82 both read and write it. `[VERIFIED: QuestBoard.Repository/Entities/EventSignupEntity.cs:6-7]`, quoted: `// This table carries no GroupId of its own and is tenant-scoped through its required // Event navigation. No code reads or writes it yet.` — this is dead documentation, not a code risk, but do not let it mislead a reviewer into thinking the table is greenfield.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cryptographically random bearer token | A `Guid.NewGuid()`-based token or a custom PRNG | `System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)` + `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode` | `Guid` is not a cryptographic primitive and its entropy/format leaks implementation details; the recommended pair is already imported and used in this exact codebase for the password-reset token (`AccountController.cs`, `WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken))`) — same pattern, same trust level needed here since D-06 makes this string the entire authentication mechanism |
| QR code rendering | A hand-rolled QR matrix encoder | QRCoder's `SvgQRCode` | QR encoding (Reed-Solomon error correction, mode selection, mask patterns) is a well-defined but easy-to-get-subtly-wrong spec; this is exactly the "format is the risk" case the project already takes dependencies for (Markdig, HtmlSanitizer, AngleSharp) |

**Key insight:** The ICS *writer* is the one place in this phase where hand-rolling is the *recommended* path rather than the pitfall, precisely because D-01/D-10/D-11/D-13 collectively shrank the output surface to five fields with no timezone, no recurrence expansion, and no free text needing HTML-escaping-grade care — see Alternatives Considered for the reasoning and the explicit flag that this is a build decision the operator should get final say on if a review disagrees.

## Common Pitfalls

### Pitfall 1: Believing `X-WR-CALNAME` renames the subscription everywhere
**What goes wrong:** UI copy or a plan assumes setting `X-WR-CALNAME` guarantees the subscription shows a friendly name on every client.
**Why it happens:** `X-WR-CALNAME` is a non-standard `X-` property (RFC 7986's `NAME` is the standardized successor, with *weaker* client support). Behaviour is inconsistent: Outlook is reported to use it to create/name the calendar; Google Calendar's own exports include it; but Apple Calendar's subscribe dialog reportedly shows the same UI whether or not it is present — i.e. it does **not** reliably rename the subscription on Apple platforms `[CITED: search results synthesizing microformats/community sources — no vendor documentation found; confidence LOW]`.
**How to avoid:** Emit `X-WR-CALNAME:[Board1, Board2, ...]` or a fixed value like `X-WR-CALNAME:D&D Quest Board` anyway — it costs one line, has no downside, and helps at least Outlook/Google — but do **not** write UI copy promising "your phone will show this name," since Apple is the one platform where it is unreliable and Apple Calendar/iOS is an explicitly named target client.
**Warning signs:** A UAT script that asserts the calendar *name* on an iPhone rather than merely asserting the feed parses and the events appear.

### Pitfall 2: Promising a refresh latency the clients will not deliver
**What goes wrong:** UI copy says "changes appear within an hour" or similar.
**Why it happens:** `X-PUBLISHED-TTL` (non-standard, Microsoft-originated) and `REFRESH-INTERVAL;VALUE=DURATION` (RFC 7986, standardized 2016, reportedly used by Google and Apple) are both advisory. Multiple independent sources report that **current Google Calendar ignores both** and polls on its own internal schedule — commonly cited as roughly 8–24 hours with no user-facing manual refresh button and no configurable interval `[CITED: multiple WebSearch sources — calendarbridge.com, usecarly.com, calfeed.ai, twocal.app — none of which are Google's own documentation; confidence LOW, treat as directional only]`. Apple Calendar is reported to allow a **per-subscription** refresh interval the *user* sets locally (as low as 5–15 minutes), independent of anything the server publishes `[CITED, same caveat]`.
**How to avoid:** Emit both `X-PUBLISHED-TTL` and `REFRESH-INTERVAL;VALUE=DURATION` (cheap, no downside — pick a value like `PT1H` or `PT4H`), but write UI copy that says something like "your calendar app decides how often to check — this can take anywhere from minutes to about a day," never a specific number. Supporting conditional GET (`ETag`/`If-None-Match`, `Last-Modified`/`If-Modified-Since`) is worth doing regardless of whether it speeds anything up, since Apple Calendar is reported to send `If-None-Match` on repeat polls and a `304 Not Modified` response is strictly cheaper than regenerating and re-transferring the full ICS body every time `[CITED, LOW confidence for Google/Outlook, MEDIUM-plausible for Apple]`.
**Warning signs:** A support request "I edited an event and my phone still shows the old time" — this is expected client behaviour, not a bug in the feed, and should be documented as such rather than chased.

### Pitfall 3: Treating `VALARM` omission as neutral when it isn't guaranteed to be
**What goes wrong:** D-04 already decided "no VALARM" — the risk here is not the decision but *justifying* it as risk-free.
**Why it happens:** Apple's subscribe flow is reported to default new subscriptions to "Remove Alerts," meaning even if the feed *did* emit `VALARM`, Apple would strip it by default anyway (the user can opt back in) `[CITED, LOW confidence — no vendor doc found]`. This makes D-04 low-risk on Apple specifically, but the research found no equivalent confirmation for Google Calendar's or Outlook's default alarm handling on a subscribed feed.
**How to avoid:** Nothing to change — D-04 stands — but do not extend the "clients strip alarms anyway" reasoning to Google/Outlook in any documentation the plan produces, since it was not confirmed for those two.
**Warning signs:** N/A — this pitfall is about not over-claiming in documentation, not about code.

### Pitfall 4: `SEQUENCE` reasoning borrowed from iTIP (meeting invitations) applied to a polled subscription
**What goes wrong:** Assuming a client will "reject" an updated event because `EventEntity` has no modified timestamp to drive a `SEQUENCE` bump.
**Why it happens:** `SEQUENCE`'s documented behavior (a client only accepts a revision whose `SEQUENCE` is >= what it holds, else discards it as stale) is specified for **iTIP** — the RSVP/organizer-attendee update protocol used by meeting invitations (`METHOD:REQUEST`/`REPLY`) `[CITED: kanzaki.com/docs/ical/sequence.html and related sources]`. This feed has no `METHOD`, no `ORGANIZER`, no `ATTENDEE` — it is a plain published calendar (`VCALENDAR` with no `METHOD`, i.e. implicitly `PUBLISH`), which most clients are reported to treat by **fully replacing** their locally-cached copy of the feed's VEVENTs by `UID` on each successful refetch, rather than running iTIP-style sequence comparison `[CITED, LOW confidence — this specific distinction (published feed vs. iTIP) was not found confirmed by any single authoritative source in this session; it is the least certain claim in this document]`.
**How to avoid:** Since `EventEntity` has no modified timestamp at all `[VERIFIED: QuestBoard.Repository/Entities/EventEntity.cs:7-59 — full file read; no `UpdatedAt`/`ModifiedAt` field exists]`, emitting a constant `SEQUENCE:0` on every VEVENT is the only option without a schema change, and per the "full replace on refetch" behaviour above this is expected to be harmless for the plain-`PUBLISH` case this feed is. If a future review disagrees and wants a real `SEQUENCE`, that requires adding a modified-timestamp column to `EventEntity` — out of scope for this phase's decisions as recorded, and should be flagged rather than silently added.
**Warning signs:** If UAT specifically finds a client is stuck showing a stale time/title after several confirmed refetches, re-open this pitfall with real device evidence rather than trusting this document's LOW-confidence claim either way.

### Pitfall 5: Building the D-17 query as a variant of an existing method instead of a new one
**What goes wrong:** A plan tries to add an `EventSignups`-only overload onto `EventRepository`/`IEventRepository` by parameterizing `GetUpcomingAcrossGroupsWithSignupsAsync`.
**Why it happens:** The existing method's shape looks close — same `IgnoreQueryFilters()` + pinned-set pattern, same take-a-membership-list signature — but it is rooted at `DbContext.Events`, and D-17 requires filtering by `EventSignups.UserId` first, which changes the join direction (an event with zero signup rows for this viewer must never appear, even if it exists and is within the board and date window). `[VERIFIED: QuestBoard.Repository/EventRepository.cs:160-190]`, quoted: `var entities = await DbContext.Events.IgnoreQueryFilters().Where(e => memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null)...` — this starts from `Events`, not `EventSignups`.
**How to avoid:** Write a new method, most naturally on `IEventSignupRepository` (already exists — `EventSignupService`/`IEventSignupRepository` are registered per `QuestBoard.Domain/Extensions/ServiceExtensions.cs:47` and `QuestBoard.Repository/Extensions/ServiceExtensions.cs:32`) rather than `IEventRepository`, since the query root is `EventSignups`.
**Warning signs:** A code review comment asking "why does this look so similar to `GetUpcomingAcrossGroupsWithSignupsAsync` — can we merge them?" — the answer is no, per CONTEXT.md's explicit framing of this as "a third query shape."

## Code Examples

### All-day vs timed VEVENT (D-01, D-02, the all-day `VALUE=DATE` mapping)
```
# Timed event (StartTime is not null) — floating local time, one-hour block, TRANSPARENT:
BEGIN:VEVENT
UID:event-42@questboard.example
DTSTART:20260920T190000
DTEND:20260920T200000
SUMMARY:[The Last Bastion] Session 12
TRANSP:TRANSPARENT
SEQUENCE:0
END:VEVENT

# All-day event (StartTime is null) — VALUE=DATE, exclusive next-day DTEND:
BEGIN:VEVENT
UID:event-43@questboard.example
DTSTART;VALUE=DATE:20260921
DTEND;VALUE=DATE:20260922
SUMMARY:[The Last Bastion] Holiday - No Session
TRANSP:TRANSPARENT
SEQUENCE:0
END:VEVENT
```
Per RFC 5545, DTEND's value type must match DTSTART's, and for a `VALUE=DATE` all-day entry DTEND is the exclusive (non-inclusive) end — a one-day event on the 21st has `DTSTART;VALUE=DATE:20260921` and `DTEND;VALUE=DATE:20260922`, i.e. the day *after*. `[CITED: RFC 5545 §3.6.1 via WebSearch synthesis of datatracker.ietf.org/doc/html/rfc5545 and icalendar.org — the exclusive-end rule itself is well-established and consistently reported across sources, confidence MEDIUM]`. Getting this wrong (using the same date for both, or an inclusive end) is the single most commonly cited iCalendar bug — it renders a one-day event spanning two days on most clients.

### Line folding and CRLF (RFC 5545 §3.1, mechanical correctness)
```
Rule: content lines are CRLF-terminated ("\r\n", not "\n" alone).
Rule: a line SHOULD NOT exceed 75 octets excluding the line break.
Rule: to fold a long line, insert CRLF immediately followed by exactly one SPACE or
      HTAB at the continuation point; the reader removes any "CRLF + single
      whitespace" sequence when unfolding.
Rule: folding must happen on octet boundaries, never in the middle of a multi-byte
      UTF-8 sequence, or the reconstructed text is corrupted.
Rule (TEXT values — SUMMARY, etc., though this feed emits none of the free-text
      properties that need heavy escaping since D-10/D-11 dropped DESCRIPTION/URL):
      backslash-escape comma, semicolon, and backslash itself; encode an embedded
      newline as the two literal characters "\" + "n".
```
`[CITED: RFC 5545 §3.1 via datatracker.ietf.org/doc/html/rfc5545, confirmed by multiple independent secondary sources — confidence MEDIUM per the classify-confidence seam's cross-checked tier]`. Because D-09's `SUMMARY` is `[Board] Title` plus an optional ` (maybe)`/` (declined)` suffix, and board/event titles are free text the DM controls, the writer must escape `,`, `;`, `\`, and newlines in that one field even though `DESCRIPTION` and `URL` are gone — this is the one place in the whole writer where escaping still matters.

### Token generation matching the existing codebase idiom
```csharp
// Precedent: QuestBoard.Service/Controllers/Admin/AccountController.cs already does
// WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken)) for password-reset tokens.
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

public static string GenerateSubscriptionToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits
    return WebEncoders.Base64UrlEncode(bytes);       // URL-safe, no padding
}
```
`[VERIFIED: QuestBoard.Service/Controllers/Admin/AccountController.cs — `using Microsoft.AspNetCore.WebUtilities;` import and `WebEncoders.Base64UrlEncode(...)` call confirmed present in the file]` for the existing idiom; the `RandomNumberGenerator.GetBytes` static-method call and the 32-byte length recommendation are `[CITED: WebSearch, current .NET guidance — RNGCryptoServiceProvider is obsolete]`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `RNGCryptoServiceProvider` for random bytes | `RandomNumberGenerator.GetBytes` (static) | .NET 6 (obsoleted the instance-based API) | This codebase already targets net10.0, so use the current static API directly — no migration concern |
| `X-PUBLISHED-TTL` as the way to hint refresh cadence | `REFRESH-INTERVAL;VALUE=DURATION` (RFC 7986) | Standardized 2016 | Emit both for maximum (still-advisory) compatibility — neither is authoritative per Pitfall 2 |

**Deprecated/outdated:** None specific to this phase's dependency choices — QRCoder and the BCL crypto APIs are both current.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | QRCoder is the right QR package name/identity and its `SvgQRCode` renderer needs no native dependency | Standard Stack, Don't Hand-Roll | If the package name is subtly wrong (typosquat) or the SVG renderer secretly pulls in `System.Drawing` transitively, the container build breaks or a supply-chain risk is introduced — mitigated by the `checkpoint:human-verify` flagged in the Package Legitimacy Audit |
| A2 | Apple Calendar does not reliably honour `X-WR-CALNAME` while Outlook and Google's own exports do | Common Pitfalls, Pitfall 1 | If wrong, UI copy under-promises a feature that actually works fine on Apple, which is harmless — the risk is asymmetric and low |
| A3 | Google Calendar ignores `X-PUBLISHED-TTL`/`REFRESH-INTERVAL` and polls on an internal ~8–24h schedule with no manual refresh | Common Pitfalls, Pitfall 2 | If Google's actual behaviour is meaningfully different (e.g. much faster, or configurable), UI copy is unnecessarily pessimistic — low harm, but worth a real-device check during UAT since this is the single most user-visible claim in the phase |
| A4 | Apple Calendar defaults subscribed-feed alarms to stripped/off | Common Pitfalls, Pitfall 3 | If wrong, D-04's "no VALARM" decision is unaffected (it was chosen independently of this claim) — the only risk is an inaccurate justification in documentation |
| A5 | A plain-`PUBLISH`-method feed is fully replaced by UID on each client refetch, so a constant `SEQUENCE:0` is harmless absent a modified-timestamp column | Common Pitfalls, Pitfall 4 | If wrong and some client actually does apply iTIP-style sequence gating to a subscribed feed, an edited event's new time/title could be silently ignored by that client indefinitely — this is the highest-impact wrong-assumption in the document and should be the first thing re-tested against a real device during UAT if any report of "stale event" surfaces |
| A6 | `webcal://` support is inconsistent on Android/desktop and a plain `https://` fallback is standard practice | Standard Stack (implicit in D-14), Code Examples | Low risk — the plan already includes copy-button and QR fallbacks per D-14, so a webcal:// failure on any one platform has two other paths to the same URL |

## Open Questions

1. **Should the feed route be added to `GroupSessionMiddleware`'s `ExemptPathPrefixes`?**
   - What we know: anonymous requests (the real audience) already pass through untouched regardless. Authenticated requests to a non-exempt path with no active board are redirected to `/groups/pick`.
   - What's unclear: whether an authenticated user opening their own subscription link in a browser should see the raw ICS (requires the exemption) or the board-picker redirect (requires nothing — it's the default).
   - Recommendation: default to **not exempting** it (simplest, matches "this is not a browsing surface") and record the decision explicitly in the plan rather than leaving it to accident; revisit only if UAT surfaces confusion.

2. **Exact rolling-window bounds for D-13.**
   - What we know: CONTEXT.md says "a few months of history, roughly a year ahead" and explicitly leaves the exact numbers to the planner, configurable like `AgendaOptions`.
   - What's unclear: the precise month counts.
   - Recommendation: mirror `AgendaOptions`' shape (`QuestBoard.Domain/Models/AgendaOptions.cs`) with a new `CalendarFeedOptions` class, e.g. `MonthsBack = 3`, `MonthsAhead = 12`, both overridable via configuration — this is a product-taste call, not a technical one, and belongs in the plan/discuss layer if the operator wants to weigh in.

3. **Whether `SEQUENCE` matters enough to warrant a schema change.**
   - What we know: `EventEntity` has no modified timestamp today `[VERIFIED: EventEntity.cs full file]`.
   - What's unclear: whether real-world client behaviour (Pitfall 4, A5) makes this a non-issue or a real one.
   - Recommendation: ship with constant `SEQUENCE:0` and flag this as a candidate follow-up if UAT or real usage surfaces a stale-event report — do not add a column speculatively.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK/runtime | Whole phase | ✓ `[VERIFIED: QuestBoard.Service.csproj TargetFramework net10.0; Dockerfile FROM mcr.microsoft.com/dotnet/aspnet:10.0 and dotnet/sdk:10.0]` | net10.0 | — |
| QRCoder NuGet package | D-14 QR code | ✗ (not yet installed) | 1.8.0 latest, confirmed via NuGet registry API this session | None needed — install is the task |
| System.Drawing / libgdiplus in container | QR rendering (if the wrong renderer is chosen) | ✗ (Dockerfile has no apt-get layer for it) | — | Use `SvgQRCode` (no native dep) instead of `QRCode`/`ArtQRCode` renderers — this is the reason SVG is recommended, not a gap to fill |
| A reverse-proxy-correct absolute base URL | Building the feed's own `https://.../calendar/{token}.ics` link on Profile, and any `webcal://` variant | ✓ via `EmailSettings:AppUrl` (per CONTEXT.md's Claude's Discretion note, following Phase 78 D-07) | — already configured, used by existing email links | Phase 78's forwarded-header fix is unshipped; do not depend on request-derived scheme/host — `AppUrl` is the only proven-correct source today |

**Missing dependencies with no fallback:** none — QRCoder is a one-line install, not a blocking environment gap.
**Missing dependencies with fallback:** none beyond the QR renderer choice noted above, which is not really a "missing dependency" so much as a "pick the right one" decision.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2) + FluentAssertions 8.10.0, `[VERIFIED: QuestBoard.UnitTests.csproj and QuestBoard.IntegrationTests.csproj PackageReference entries]` |
| Config file | none dedicated — standard `dotnet test` per-project convention already in use across the solution |
| Quick run command | `dotnet test QuestBoard.UnitTests` (ICS writer is pure-function, belongs here) |
| Full suite command | `dotnet test` (solution-wide, includes `QuestBoard.IntegrationTests` for the tenant-isolation and HTTP-status-code cases) |

### Phase Requirements → Test Map
No REQ-IDs exist yet (see `<phase_requirements>` above); the table below maps CONTEXT.md decisions to the test shape they need, for the planner to convert into REQ-IDs and concrete test names.

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|---------------------|-------------|
| D-01/D-02/D-03/D-13 | ICS writer emits exact bytes for a timed event, an all-day event, folds/escapes correctly | unit (golden-file / exact-string assertion) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ Wave 0 |
| D-17 | Feed excludes a one-shot board event the viewer has no signup row on; includes a campaign auto-row | integration, two-group | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | ❌ Wave 0 |
| D-08 | Revoked token → 410; unknown token → 404; live token → 200 with correct Content-Type | integration | same file as above, additional theories | ❌ Wave 0 |
| Tenant isolation (second-layer re-check) | A cross-board leak, if the predicate is ever dropped, is caught and logged, never rendered | integration, two-group, mirrors `EventService.GetCrossBoardAgendaAsync`'s test precedent | same file | ❌ Wave 0 |
| D-12 | A cancelled event with a live signup row does not appear in the feed | integration | same file | ❌ Wave 0 |
| D-07 | Rename/revoke actions work; last-fetched timestamp write is throttled | integration + unit (throttle logic) | same file + a small throttle unit test | ❌ Wave 0 |
| D-15/D-16 | Profile page renders the subscription section on both layouts; nothing minted until Add is pressed | integration (markup assertion under both desktop and real mobile User-Agent, per this codebase's established convention) | `dotnet test QuestBoard.IntegrationTests --filter ProfileCalendarSubscriptionTests` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests` (fast; the ICS writer is pure and should dominate the unit suite for this phase)
- **Per wave merge:** `dotnet test` (full solution, catches the integration/tenant-isolation suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`, plus a real-device or `curl`-based manual check against at least one real calendar client (Apple/Google/Outlook) — markup/unit tests alone cannot prove a real phone renders the feed correctly, matching the lesson Phase 78's research draws from Discord-card verification.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/CalendarFeedWriterTests.cs` — exact-byte assertions for timed/all-day/escaped-title VEVENT output, folding at 75 octets, CRLF line endings
- [ ] `QuestBoard.IntegrationTests/CalendarSubscriptionFeedTests.cs` — two-group tenant isolation (mirroring `LayoutNavigationTests`/`EventRepository` two-group precedent already in the codebase), D-08 status codes, D-12 cancellation exclusion, D-17 signup-row gating
- [ ] `QuestBoard.IntegrationTests/ProfileCalendarSubscriptionTests.cs` — add/rename/revoke round trip, both-layout markup rendering under a real mobile User-Agent (matching this codebase's established `MobileDetectionMiddleware` test convention)
- [ ] No framework install needed — xUnit v3/FluentAssertions are already solution-wide dependencies

*(A conformance validator such as `icalendar.dev/validator/` `[CITED: found via WebSearch, not automatable in this session — appears to be a browser-based/manual tool, not a CI-callable API]` can supplement the golden-file unit tests as a manual pre-ship sanity check, but should not be treated as a CI gate unless it exposes a scriptable interface, which was not confirmed this session.)*

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | Partial — the token itself *is* the authentication mechanism for this endpoint (bearer-in-URL), not a login | `RandomNumberGenerator`-sourced token, 256 bits, never logged in full (D-06's own accepted-cost note already covers the "visible in DB" tradeoff; logging is a separate, avoidable exposure) |
| V3 Session Management | No | This endpoint is deliberately stateless/anonymous; no session applies |
| V4 Access Control | Yes | The tenant-isolation query pattern (Pattern 1/2 above) is the access-control mechanism; `410`/`404` distinction (D-08) is a deliberate, accepted enumeration-timing tradeoff already reasoned through in CONTEXT.md |
| V5 Input Validation | Yes | The `{token}` route parameter must be validated as a well-formed base64url string before hitting the database (defense in depth, not a security requirement per se — an ill-formed token simply won't match any row) |
| V6 Cryptography | Yes | `RandomNumberGenerator` (BCL, never hand-rolled) for token generation — see Don't Hand-Roll |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Token leaked via URL (browser history, referrer headers, shared screenshots, server access logs) | Information Disclosure | Already accepted as a known limitation in D-06/D-08's "accepted cost" notes; the mitigation this phase *does* own is: never log the full token value in application logs (log a truncated/hashed form only), and ensure the throttled last-fetched write (D-07) does not accidentally write the raw token into a log line as a side effect of its implementation |
| Cross-tenant data leak via a dropped or mistranslated `IgnoreQueryFilters()` predicate | Elevation of Privilege / Information Disclosure | Pattern 1 + Pattern 2 above — pinned predicate + second-layer re-check + `LogError`, exactly matching the two prior real incidents this codebase has had (Phases 49/55, referenced repeatedly in ROADMAP.md) |
| Enumeration via the `410`/`404` status distinction | Information Disclosure (minor, already accepted) | D-08 already accepts this cost explicitly; no further mitigation needed since the token space (256 bits) makes brute-force enumeration infeasible regardless of which status code confirms a hit |
| Rate-limit-exempt hammering of the anonymous endpoint | Denial of Service | Apply an `EnableRateLimiting` policy per the `forgot-password`/`set-password` precedent (`Program.cs:120-136`), partitioned by client IP (anonymous, no user id available) or by the token itself (better — ties the budget to the specific leaked-or-not subscription rather than a shared IP pool a NAT/proxy might collapse many legitimate calendar clients into) — `[VERIFIED: QuestBoard.Service/Program.cs:118-136]` for the existing policy shape to pattern from |
| A write-storm on the last-fetched timestamp (D-07) | Denial of Service (of the database) | Throttle the write — e.g. only update `LastFetchedAt` if the new value differs from the stored one by more than some interval (5–15 minutes), avoiding a write on every single poll |

## Sources

### Primary (HIGH confidence — read directly from the repository this session)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` (lines 280-559) — all query filter definitions, including `EventEntity`/`EventSignupEntity`
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (full file) — exemption list and anonymous-passthrough order
- `QuestBoard.Repository/EventRepository.cs` (full file) — existing cross-board read precedent and its exact line numbers
- `QuestBoard.Domain/Services/EventService.cs` (full file) — second-layer re-check pattern
- `QuestBoard.Repository/Entities/EventEntity.cs`, `EventSignupEntity.cs` (full files) — confirms no modified timestamp, confirms `HasAnswered`/`Availability` shapes
- `QuestBoard.Domain/Models/EventSignup.cs`, `AgendaOptions.cs`, `EmailSettings.cs`, `GroupWithMemberCount.cs` (full files)
- `QuestBoard.Service/Program.cs` (rate limiter and pipeline-order sections) — `forgot-password`/`set-password` policy pattern, `UseForwardedHeaders`/`UseSession`/`UseAuthentication`/`UseMiddleware<GroupSessionMiddleware>`/`UseRateLimiter`/`UseAuthorization` ordering
- `QuestBoard.Service/Controllers/AgendaController.cs`, `Admin/AccountController.cs` (partial) — membership-read pattern (`GetGroupsForUserAsync`), token-generation idiom (`WebEncoders.Base64UrlEncode`)
- `QuestBoard.Service/Views/Account/Profile.cshtml`, `Profile.Mobile.cshtml` (full files) — exact current markup both layouts must extend
- `api.nuget.org/v3-flatcontainer/qrcoder/index.json`, `.../ical.net/index.json` — direct registry queries confirming latest versions

### Secondary (MEDIUM confidence — WebSearch cross-checked against RFC text or multiple independent sources)
- RFC 5545 §3.1 line-folding/CRLF/escaping mechanics (datatracker.ietf.org, corroborated by icalendar.org and multiple dev blogs)
- RFC 5545 §3.6.1 all-day `VALUE=DATE`/exclusive `DTEND` semantics

### Tertiary (LOW confidence — single-provider WebSearch, no vendor documentation found, marked for validation)
- `X-WR-CALNAME` behaviour differences across Apple/Google/Outlook
- `X-PUBLISHED-TTL`/`REFRESH-INTERVAL` being ignored by Google Calendar, and Google's ~8-24h polling cadence
- Apple Calendar's ETag/If-None-Match conditional-request support
- Apple Calendar's default "Remove Alerts" behaviour on subscribe
- `SEQUENCE`'s irrelevance to a plain-PUBLISH feed's full-replace-by-UID refresh behaviour (the single least-certain claim in this document — see Assumption A5)
- `webcal://` scheme support variability on Android/desktop
- QRCoder/Ical.Net download counts and ownership-transfer details (WebFetch of nuget.org package pages, not independently cross-checked)

## Metadata

**Confidence breakdown:**
- Standard stack: MEDIUM — QRCoder's technical fitness (SVG, no native deps) is well-corroborated; its exact download/ownership figures are single-source
- Architecture: HIGH — every query-filter, middleware, and pipeline-order claim was read directly from the file this session with line numbers and verbatim quotes
- Pitfalls: LOW-to-MEDIUM — the RFC-mechanics pitfalls (folding, all-day dates) are MEDIUM/CITED against the RFC text itself; every client-behaviour pitfall (refresh cadence, VALARM, X-WR-CALNAME, SEQUENCE) is LOW because no vendor publishes this and only third-party observation was found

**Research date:** 2026-09-17
**Valid until:** 30 days for the codebase-grounded findings (stable unless the code changes); the client-behaviour findings should be treated as indefinitely provisional — Apple/Google/Microsoft can and do change subscription-polling behaviour without notice, so re-verify against a real device at UAT time regardless of this document's age.
