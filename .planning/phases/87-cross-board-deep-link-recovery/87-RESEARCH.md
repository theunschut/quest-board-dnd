# Phase 87: Cross-Board Deep Link Recovery - Research

**Researched:** 2026-09-21
**Domain:** ASP.NET Core middleware pipeline, EF Core global query filters, tenant-isolation bypass design
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Phase Boundary.** A member who follows a link to a page on a board they belong to lands on that
page, on that board, instead of the empty 404 they get today. The board is switched automatically,
and a one-shot banner on the landed page says which board they were moved to and offers a way back.

Two request shapes, kept as separate code paths:
1. **Wrong board active.** A new middleware, sitting directly after `GroupSessionMiddleware`,
   resolves the URL's target board and repoints the session before the action runs.
2. **No board active** (fresh login, expired session). `GroupPickerController.Index` already
   receives the `returnUrl`; when that URL resolves to exactly one board the viewer belongs to, it
   skips the picker entirely.

Both call one shared resolver and one shared active-board switcher. The resolver is the phase's only
new read across the tenancy boundary: a closed `(controller, action)` registry, membership-pinned,
returning a board id and nothing else.

**Not in this phase:** any change to what a non-member sees (byte-identical to today); writes — POST
and every other non-idempotent verb behave exactly as they do now; board-qualified routes; a rendered
404/error page; restoring `URL` to the calendar feed; board-qualifying the URLs emitted by email jobs.

**Landing behaviour**

- **D-01: Auto-switch and land on the page — no confirm step.** Deliberate revision of Phase 82 D-11
  (which rejected silent switching for the Agenda's row actions) for deep links only. The Agenda's own
  confirm-then-switch modal stays as it is.
- **D-02: Auto-switch fires only on a real top-level navigation, gated on request headers.** Require
  `Sec-Fetch-Dest: document` **and** `Sec-Fetch-Mode: navigate`, and refuse when any prefetch hint is
  present (`Sec-Purpose`, `Purpose`, `X-Moz`). A request that fails the gate falls through to exactly
  today's behaviour, the bare 404 — no confirm-interstitial fallback. **Claude's discretion** on the
  exact header set / prefetch hints honoured; may not weaken the property that a non-navigation
  request never changes the active board.
- **D-03: A one-shot banner on the landed page, carrying a switch-back control.** TempData-style:
  shown once, gone on the next navigation. No new session key.
- **D-04: Switch-back posts to `SelectGroup` with no `returnUrl`.** Correctness requirement, not a
  preference — dropping `returnUrl` makes the ping-pong loop structurally impossible.
- **D-05: When the picker is reached with a `returnUrl` that resolves, skip the picker.** If it
  resolves to exactly one board the viewer belongs to, switch and go straight to the page. Belongs in
  `GroupPickerController.Index`, generalising its existing `groups.Count == 1` auto-select branch.

**Where resolution lives**

- **D-06: A new middleware registered directly after `GroupSessionMiddleware`.** Reads route values,
  resolves the target board, repoints session, sets the banner, lets the same request continue — no
  redirect. **Placement before `UseAuthorization` is required, not incidental** (see D-18).
- **D-07: Two middlewares, one shared resolver.** Wrong-board middleware is its own class with its own
  tests; `GroupSessionMiddleware`'s null-board branch and `GroupPickerController.Index` call the same
  resolver service.
- **D-08: Extract a shared active-board switcher.** The three session writes (`ActiveGroupId`,
  `ActiveGroupName`, `ActiveGroupValidatedAtUtc`) move into one small service with **three callers**:
  `GroupPickerController.Index`'s single-group branch, `SelectGroup`, and the new middleware.

**The cross-board lookup seam**

- **D-09: A closed `(controller, action)` registry.** A route absent from the registry never resolves.
  Opt-in by construction.
- **D-10: The lookup returns a board id and nothing else.** Projected in SQL:
  `.IgnoreQueryFilters().Where(x => x.Id == id && memberGroupIds.Contains(x.GroupId)).Select(x => (int?)x.GroupId)`.
  Structurally cannot leak a title, name, or any row data.
- **D-11: Containment is an allowlist architecture test plus one dedicated repository class.** Mirror
  `AmbientClockSeamTests`: a closed list of files permitted to call `IgnoreQueryFilters()`. **7 call
  sites today**: `EventSignupRepository.cs:91`, `EventRepository.cs:172`, `GroupRepository.cs:135` and
  `:147`, `QuestRepository.cs:270` and `:293`. Every cross-board id→board lookup lives in one
  dedicated repository class.
- **D-12: No SuperAdmin branch.** The resolver pins to the viewer's own `UserGroups` memberships, read
  fresh per request. A SuperAdmin following a link to a board they are not a member of gets the same
  404 as anyone else.

**Oracle parity**

- **D-13: Parity is structural, not maintained.** A non-member asking for an entity that exists and
  any viewer asking for a nonexistent id run the identical code path: same resolver, same query shape,
  both return null, both fall through to the same `NotFound()`.
- **D-14: The bare 404 stays exactly as it is.** No `UseStatusCodePages` registered; remains unchanged.
- **D-15: One paired equivalence test, not two separate 404 assertions.** A single test issues both
  requests and asserts the responses match on status, body and headers.

**Scope — which routes participate**

- **D-16: All 18 board-scoped top-level GET routes that take an id.**
  Read (8): `Quest/Details`, `Events/Details`, `Characters/Details`, `Contacts/Details`,
  `QuestLog/Details`, `Shop/Details`, `Series/Details`, `DungeonMaster/Profile`.
  Edit/manage (10): `Quest/Edit`, `Quest/Manage`, `Quest/CreateFollowUp`, `Events/Edit`,
  `Characters/Edit`, `Contacts/Edit`, `QuestLog/EditRecap`, `ShopManagement/Edit`,
  `ContactCategoryManagement/Edit`, `DungeonMaster/EditProfile`.
  Six image subresources excluded for free (arrive as `Sec-Fetch-Dest: image`, fail D-02's gate):
  `Characters/GetProfilePicture`, `Characters/GetCroppedPicture`, `Contacts/GetContactImage`,
  `Contacts/GetCroppedContactImage`, `DungeonMaster/GetDMProfilePicture`,
  `DungeonMaster/GetOriginalDMProfilePicture`. Same for `Shop/Details`'s `isModal` AJAX variant.
- **D-17: The two `DungeonMaster` routes resolve only when the answer is unambiguous.** Switch only
  when the target user is in exactly one of the viewer's boards and it is not the active one.
  `EditProfile` takes `int? id` — a null id is self-edit and never resolves.
- **D-18: A resolved link that the page will not serve is still resolved.** The resolver answers
  "which board owns this id"; the page answers everything else (visibility, authorization). **This is
  why D-06's placement before `UseAuthorization` is mandatory** — a viewer who is a Player on Board A
  and a DM on Board B following `/Quest/Edit/42` must be judged with Board B's role.

**Writes**

- **D-19: GET and HEAD only — an explicit method check, not an inherited one.** A cross-board POST
  behaves exactly as it does today. D-02's header gate does **not** exclude POSTs on its own (a form
  post is also `Sec-Fetch-Mode: navigate` + `Sec-Fetch-Dest: document`) — excluding writes must be an
  explicit verb check.

**Emails and the calendar feed**

- **D-20: The email URLs are left exactly as they are.** The resolver handles them like any other
  link, including ones already sitting in people's mailboxes.
- **D-21: The calendar feed is out of scope** — it has no deep links at all (`URL` deliberately
  dropped, per `CalendarFeedWriter.cs:9`).

### Claude's Discretion

- **D-02's prefetch mechanism** — the header set, the exact prefetch hints honoured, and the decision
  to fall through to today's 404 rather than a confirm interstitial are Claude's call. Research and
  planning may refine *which* headers are checked; may not weaken the property that a non-navigation
  request never changes the active board.

### Deferred Ideas (OUT OF SCOPE)

- A rendered 404/error page (`UseStatusCodePagesWithReExecute` plus a generic not-found view and its
  mobile twin) — deliberately out of this phase (D-14).
- Restoring `URL` to the calendar feed (D-21) — worth a roadmap entry, not this phase.
- A board hint parameter on newly-sent email links (`?b=3`) — declined, second resolution route for a
  benefit only new mail sees.
- Board-qualified routes (`/b/{board}/quest/42`) — can only ever be an addition, never a replacement.
- Answering cross-board POSTs with a 409 plus client-side "your session moved boards, reload" handling
  (D-19) — needs UI that does not exist.
</user_constraints>

<phase_requirements>
## Phase Requirements

No REQ-IDs. This phase runs on the locked `D-01` through `D-21` decision IDs in
`87-CONTEXT.md`, the way Phase 86 did. The decisions are implementation choices about a single
recovery path rather than independently verifiable user-facing capabilities.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01 | Auto-switch, no confirm | Verified `Views/Agenda/Index.cshtml:190-237` is the confirm-then-switch pattern being *not* reused for this path — confirmed distinct code path exists there today |
| D-02 | Header-gated auto-switch | Fetch Metadata Request Headers verified against MDN + WHATWG/Chromium prefetch-header intent threads (see Sources) |
| D-03 | One-shot banner | Verified existing `_Toasts.cshtml` TempData flash-message partial, rendered from both `_Layout.cshtml:250` and `_Layout.Mobile.cshtml:211` — the mechanism to extend |
| D-04/D-05 | GroupPicker changes | Verified `GroupPickerController.cs` current `Index`/`SelectGroup`/`RedirectToLocal` implementation in full |
| D-06/D-07/D-08 | Middleware placement, shared resolver/switcher | Verified `Program.cs:317-325` pipeline order and `GroupSessionMiddleware.cs` in full |
| D-09/D-10/D-11/D-12 | Registry, projection shape, containment, no-SuperAdmin | Verified `QuestBoardContext.cs:385-572` filter set and all 7 existing `IgnoreQueryFilters()` call sites; verified `AmbientClockSeamTests.cs` as the allowlist pattern to mirror |
| D-13/D-14/D-15 | Oracle parity | Verified no `UseStatusCodePages*` registration anywhere in `QuestBoard.Service` |
| D-16 | 18 routes | Verified representative controller actions (`QuestController`, `CharactersController`, `ContactsController`, `EventsController`, `ShopManagementController`, `ContactCategoryManagementController`) all take plain `int id` and 404 the same way |
| D-17 | DM profile ambiguity | Verified `DungeonMasterController.cs` `IsTargetInActiveGroupAsync` gate and `Profile`/`EditProfile` signatures directly |
| D-18 | Resolve-then-let-the-page-decide | Verified via D-06 pipeline placement (before `UseAuthorization`) |
| D-19 | GET/HEAD only | Verified `GroupSessionMiddleware.cs` GET/HEAD-vs-409 branch as the existing verb-check precedent |
</phase_requirements>

## Summary

This phase adds exactly one new tenancy-crossing read path (a resolver) and one new middleware to an
already well-understood pipeline. Nothing in the stack changes — no new package, no new external
service. The work is almost entirely: (1) a `(controller, action)` registry mapping 18 routes to one
of a handful of id→board projections, (2) a middleware that reads route values (available via
`HttpContext.GetRouteValue` after `UseRouting()`, which already runs before `GroupSessionMiddleware`),
checks the Fetch Metadata headers, calls the resolver, and repoints session through a newly-extracted
shared switcher service, (3) a `GroupPickerController.Index` change reusing the same resolver for the
no-active-board case, and (4) a banner surfaced through the codebase's *existing* one-shot
TempData-flash-message mechanism (`_Toasts.cshtml`), which is already rendered identically from both
the desktop and mobile layouts — meaning the phase's best-known failure mode (a missed `.Mobile.cshtml`
twin) is largely defused for the banner specifically, though **not** for anything else this phase
touches (e.g. if a confirm surface were ever added, which D-01 explicitly rejects).

The riskiest part of this phase is not the happy path — it's proving the two negative properties: (a)
a non-member gets byte-identical output to a nonexistent-id request (D-13/D-15, "no membership
oracle"), and (b) a non-navigational request (image load, prefetch, prerender, prefetch-hinting
crawler) can never move a session's active board (D-02). Both are testable mechanically, not by
inspection — see Validation Architecture below.

**Primary recommendation:** Build one dedicated `CrossBoardLinkResolver` repository/service holding
the closed registry and the `IgnoreQueryFilters()` projection (D-09/D-10/D-11), one `ActiveBoardSwitcher`
service holding the three session writes (D-08), one `CrossBoardDeepLinkMiddleware` calling both
(D-06/D-07), a matching change to `GroupPickerController.Index` (D-05), and a new `TempData["BoardSwitch"]`
(or similarly named) entry rendered from the existing `_Toasts.cshtml` partial (D-03). No other files
in the read/render path need new abstractions.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Resolve "which board owns this id" | API/Backend (new repository class, EF Core `IgnoreQueryFilters()`) | — | The only tier that can see across the tenancy boundary; must stay a single audited seam per D-11 |
| Detect "is this a real top-level navigation" | API/Backend (middleware, reads `Sec-Fetch-*` request headers) | Browser (headers are browser-generated, not client script) | Server-side header check; the browser is the trust boundary that generates these headers, not JS running in it |
| Repoint the active board | API/Backend (session write via shared switcher service) | — | Session is server-side state; no client-side session mutation exists anywhere in this app |
| Render the one-shot banner | Frontend Server (SSR: Razor view + TempData) | Browser (Bootstrap toast dismiss/autohide, already wired) | TempData is an SSR concept; the toast markup and JS behaviour are already shared infrastructure |
| Membership check for the registry lookup | API/Backend (`UserGroups` table read, fresh per request) | Database | Matches the existing `AgendaController`/`DungeonMasterController` pattern — fresh read, never cached |
| Authorization on the landed page itself | API/Backend (existing `[Authorize(Policy=...)]` / role checks) | — | Untouched by this phase — D-18 deliberately keeps the resolver blind to page-level authorization |

## Standard Stack

### Core

No new libraries. This phase is implemented entirely with ASP.NET Core 10 middleware/DI primitives
and EF Core 10 already in the project.

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|---------------|
| `Microsoft.AspNetCore.Http` (framework) | in-box, .NET 10 | `IMiddleware`/`RequestDelegate`, `HttpContext.GetRouteValue`, raw header access | Already the pattern used by `GroupSessionMiddleware` and `MobileDetectionMiddleware` [VERIFIED: QuestBoard.Service/Middleware/GroupSessionMiddleware.cs] |
| `Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory` (framework) | in-box, .NET 10 | Read/write TempData from middleware (outside an MVC action context) | Needed to set the banner from the new middleware, not just from a controller |
| EF Core `IgnoreQueryFilters()` (framework) | in-box, EF Core 10 | The one escape hatch from the 18 `HasQueryFilter` predicates | Already the mechanism used by all 7 existing cross-board reads [VERIFIED: grep across QuestBoard.Repository] |

### Supporting

| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| Fetch Metadata Request Headers (`Sec-Fetch-Dest`, `Sec-Fetch-Mode`) | Web platform standard, browser-generated | D-02's gate | Every request; read as plain HTTP headers, no package needed [CITED: developer.mozilla.org/en-US/docs/Web/HTTP/Guides/Fetch_metadata] |
| `Sec-Purpose` / legacy `Purpose` / Firefox-legacy `X-Moz` headers | Prefetch-signalling headers | Refuse-on-present check in D-02's gate | Present on `<link rel=prefetch>`, Speculation Rules API, and older Firefox link prefetch [CITED: developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Sec-Purpose; github.com/WebKit/standards-positions/issues/493] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Middleware-based resolution (D-06) | Result-filter intercepting `NotFoundResult` | Rejected in CONTEXT.md — fires on every legitimate 404 (including role-check 404s per Phase 49/55), costs a round trip |
| Middleware-based resolution (D-06) | Board-qualified routes (`/b/{board}/quest/42`) | Rejected — rewrites every emitted URL and does nothing for links already in mailboxes |
| Per-action registry (D-09) | Attribute on each controller action | Rejected — scatters the escape hatch's surface across 13 controllers, no single audit point |
| New TempData key on existing `_Toasts.cshtml` (this research's recommendation for D-03) | A dedicated new banner partial | Not rejected by CONTEXT.md, but the existing partial is a strictly better fit: already TempData-driven (one-shot by construction), already rendered from both layouts, already has an established toast markup/JS pattern (see `GoldReceived` toast for a precedent carrying a bespoke payload beyond a plain string) |

**Installation:** None — no `dotnet add package` needed for this phase.

**Version verification:** Not applicable — no new packages. Confirmed via reading `Program.cs`,
`QuestBoard.Service.csproj` is unnecessary since nothing changes in the dependency graph.

## Package Legitimacy Audit

**Not applicable.** This phase introduces zero new external packages, in any ecosystem. Every
component used (ASP.NET Core middleware, EF Core `IgnoreQueryFilters()`, Fetch Metadata request
headers) is either already a project dependency or a browser-native HTTP header requiring no package.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Browser navigation (top-level document GET, e.g. clicking an email link or a bookmark)
        │
        │  headers: Sec-Fetch-Dest, Sec-Fetch-Mode, Sec-Purpose/Purpose/X-Moz (browser-set)
        ▼
UseForwardedHeaders → UseHttpsRedirection → UseStaticFiles → MobileDetectionMiddleware
        │
        ▼
UseRouting()  ── route values (controller/action/id) now available on HttpContext
        │
        ▼
UseSession() → UseAuthentication()
        │
        ▼
GroupSessionMiddleware   (existing: null-ActiveGroupId → redirect/409; membership revalidation)
        │  ActiveGroupId now resolved-or-null for this request
        ▼
╔═══════════════════════════════════════════════════════════════════════╗
║ NEW: CrossBoardDeepLinkMiddleware  (D-06/D-07)                        ║
║                                                                         ║
║  1. Anonymous / exempt path? → pass through unchanged                 ║
║  2. Not GET/HEAD? → pass through unchanged (D-19)                     ║
║  3. Fails Sec-Fetch-* navigation gate? → pass through unchanged (D-02)║
║  4. (controller, action) not in the closed registry? → pass through  ║
║  5. Registry hit → CrossBoardLinkResolver.ResolveBoardIdAsync(        ║
║       controller, action, id, viewerMembershipIds)                    ║
║       — one IgnoreQueryFilters() SQL projection, membership-pinned    ║
║  6. Resolved board == null → pass through (today's 404 downstream)   ║
║  7. Resolved board == ActiveGroupId → pass through (nothing to do)   ║
║  8. Resolved board != ActiveGroupId → ActiveBoardSwitcher.Switch(...) ║
║       (writes ActiveGroupId/ActiveGroupName/ActiveGroupValidatedAtUtc)║
║       + TempData banner set via ITempDataDictionaryFactory            ║
║       + SAME REQUEST CONTINUES (no redirect)                          ║
╚═══════════════════════════════════════════════════════════════════════╝
        │
        ▼
UseRateLimiter() → UseAuthorization()   ← D-18: role checks now see the SWITCHED board
        │
        ▼
MVC action (e.g. QuestController.Details) — QuestBoardContext's HasQueryFilter reads
ActiveGroupId LIVE from ActiveGroupContextService, which reads Session on every access
        │
        ▼
Response — the requested page, on the requested board, with TempData banner queued
        │
        ▼
_Layout.cshtml / _Layout.Mobile.cshtml → <partial name="_Toasts" />
        │
        ▼
Banner rendered once; TempData consumed → gone on next navigation


SEPARATE PATH — no active board at all (D-05):
GroupPickerController.Index(returnUrl)
        │
        ├─ returnUrl resolves via the SAME CrossBoardLinkResolver, viewer belongs to exactly
        │  one matching board → ActiveBoardSwitcher.Switch(...) → RedirectToLocal(returnUrl)
        │
        └─ returnUrl does not resolve (unmapped route / non-member / nonexistent id / multi-board
           ambiguity) → today's picker view, unchanged
```

### Recommended Project Structure

```
QuestBoard.Repository/
├── CrossBoardLinkRepository.cs        # NEW — the one dedicated repo class holding D-09's registry
│                                       #   and D-10's projection queries. Add to D-11's IgnoreQueryFilters allowlist.
QuestBoard.Domain/
├── Interfaces/
│   ├── ICrossBoardLinkResolver.cs     # NEW — thin interface the middleware and GroupPickerController share
│   └── IActiveBoardSwitcher.cs        # NEW — D-08's shared session-write service
├── Services/
│   ├── CrossBoardLinkResolverService.cs   # NEW — calls ICrossBoardLinkRepository, applies membership pin
│   └── ActiveBoardSwitcherService.cs      # NEW — the three session writes, D-08's three callers converge here
QuestBoard.Service/
├── Middleware/
│   └── CrossBoardDeepLinkMiddleware.cs    # NEW — D-06/D-07, registered in Program.cs after GroupSessionMiddleware
├── Controllers/
│   └── GroupPickerController.cs           # MODIFIED — Index() calls the resolver for D-05
├── Views/Shared/
│   └── _Toasts.cshtml                     # MODIFIED — new TempData key rendered as a toast carrying the
│                                           #   switch-back form (D-03/D-04); already shared by both layouts
QuestBoard.UnitTests/Architecture/
└── CrossBoardIgnoreQueryFiltersSeamTests.cs   # NEW — mirrors AmbientClockSeamTests.cs; the D-11 allowlist test
QuestBoard.IntegrationTests/
└── Middleware/
    └── CrossBoardDeepLinkMiddlewareTests.cs   # NEW — the D-02 header-gate and D-15 paired-parity tests
```

### Pattern 1: Route-value-driven resolver called from middleware after `UseRouting()`

**What:** Read `context.GetRouteValue("controller")`, `"action"`, `"id"` inside middleware placed
after `UseRouting()` (confirmed: `Program.cs:319` calls `UseRouting()` before `GroupSessionMiddleware`
at `:323`, and endpoint routing populates route values at `UseRouting()` time, not at the terminal
`MapControllerRoute` call).

**When to use:** Exactly this phase's middleware — needs to know controller/action/id before the MVC
action pipeline runs, without a second round trip.

**Example:**
```csharp
// Pattern verified against QuestBoard.Service/Middleware/GroupSessionMiddleware.cs's own
// route/session access style — this file follows the same InvokeAsync(HttpContext) shape.
public class CrossBoardDeepLinkMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context); return; // D-19
        }

        var controller = context.GetRouteValue("controller") as string;
        var action = context.GetRouteValue("action") as string;
        var idRaw = context.GetRouteValue("id") as string;

        if (controller == null || action == null
            || !int.TryParse(idRaw, out var id)
            || !CrossBoardLinkRegistry.TryGetLookupKind(controller, action, out var lookupKind))
        {
            await next(context); return; // D-09: not in the closed registry
        }

        if (!IsRealTopLevelNavigation(context.Request))
        {
            await next(context); return; // D-02
        }

        var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
        var resolver = context.RequestServices.GetRequiredService<ICrossBoardLinkResolver>();
        var userId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var resolvedGroupId = await resolver.ResolveBoardIdAsync(lookupKind, id, userId);

        if (resolvedGroupId is not { } targetGroupId || targetGroupId == groupContext.ActiveGroupId)
        {
            await next(context); return; // null → let today's 404 happen; same board → no-op
        }

        var switcher = context.RequestServices.GetRequiredService<IActiveBoardSwitcher>();
        var (previousGroupId, previousGroupName) = (groupContext.ActiveGroupId,
            context.Session.GetString(SessionKeys.ActiveGroupName));
        await switcher.SwitchAsync(context, targetGroupId);

        var tempDataFactory = context.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
        var tempData = tempDataFactory.GetTempData(context);
        tempData["BoardSwitchNotice"] = /* board name + previousGroupName for the switch-back form */ "…";

        await next(context); // SAME request continues — D-06, no redirect
    }

    private static bool IsRealTopLevelNavigation(HttpRequest request)
    {
        if (request.Headers["Sec-Purpose"].Count > 0) return false;
        if (request.Headers["Purpose"].Count > 0) return false;
        if (request.Headers["X-Moz"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)) return false;
        return request.Headers["Sec-Fetch-Dest"] == "document"
            && request.Headers["Sec-Fetch-Mode"] == "navigate";
    }
}
```
Source pattern: `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (session access, route
gating shape); header names per [CITED: MDN Fetch metadata guide, MDN Sec-Purpose header page].

### Pattern 2: Membership-pinned `IgnoreQueryFilters()` projection returning only an id

**What:** The one-query shape D-10 mandates. Verified as the existing shape of all 7 current call
sites — none of them return entity data, only projected scalars or id lists.

**Example (verified pattern, drawn from the four closest existing precedents):**
```csharp
// Source: QuestBoard.Repository/EventSignupRepository.cs:88-94 and
// QuestBoard.Repository/GroupRepository.cs:132-149 — the existing membership-pinned
// IgnoreQueryFilters() shape this phase's new lookup must match.
public async Task<int?> ResolveQuestBoardIdAsync(int questId, IReadOnlyCollection<int> memberGroupIds, CancellationToken token)
{
    return await DbContext.Quests
        .IgnoreQueryFilters()
        .Where(q => q.Id == questId && memberGroupIds.Contains(q.GroupId))
        .Select(q => (int?)q.GroupId)
        .FirstOrDefaultAsync(token);
}
```
This is D-10's literal projection shape, adapted per entity. `QuestLog/Details` and `QuestLog/EditRecap`
reuse this exact Quest projection — `QuestLogController` resolves through `IQuestService`, not a
separate entity [VERIFIED: QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs read in
full — no `QuestLogEntity` exists in `QuestBoardContext`]. `Series/Details` resolves against
`EventSeriesEntity.GroupId` [VERIFIED: QuestBoard.Repository/Entities/QuestBoardContext.cs:540-543,
`EventSeriesEntity.cs:46`].

### Pattern 3: The D-11 architecture-test allowlist, mirroring `AmbientClockSeamTests`

**What:** A closed, explicit file/line list enforced at the source-text level so a future stray
`IgnoreQueryFilters()` call fails the build rather than silently widening the escape hatch.

**Example:**
```csharp
// Source: QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs — the exact pattern to mirror.
// That file's GuardedRelativePaths + StripComments + line-scan shape is directly reusable: swap
// "DateTime.Today/Now/UtcNow" for "IgnoreQueryFilters()" and invert the assertion direction (this
// test asserts these ARE the only files containing it, not that they contain none of it).
public class CrossBoardIgnoreQueryFiltersSeamTests
{
    private static readonly string[] AllowedCallSites =
    [
        "QuestBoard.Repository/EventSignupRepository.cs",
        "QuestBoard.Repository/EventRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Repository/CrossBoardLinkRepository.cs", // NEW for this phase
    ];

    [Fact]
    public void IgnoreQueryFilters_OnlyAppearsInAllowedRepositoryFiles()
    {
        // Scan every .cs file under QuestBoard.Repository/, assert every "IgnoreQueryFilters()"
        // occurrence is in a file from AllowedCallSites. Fails loudly on any new call site.
    }
}
```
This directly satisfies the roadmap's top risk framing — "a mechanism rather than an intention"
[VERIFIED: 87-CONTEXT.md D-11].

### Pattern 4: Extending the existing one-shot TempData toast for the D-03 banner

**What:** `_Toasts.cshtml` already renders `Success`/`Error`/`Warning`/`Info`/`GoldReceived` TempData
keys, is rendered identically from `_Layout.cshtml:250` and `_Layout.Mobile.cshtml:211`, and TempData
is one-shot by ASP.NET Core's own design (cleared after being read once) — which is exactly D-03's
"shown once, gone on the next navigation. No new session key" requirement, for free.

**Example:**
```csharp
// Source: QuestBoard.Service/Views/Shared/_Toasts.cshtml:57-76 — GoldReceived is the existing
// precedent for a toast carrying more than a plain string (it carries an amount AND reuses
// TempData["Success"] as a caption). BoardSwitchNotice should follow the same shape: a small
// serializable payload (target board name, previous board id/name for the switch-back form),
// not a bare string, because the switch-back control needs to POST groupId.
@if (TempData["BoardSwitchTargetName"] != null)
{
    <div class="toast show" role="alert" data-bs-autohide="false">
        <div class="toast-header bg-info text-white">
            <i class="fas fa-right-left me-2"></i>
            <strong class="me-auto">Switched board</strong>
            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
        </div>
        <div class="toast-body">
            Switched to <strong>@TempData["BoardSwitchTargetName"]</strong> to open this.
            <form asp-controller="GroupPicker" asp-action="SelectGroup" method="post" class="mt-2">
                @Html.AntiForgeryToken()
                <input type="hidden" name="groupId" value="@TempData["BoardSwitchPreviousGroupId"]" />
                @* No returnUrl — D-04: dropping it makes the switch-back/auto-switch ping-pong
                   structurally impossible rather than merely guarded against. *@
                <button type="submit" class="btn btn-sm btn-outline-light">
                    Switch back to @TempData["BoardSwitchPreviousGroupName"]
                </button>
            </form>
        </div>
    </div>
}
```
Because both layouts already share this one partial, the phase's named failure mode — shipping one
layout's surface and not the other (Phases 43, 54, 72) — cannot happen for this banner specifically
without also breaking every other existing toast.

### Anti-Patterns to Avoid

- **Reading TempData via `Controller.TempData` inside a middleware.** Middleware has no
  `ControllerContext`; must resolve `ITempDataDictionaryFactory` from `context.RequestServices` and
  call `.GetTempData(context)` directly — verified as the correct DI-resolvable service (registered by
  `AddControllersWithViews()`, already called at `Program.cs:35`).
- **Capturing `ActiveGroupId` into a local variable anywhere near the filter/resolver boundary.**
  `QuestBoardContext.cs:391-393` carries an explicit `CRITICAL: Do NOT capture
  activeGroupContext.ActiveGroupId into a local var here` comment — the same rule applies to any new
  code reading `IActiveGroupContext` inside the new middleware: read it live at each check point, not
  once at the top of `InvokeAsync`, since the switcher mutates it mid-request.
- **A bare `IgnoreQueryFilters()` anywhere outside the one new repository class.** This is precisely
  how `QuestRepository.cs:270`'s comment-only "explicit cross-group intent" discipline could erode —
  D-11 exists because a comment alone did not stop that pattern from spreading to a second call site
  in the same file (`:293`). The allowlist test is the actual guardrail.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| One-shot flash message | A new session-flag-based "shown once" banner mechanism | The existing `_Toasts.cshtml` + TempData pattern | TempData is already one-shot by framework design; a session flag needs its own clear-on-every-path logic, which D-03 explicitly rejected ("no session flag to keep in sync across every join, leave and switch path") |
| Detecting a real browser navigation | User-Agent sniffing or a custom "is this a bot" heuristic | `Sec-Fetch-Dest`/`Sec-Fetch-Mode` Fetch Metadata headers | Browser-generated, not spoofable by page JavaScript (forbidden header names per the Fetch spec); User-Agent strings are the opposite — attacker- and crawler-controlled |
| Membership check for the resolver | A cached/claims-based membership list | Fresh `UserGroups` read per request | Matches the established pattern in `AgendaController.Index` and `GroupSessionMiddleware`'s periodic revalidation — the codebase already treats membership as something that must never go stale within a request's authorization decision |
| Session repointing | Reimplementing the three session writes in the new middleware | The shared `ActiveBoardSwitcher` service (D-08) | `SelectGroup` and `GroupPickerController.Index`'s single-group branch already write these three keys; a third independent write site is exactly the "second mechanism" Phase 82 D-11 exists to prevent, and a missed key (especially `ActiveGroupValidatedAtUtc`) silently forces `GroupSessionMiddleware` to re-validate on the very next request |

**Key insight:** Nearly everything this phase needs is already an established pattern somewhere in the
codebase — a membership-pinned `IgnoreQueryFilters()` read, a route-gating middleware reading session
live, a TempData-driven flash banner shared by both layouts, and an allowlist architecture test. The
work is composition and one new registry, not new mechanism design.

## Common Pitfalls

### Pitfall 1: The `Sec-Fetch-*` gate is a UX safety valve, not an authorization boundary

**What goes wrong:** Treating D-02's header check as a security control and reasoning "an attacker
can't repoint someone's session because they can't fake `Sec-Fetch-Dest: document`."

**Why it happens:** The headers are indeed unforgeable *from page JavaScript* (they're forbidden
header names per the Fetch spec), but any direct HTTP client (`curl`, a bot, a malicious extension
making its own requests, or literally any tool other than a browser's own fetch/XHR/navigation
machinery) can set arbitrary header values, including `Sec-Fetch-Dest: document`.

**How to avoid:** The actual security property this phase must preserve — a non-member sees no new
signal — comes entirely from D-10/D-12/D-13 (membership-pinned projection, no SuperAdmin escape,
structural parity), not from D-02. D-02 only prevents an *already-authenticated, already-a-member*
viewer's own browser background requests (image loads, prefetch, prerender) from silently repointing
*their own* session. Document this distinction explicitly in the plan so a reviewer doesn't mistake
the header gate for the tenancy boundary.

**Warning signs:** A plan or test that asserts "an attacker cannot switch a victim's board because
they can't set Sec-Fetch headers" — the header gate cannot be tested this way meaningfully since the
attacker doesn't control the victim's browser requests in the first place; the actual attack surface
(CSRF-style forced navigation) is already closed by same-origin/cookie scoping, not by this gate.

### Pitfall 2: `context.GetRouteValue` timing relative to `UseRouting()`/endpoint selection

**What goes wrong:** Assuming route values are only available after the endpoint has fully executed,
and either duplicating route-parsing logic from the URL manually, or placing the middleware in the
wrong pipeline position.

**Why it happens:** ASP.NET Core's endpoint routing separates *matching* (`UseRouting()`) from
*execution* (the terminal middleware / `MapControllerRoute`). Route values are populated onto
`HttpContext` at matching time, which happens once, at `UseRouting()`. Any middleware registered after
`UseRouting()` — which this phase's new middleware is, sitting after `GroupSessionMiddleware` which is
itself after `UseRouting()` — can read `context.GetRouteValue(...)` correctly.

**How to avoid:** Verified directly: `Program.cs:319` (`UseRouting()`) runs before
`GroupSessionMiddleware` (`:323`), and the new middleware is specified to sit directly after that
(D-06). No manual URL parsing needed.

**Warning signs:** A test asserting route values on a raw `DefaultHttpContext` without also setting up
routing — `MobileDetectionMiddlewareTests.cs`'s unit-test style (constructing a bare
`DefaultHttpContext`) will not populate route values; the new middleware's route-dependent behaviour
needs integration-level coverage through `WebApplicationFactory`, not a bare-context unit test.

### Pitfall 3: Forgetting `ActiveGroupValidatedAtUtc` on the new session-write path

**What goes wrong:** The new middleware (or a hand-rolled switch, if D-08's shared service is
bypassed) writes `ActiveGroupId`/`ActiveGroupName` but not `ActiveGroupValidatedAtUtc`.

**Why it happens:** It's easy to miss because the symptom is invisible on the current request — it
only surfaces on the *next* request, when `GroupSessionMiddleware`'s staleness check
(`validatedAtRaw == null` branch, `GroupSessionMiddleware.cs:131-135`) treats the missing timestamp as
"needs revalidation" and does an extra `GetGroupRoleByIdAsync` round trip. Not a correctness bug, but a
silent performance/behavior regression that D-08 exists specifically to prevent.

**How to avoid:** Route every session write for this phase through the one shared `ActiveBoardSwitcher`
service; never write the three keys inline in the new middleware or `GroupPickerController`.

**Warning signs:** A code review finding `Session.SetInt32(SessionKeys.ActiveGroupId, ...)` anywhere
outside `ActiveBoardSwitcherService`, `SelectGroup`, or `GroupPickerController.Index`'s legacy branch
(which D-08 says should itself be refactored to call the shared service).

### Pitfall 4: The switch-back loop (D-04's structural fix)

**What goes wrong:** If the switch-back control threads the current URL as `returnUrl`, clicking
"switch back" lands the viewer back on the same page, which — because it's still the deep link that
triggered the original auto-switch — immediately re-triggers D-01's auto-switch and bounces the viewer
right back to the board they just tried to leave.

**Why it happens:** The URL, not the board, is what the middleware inspects; a page belonging to Board
X is still a page belonging to Board X regardless of which board is currently active when it's
requested.

**How to avoid:** D-04 mandates the switch-back form omits `returnUrl` entirely, so `RedirectToLocal`
falls through to `RedirectToAction("Index", "Quest")` — the same place ordinary Switch Group already
sends you (`GroupPickerController.cs:69-72`, verified).

**Warning signs:** Any test or code path that pre-fills a `returnUrl` hidden field on the switch-back
form with `Request.Path`.

### Pitfall 5: `.Mobile.cshtml` twin — mostly defused for the banner, still live everywhere else

**What goes wrong:** Assuming reusing `_Toasts.cshtml` means the mobile-twin risk this phase's own
roadmap section names (Phases 43, 54, 72) is fully closed for the whole phase.

**Why it happens:** `_Toasts.cshtml` genuinely is shared by both layouts (verified), so the banner
itself needs no separate mobile work. But D-01 already rejects adding any new confirm surface, and no
other new UI is introduced by this phase's locked decisions — so the residual risk is specifically
about *future* scope creep (e.g. someone deciding partway through implementation to add a confirm step
"just for mobile", which would reintroduce exactly this risk) rather than about a component this
research identified as needing twin work.

**How to avoid:** If planning surfaces any new Razor view beyond the `_Toasts.cshtml` addition, treat
it as needing both layouts in the same task per the CLAUDE.md-adjacent project convention already
documented in `87-CONTEXT.md`'s canonical references.

**Warning signs:** A plan task that touches a `.cshtml` file with no matching `.Mobile.cshtml` change
in the same task, for any *new* view this phase introduces (not the shared `_Toasts.cshtml`).

## Code Examples

### Registering the new middleware in the pipeline

```csharp
// Source: QuestBoard.Service/Program.cs:317-325 (verified exact current pipeline)
app.UseMiddleware<MobileDetectionMiddleware>();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<GroupSessionMiddleware>();
app.UseMiddleware<CrossBoardDeepLinkMiddleware>(); // NEW — directly after GroupSessionMiddleware (D-06/D-07)
app.UseRateLimiter();
app.UseAuthorization(); // D-18: must run AFTER the switch so role checks see the resolved board
```

### The D-05 change to `GroupPickerController.Index`

```csharp
// Source: QuestBoard.Service/Controllers/GroupPickerController.cs:14-40 (verified current implementation)
// The existing groups.Count == 1 branch (:31-37) is the auto-select precedent D-05 generalises.
// New branch to add, checked before the existing groups.Count == 1 fallback:
if (!isSuperAdmin && returnUrl != null)
{
    var memberGroupIds = groups.Select(g => g.Id).ToList();
    var resolvedGroupId = await crossBoardLinkResolver.ResolveBoardIdFromUrlAsync(returnUrl, memberGroupIds, userId);
    if (resolvedGroupId is { } targetGroupId)
    {
        await activeBoardSwitcher.SwitchAsync(HttpContext, targetGroupId);
        return RedirectToLocal(returnUrl);
    }
    // Falls through to today's picker view unchanged — unmapped route, non-member's entity,
    // nonexistent id, or a route resolving to more than one of the viewer's boards.
}
```

### The D-15 paired-parity test shape

```csharp
// One test, two requests, one assertion set — not two independent 404 tests (D-15).
[Fact]
public async Task Details_NonMemberOfExistingBoard_AndNonexistentId_ProduceIdenticalResponses()
{
    // Arrange: seed a quest on Board A; authenticate a viewer who is a member of Board B only.
    var nonMemberResponse = await _client.GetAsync($"/Quest/Details/{existingQuestOnBoardAId}");
    var nonexistentResponse = await _client.GetAsync($"/Quest/Details/{int.MaxValue}");

    nonMemberResponse.StatusCode.Should().Be(nonexistentResponse.StatusCode);
    (await nonMemberResponse.Content.ReadAsStringAsync()).Should().Be(
        await nonexistentResponse.Content.ReadAsStringAsync());
    nonMemberResponse.Headers.Should().BeEquivalentTo(nonexistentResponse.Headers);
}
```

## State of the Art

Not applicable in the usual sense — no library/framework version drift is relevant here. The one
"state of the art" note worth recording: the Fetch Metadata Request Headers (`Sec-Fetch-*`) have been
shipping in Chromium and Firefox for several years and are now the standard mechanism recommended for
exactly this class of problem (distinguishing top-level navigation from subresource/prefetch/CORS
requests) — this is not a bleeding-edge or experimental technique. `Sec-Purpose` replacing the older,
non-`Sec-`-prefixed `Purpose` header is the one still-settling area: Chrome currently sends both
`Purpose: prefetch` and `Sec-Purpose: prefetch` for compatibility, and Firefox has moved from
`X-Moz: prefetch` to `Sec-Purpose` in current versions — checking all three (`Sec-Purpose`, `Purpose`,
`X-Moz`) as D-02 specifies is the currently-correct defensive superset [CITED: MDN Sec-Purpose page;
Chromium blink-dev Intent-to-Ship thread; WebKit/Mozilla standards-positions threads on the header].

**Deprecated/outdated:** None relevant to this phase's implementation choices.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ITempDataDictionaryFactory` resolved via `context.RequestServices` inside middleware behaves identically to the controller-bound `TempData` property (writes visible to the next request's Razor read through `_Toasts.cshtml`) | Anti-Patterns, Code Examples | If the underlying TempData provider (default is cookie-based since no `AddSessionStateTempDataProvider()` is registered [VERIFIED: no such call in Program.cs]) requires the response to not yet have started, and the new middleware runs late enough that headers are already sent, the banner would silently fail to appear. Low risk since the middleware runs well before the response body is written, but not exercised as a concrete runtime test in this research session — the planner should add an explicit integration test asserting the `Set-Cookie` TempData cookie (or equivalent) is present on the response that carries the switch. |
| A2 | No other existing middleware or filter already reads/short-circuits on `Sec-Fetch-*` headers elsewhere in the pipeline in a way that would conflict with the new gate | Common Pitfalls, Pattern 1 | Verified via grep — no existing reference to `Sec-Fetch` anywhere in `QuestBoard.Service` outside the phase's own planning docs. Low risk. |

**If this table is empty:** N/A — two low-risk items logged above; neither touches a locked D-NN
decision, both are implementation-detail confirmations the planner should verify with a concrete test
rather than a design change.

## Open Questions

1. **Exact TempData payload shape for the switch-back banner (previous board id/name).**
   - What we know: D-03 requires the banner to name both the target and the origin board and offer a
     switch-back control; D-04 requires the switch-back form to omit `returnUrl`.
   - What's unclear: Whether "previous board" means the board active *before* this middleware ran
     (straightforward — read `groupContext.ActiveGroupId` before calling the switcher) or something
     more nuanced for the no-active-board case (D-05), where there was no "previous board" to switch
     back to at all — only "no board selected."
   - Recommendation: For the D-05 path (picker skip), the banner either doesn't offer a switch-back
     control (there's nothing to switch back to) or offers "go to the group picker" instead. This is a
     small scope decision the planner should settle explicitly rather than leaving implicit — it
     doesn't touch any locked D-NN decision, both variants are consistent with D-03's wording ("carrying
     a switch-back control" is stated in the context of D-01's wrong-board case specifically).

2. **Where the closed registry (D-09) should live relative to `CrossBoardLinkRepository`.**
   - What we know: D-09 wants "a closed `(controller, action)` registry"; D-11 wants "every cross-board
     id→board lookup lives in one dedicated repository class."
   - What's unclear: Whether the registry (the `(controller, action) → lookup kind` mapping) belongs in
     the same repository class as the actual EF queries, or as a separate static lookup table consumed
     by both the repository and the middleware (the middleware needs to know *whether* a route
     participates before it's worth calling the repository at all, per the Pattern 1 code example's
     early-exit shape).
   - Recommendation: Keep the registry as a small static/DI-injected lookup consumed by the middleware
     for the early-exit check, and have the repository's public methods keyed by an enum/lookup-kind
     value the registry produces — this avoids the middleware needing repository access just to know
     whether a route is in scope, and keeps the repository itself string-free (matching this
     codebase's general preference for typed keys visible elsewhere, e.g. `SessionKeys`).

## Environment Availability

Not applicable — this phase introduces no new external tool, service, runtime, or package dependency.
Everything needed (ASP.NET Core 10, EF Core 10, SQL Server, the existing test stack) is already
present and exercised by the existing test suite; no new environment probe is warranted.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2, FluentAssertions v8.10.0, NSubstitute v5.3.0 [VERIFIED: .planning/codebase/TESTING.md — cross-checked against project convention; note the doc's CI section still says .NET 8.0.x, which is stale per project memory (`project_dotnet_version.md`: the app runs on .NET 10) — trust the runtime/test-framework version numbers, not the CI .NET-version line] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution — required, since tests share one in-memory database) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~CrossBoard"` (run from repo root `C:\Repos\quest-board`) |
| Full suite command | `dotnet test` (run from repo root `C:\Repos\quest-board`) |

### Phase Requirements → Test Map

| Decision ID | Behavior | Test Type | Automated Command | File Exists? |
|-------------|----------|-----------|--------------------|-------------|
| D-01 | Wrong-board GET lands on the page, board switched, no confirm step | integration | `dotnet test --filter "FullyQualifiedName~CrossBoardDeepLinkMiddlewareTests"` | ❌ Wave 0 |
| D-02 | Non-navigation request (missing/mismatched Sec-Fetch-*, or any prefetch hint present) never switches the board | integration | same filter as above, parameterized `[Theory]` over header combinations | ❌ Wave 0 |
| D-03/D-04 | Banner renders once, switch-back form has no `returnUrl`, no ping-pong on click | integration (HTTP assertions on response body + a second follow-up request to confirm the banner is gone) | same filter | ❌ Wave 0 |
| D-05 | `GroupPickerController.Index` skips the picker when `returnUrl` resolves to exactly one membership | integration | `dotnet test --filter "FullyQualifiedName~GroupPickerControllerIntegrationTests"` | Existing file likely needs new test methods — check for `GroupPickerControllerIntegrationTests.cs` in Wave 0 |
| D-09/D-10/D-11 | Closed registry; projection returns only an id; `IgnoreQueryFilters()` allowlist stays closed | unit (architecture test, mirrors `AmbientClockSeamTests`) | `dotnet test --filter "FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests"` | ❌ Wave 0 |
| D-12 | SuperAdmin with no membership in the target board gets the same 404 as anyone else | integration | same middleware test class, `[Fact]` with a SuperAdmin test client | ❌ Wave 0 |
| D-13/D-15 | Non-member-of-existing-entity and nonexistent-id produce byte-identical responses (status, body, headers) | integration, one paired test per registry entry (or per representative controller family) | same filter | ❌ Wave 0 |
| D-16 | All 18 routes are covered by the registry; the 6 image subresources and the Shop AJAX modal variant are excluded | integration, table-driven `[Theory]` over the 18 routes + `[Theory]` over the excluded 7 | same filter | ❌ Wave 0 |
| D-17 | DungeonMaster/Profile and EditProfile only resolve when unambiguous (exactly one shared board, not already active); null id on EditProfile never resolves | integration | `dotnet test --filter "FullyQualifiedName~DungeonMasterControllerIntegrationTests"` | Existing file likely needs new test methods |
| D-18 | Role-based authorization (`DungeonMasterOnly` policy) is judged against the switched board, not the pre-switch board | integration | same middleware test class, using `AuthenticationHelper.CreateAuthenticatedDMClientAsync` scoped to the target board | ❌ Wave 0 |
| D-19 | A cross-board POST behaves exactly as today (proceeds, then 404s inside the action) — the new middleware never fires for POST | integration | same middleware test class | ❌ Wave 0 |
| D-20 | Full unauthenticated email-link path (`Login` → `GroupPicker.Index(returnUrl)` → resolves → lands on page) works end-to-end | integration | `dotnet test --filter "FullyQualifiedName~AccountControllerIntegrationTests"` or a new end-to-end test class | ❌ Wave 0, likely a new test |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~CrossBoard"` (repo root
  `C:\Repos\quest-board`) — covers the new middleware, resolver, switcher, and registry in isolation.
- **Per wave merge:** `dotnet test` (full suite) — the shared resolver/switcher touch
  `GroupSessionMiddleware`'s null-board branch and `GroupPickerController`, both of which have existing
  regression coverage that must stay green.
- **Phase gate:** Full suite green before `/gsd-verify-work`, plus a manual/documented check that
  `dotnet build` from `C:\Repos\quest-board` (not the stale Linux-path prefixes recorded in earlier
  phase plans) succeeds.

### Wave 0 Gaps

- [ ] `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs` — covers D-01, D-02,
  D-03/D-04, D-12, D-13/D-15, D-16, D-18, D-19.
- [ ] `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` — covers D-11,
  mirroring `AmbientClockSeamTests.cs`'s file-scan pattern.
- [ ] New test methods appended to the existing `GroupPickerControllerIntegrationTests.cs` (confirm
  file name via Wave 0 — not verified to exist under that exact name this session, but
  `GroupPickerController` already has integration coverage per `TESTING.md`'s "Group management:
  multi-tenancy routing, group picker" line) — covers D-05.
- [ ] New test methods appended to the existing DungeonMaster controller integration test file —
  covers D-17.
- [ ] Framework install: none — `dotnet test` already runs the full stack needed.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V4 Access Control | yes — this is the core of the phase | Membership-pinned `IgnoreQueryFilters()` projection (D-10), fresh-per-request membership read (D-12), structural response parity (D-13) — never a role/claim cache |
| V2 Authentication | no (unchanged) | Existing ASP.NET Core Identity cookie auth, untouched |
| V3 Session Management | yes (session content changes, not session mechanism) | `ActiveGroupId`/`ActiveGroupName`/`ActiveGroupValidatedAtUtc` written only through the shared `ActiveBoardSwitcher` (D-08), never duplicated |
| V5 Input Validation | yes | Route `id` parsed as `int` before any query (existing `int id` action-parameter binding does this implicitly); registry lookup keyed by a closed enum, not raw strings, once past the initial route-value read |
| V6 Cryptography | no | Not applicable — no new secret/token material |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Tenant-isolation bypass (IDOR across boards) | Tampering / Information Disclosure | The single, allowlisted `IgnoreQueryFilters()` seam (D-11), membership-pinned in the SQL predicate itself (D-10) rather than checked after the fact |
| Membership oracle (distinguishable 404 for "exists on another board" vs "doesn't exist") | Information Disclosure | D-13's structural parity — no branch exists to produce a different response, verified by the D-15 paired test |
| Session fixation via unintended repoint (a GET silently changing which board is "yours") | Tampering | D-02's Fetch Metadata gate — **note:** this defends the *legitimate member's own browser* against its own background requests, not against a malicious third party (see Common Pitfalls Pitfall 1); the actual cross-site protection is that Session is a same-origin, cookie-scoped concept unreachable from another origin's script in the first place |
| Open redirect via `returnUrl` | Tampering | Already mitigated by the existing `Url.IsLocalUrl(returnUrl)` check in `GroupPickerController.RedirectToLocal` (verified, unchanged by this phase) and by `GroupSessionMiddleware`'s hardcoded `/groups/pick` redirect target (never user-supplied) |
| Privilege escalation via stale role judged against the wrong board | Elevation of Privilege | D-18's mandatory pipeline placement — the middleware runs before `UseAuthorization`, so `DungeonMasterHandler`'s live `activeGroupContext.ActiveGroupId` + fresh `GetGroupRoleAsync` read (verified pattern, not re-read this session but cited directly in 87-CONTEXT.md D-18 with its own verification note) always sees the post-switch board |

## Sources

### Primary (HIGH confidence)
- `C:/Repos/quest-board/QuestBoard.Service/Program.cs` (read in full, lines 1-370-ish; pipeline order
  at :317-325 and :360 `MapControllerRoute` verified directly)
- `C:/Repos/quest-board/QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (read in full)
- `C:/Repos/quest-board/QuestBoard.Service/Controllers/GroupPickerController.cs` (read in full)
- `C:/Repos/quest-board/QuestBoard.Service/Services/ActiveGroupContextService.cs` (read in full)
- `C:/Repos/quest-board/QuestBoard.Service/Constants/SessionKeys.cs` (read in full)
- `C:/Repos/quest-board/QuestBoard.Repository/Entities/QuestBoardContext.cs` (lines 380-572 read in
  full — all `HasQueryFilter` calls and the `IgnoreQueryFilters()`-adjacent comments)
- `C:/Repos/quest-board/QuestBoard.Repository/EventSignupRepository.cs`,
  `EventRepository.cs`, `GroupRepository.cs`, `QuestRepository.cs` (grep-confirmed all 7 current
  `IgnoreQueryFilters()` call sites with surrounding context)
- `C:/Repos/quest-board/QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` (read in
  full — confirms QuestLog resolves through `IQuestService`, no separate entity)
- `C:/Repos/quest-board/QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` (read
  in full — confirms D-17's `IsTargetInActiveGroupAsync` gate)
- `C:/Repos/quest-board/QuestBoard.Service/Controllers/AgendaController.cs` (read in full — confirms
  the fresh-membership-read pattern and no-SuperAdmin precedent this phase follows)
- `C:/Repos/quest-board/QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` (read in full — the
  allowlist architecture-test pattern this phase's D-11 test should mirror)
- `C:/Repos/quest-board/QuestBoard.Service/Views/Shared/_Toasts.cshtml`,
  `_Layout.cshtml`, `_Layout.Mobile.cshtml` (grep-confirmed both layouts render `_Toasts` identically)
- `C:/Repos/quest-board/QuestBoard.Service/Extensions/ControllerExtensions.cs` (read in full — the
  existing TempData-then-redirect helper convention)
- `C:/Repos/quest-board/QuestBoard.IntegrationTests/Mobile/MobileDetectionMiddlewareTests.cs` (read in
  full — the existing bare-`DefaultHttpContext` middleware unit-test style, and its route-value
  limitation noted in Common Pitfalls Pitfall 2)
- `C:/Repos/quest-board/.planning/codebase/TESTING.md` (read in full)
- `C:/Repos/quest-board/.planning/phases/87-cross-board-deep-link-recovery/87-CONTEXT.md` (read in
  full — the phase's locked decisions)
- `C:/Repos/quest-board/.planning/config.json` (read in full — confirms `nyquist_validation: true`,
  no `security_enforcement` key present, hence Security Domain section included)

### Secondary (MEDIUM confidence)
- [MDN — Fetch metadata request headers guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/Fetch_metadata) — `Sec-Fetch-Dest`/`Sec-Fetch-Mode` semantics, `document`/`navigate` values, example header set for a top-level same-origin navigation
- [MDN — Sec-Fetch-Mode header](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Sec-Fetch-Mode) — valid values including `navigate`
- [MDN — Sec-Fetch-Dest header](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Sec-Fetch-Dest) — valid values including `document`, `image`, `empty`
- [MDN — Sec-Purpose header](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Sec-Purpose) — `prefetch` / `prefetch;prerender` values, relationship to the legacy `Purpose` header
- [Chromium blink-dev — Intent to Ship: Pass 'Sec-Purpose: prefetch' header](https://groups.google.com/a/chromium.org/g/blink-dev/c/0yrUBDA8uUs) — confirms Chrome sends both `Purpose: prefetch` and `Sec-Purpose: prefetch` during the transition
- [WebKit/standards-positions#493](https://github.com/WebKit/standards-positions/issues/493) and [mozilla/standards-positions#1228](https://github.com/mozilla/standards-positions/issues/1228) — cross-browser positions on `Sec-Purpose`, confirms Firefox's move from `X-Moz: prefetch` to `Sec-Purpose`

### Tertiary (LOW confidence)
- None — every claim above rests on a direct file read (this session) or an MDN/browser-vendor primary
  source.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; every mechanism used is either verified in-repo or a
  browser-standard HTTP header confirmed against MDN
- Architecture: HIGH — pipeline order, session/TempData access, and query-filter shape all verified by
  direct file reads this session, not inferred from CONTEXT.md's citations alone
- Pitfalls: HIGH — every named pitfall traces to a specific verified line or a specific verified
  absence (no `Sec-Fetch` precedent in-repo, no `UseStatusCodePages` registration)

**Research date:** 2026-09-21
**Valid until:** 30 days (stable ASP.NET Core/EF Core mechanisms; no fast-moving dependency in scope) —
re-verify the Fetch Metadata header transition state (`Purpose` → `Sec-Purpose`) if this phase slips
past several months, since that header is the one still-settling piece of the stack referenced here.
