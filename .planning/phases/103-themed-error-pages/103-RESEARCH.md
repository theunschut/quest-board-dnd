# Phase 103: Themed Error Pages - Research

**Researched:** 2026-11-18
**Domain:** ASP.NET Core 10 MVC error-handling pipeline (status-code re-execute, exception handler, rate limiter), tenant-safe cross-board link resolution, v9 theme (Razor, layered CSS, phone twins)
**Confidence:** HIGH on pipeline behaviour (spiked against the real framework this session), HIGH on the resolver audit (every target action read), MEDIUM on production logging (the unprivileged `claude` account cannot read the CT 102 journal)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

### Cross-board 404s and the deep-link switch
- **D-01: The switch's reach does not change.** `CrossBoardDeepLinkMiddleware` and
  `CrossBoardLinkRegistry` keep the same closed route list and the same membership requirement. No
  routes are added, and nothing switches to a board the viewer does not belong to. The error page,
  not a wider switch, answers the operator's case.
- **D-02: The 404 page carries one fixed sentence for every viewer and every 404.** An example:
  "It may have been removed, or it may belong to a board you're not viewing right now." The wording
  never depends on the requested id, the record, or whether the record exists anywhere. Nothing on
  the page reveals anything about another board's records. That includes the page itself: the
  same body for a missing id and for an id on a board the viewer cannot see.
- **D-03: A "Switch board" button to the board picker (`/groups/pick`) appears on the 404 page
  only for an authenticated viewer who belongs to two or more boards.** It depends on the viewer's
  own memberships, never on the record. It is a secondary action next to the three required
  buttons. It is the manual way out when the switch did not fire.
- **D-04: The phase 99 Draft/scheduled deep-link leak is fixed in this phase.** The resolver
  returns a board only when the viewer could open the record there. For a shop item, that means
  the viewer would pass `Shop/Details` on the target board: they are a Dungeon Master or Admin of
  that board or a SuperAdmin, or the item is visible to members (Published or Archived, and its
  sale has started). Otherwise the resolver answers "not found", exactly the path a nonexistent id
  takes. The request then gets the normal themed 404 with no switch and no one-shot banner. —
  **Reversibility:** costly — the resolver becomes visibility-aware; reverting reopens the leak
  and touches the resolver, its repository query and the guard facts.
- **D-05: Every lookup kind in the registry is audited, not only shop items.** Research lists each
  kind whose target action can answer 404, or refuse, for a member on a record that exists. The
  kinds are Quest, Event, EventSeries, Character, Contact, ContactCategory, ShopItem and
  BoardMember. The list covers Details, Edit, Manage and the other registered actions. The D-04
  rule ("switch only if the viewer could open it there") applies to each kind found. Kinds with
  no such rule are recorded as checked and left as they are.
- **D-06: The membership-pinned lookup stays the tenancy boundary.** The fix never adds a branch
  that answers differently for "exists but hidden" and "does not exist": same response, same
  headers, no banner, and no session change. Guard facts pin both
  paths as indistinguishable.

### Which responses get the page
- **D-07: Every bodiless 4xx/5xx gets the themed page.** One status-code re-execute path covers
  all error statuses, with specific copy for 400, 404 and 500 and one generic fallback for the
  rest. A response that already has a body keeps it, except 429 (D-10).
- **D-08: 403 joins the new page.** `Account/AccessDenied` becomes the 403 variant of the shared
  error page: the same look and buttons, with today's two cases for authenticated and anonymous
  viewers. The cookie `AccessDeniedPath` (`/Account/AccessDenied`) and its URL stay the same.
- **D-09: Only browser document navigations get the HTML page.** That means a request whose
  `Accept` includes `text/html` and that is not `X-Requested-With: XMLHttpRequest`; research
  refines the rule. Fetch callers keep exactly what they get today. That covers the shop item
  modal and its `isModal` Details form, the markdown preview, event details, the series form and
  the shop preview. No existing JS changes behaviour.
- **D-10: 429 becomes a themed page for browser navigations.** It keeps the same sentence, "Too
  many requests. Please try again later.", and the 429 status. Non-HTML callers keep the plain
  text body. This is done in the rate limiter's `OnRejected`.

### 500 in production and development
- **D-11: Development keeps the developer exception page for unhandled exceptions.** Production
  and Testing use the themed 500, so integration tests can assert it. Status-code pages (404 and
  the rest) are themed in every environment, Development included.
- **D-12: The 500 page shows "Reference: <TraceIdentifier>".** That is the same id the exception
  handler logs, so the operator can find it in the CT 102 logs. Nothing else from the exception
  reaches the page: no message, type or path.
- **D-13: No new logging or alerting.** The built-in exception handler logging, which carries the
  trace id, is enough.
- **D-14: The broken `/Error` handler is fixed.** `UseExceptionHandler("/Error")` points at a route
  with no action today, so a production 500 ends bare. The new error endpoint serves that path,
  and the `/Error` exemption in `GroupSessionMiddleware` stays valid for it.

### Buttons, anonymous viewers and the phone layout
- **D-15: "Go back" is `history.back()`, wired by a script file in `wwwroot/js`.** No inline
  script is used (CSP). With no history, or without JS, it falls back to a same-site `Referer`
  link, and to hiding the button when there is none. An off-site `Referer` is never followed.
- **D-16: "Home" goes to `/`.**
- **D-17: An authenticated viewer gets "Log out", which ends on the login page.** An anonymous
  viewer gets "Log in" instead. It goes to the login page with a `returnUrl` only when that URL
  is local. Button order and roles follow `.claude/ui-guidelines.md`: filled buttons with icons,
  `btn-secondary` for back, and the commit-style fill reserved as the guidelines say.
- **D-18: There is a `.Mobile.cshtml` twin with its own `*.mobile.css`**, as in the converted
  areas: no `@media` in the twin, and 44px targets.
- **D-19: A Gate 1 look check comes before the build.** It covers mockups of the 404 (with and
  without the Switch board button), 500 (with the reference), 403 and anonymous variants, on
  desktop and phone. They are compared against the live converted phase 98 to 100 pages, as in
  the phase 98 to 100 practice. Nothing visual is built before the operator approves Gate 1.

### Claude's Discretion
- **Log out mechanism (operator said "you decide"):** prefer reusing the existing
  `Account/Logout` POST form with an anti-forgery token rendered on the error page. Any mechanism
  is acceptable as long as logout can never be triggered by a cross-site GET or link.
- The exact copy per status code, within D-02's no-leak rule and the guidelines' tone.
- Which layout the error page uses for a failure under `/platform` (Platform layout versus the main
  one). Also a minimal fallback when the layout itself throws (for example the database is down
  or the active-board lookup fails), so the error page never errors into a bare response.
- Excluding `/hangfire`, `/health` and static-file paths from the HTML error page if needed.
- How the page renders with no active board, for example a 404 reached from the group picker
  path.

### Deferred Ideas (OUT OF SCOPE)
- **Widening the automatic board switch.** This means more routes in the registry, such as index
  pages or the quest log, or switching to boards the viewer is not a member of. The operator kept
  the switch's reach as it is (D-01). It would be a backlog item if 404s keep coming from
  unregistered routes.
- **Alerting on 500s** (email or notification): a new capability, not requested.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs were assigned (ROADMAP: "Requirements: TBD"). CONTEXT.md decisions D-01..D-19 are the contract. Research support per decision:

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01, D-06 | Reach unchanged; one indistinguishable path | Registry audit (section "D-04/D-05 audit"); resolver stays one membership-pinned query; parity pins in "Validation Architecture" |
| D-02, D-03 | Fixed 404 sentence; Switch board button | Copy table; button depends only on `IGroupService.GetGroupsForUserAsync(viewer)`; no echo of path/id |
| D-04, D-05 | Visibility-aware resolver for all kinds | Per-route table with predicate for each of the 18 registered routes; second resolver caller (`GroupPickerController.Index`) found |
| D-07, D-09, D-10 | Which responses get the page | Pipeline placement, navigation rule, `IStatusCodePagesFeature.Enabled=false` gate, 429 via re-execute (all spiked) |
| D-08 | 403 variant | `AccessDenied` is a 200 page reached by 302; status/wording/guard-test impact listed |
| D-11, D-12, D-13, D-14 | 500 behaviour and Reference | Dev page is automatic; `/Error` fix; D-12/D-13 premise correction (scopes are off by default) |
| D-15, D-16, D-17 | Buttons | Same-site Referer helper, JS wiring, `Logout(toLogin)` mechanism |
| D-18, D-19 | Twin and Gate 1 | Name-based view lookup for the twin, layout/guard-list impact, phase 98-100 plan shape and fixtures |
</phase_requirements>

## Summary

The phase is two independent pieces of work that share a page. (1) A pipeline piece: one `UseStatusCodePagesWithReExecute("/Error/{0}")` for every environment, a small gate that turns the feature off for anything that is not a browser document navigation, a new `ErrorController` that also serves the exception handler's `/Error`, a minimal layout that cannot throw, a `.Mobile` twin and one script file. (2) A security piece: the cross-board resolver becomes visibility-aware for every registered route, so a member is never switched to a board for a record they could not open there. Both were checked against the real framework (spikes in the scratchpad) and the real controllers.

Four findings change or extend CONTEXT.md and the planner must carry them (details in "Findings that correct or extend CONTEXT.md"): the registry has **18** routes, not 19; the exception-handler log line does **not** carry the trace id with the default console format, so D-12's "same id the handler logs" is false until scopes are switched on; the resolver has a **second caller** (`GroupPickerController.Index`, the post-login picker skip) that the same fix must cover; and several existing facts pin today's leak or today's bare 404 and must be rewritten, not deleted.

**Primary recommendation:** Put `UseStatusCodePagesWithReExecute("/Error/{0}", createScopeForStatusCodePages: true)` after `UseStaticFiles` and before `UseRouting`, followed by a gate middleware that sets `IStatusCodePagesFeature.Enabled = false` unless `Accept` contains `text/html`, no `X-Requested-With: XMLHttpRequest`, and `Sec-Fetch-Dest` (when present) is `document`/`iframe`/`frame`. Render every themed page through one dedicated minimal layout pair (no DB reads) via the name-based view `Views/Shared/ErrorPage`. Make the resolver take the route's required access level plus a SuperAdmin flag and apply board-level (role, page availability) and record-level predicates inside the single membership-pinned query, so "hidden" and "nonexistent" share one code path.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Deciding a response is an error page | API / Backend (ASP.NET pipeline) | — | `StatusCodePagesMiddleware` and `ExceptionHandlerMiddleware` own it; no client logic |
| Browser-navigation vs fetch decision | API / Backend (gate middleware, one static helper) | — | Request headers only; the same helper serves the gate, the Error action and `OnRejected` |
| Error page rendering | Frontend Server (SSR Razor) | CDN / Static (CSS, JS) | Server renders HTML; sheets and `error-actions.js` are static files |
| Go back | Browser / Client (`history.back()`) | Frontend Server (same-site Referer fallback link) | History exists only in the browser; the fallback must work without JS |
| Log out | API / Backend (`Account/Logout` POST, antiforgery) | Frontend Server (form on the page) | State change, must be POST with a token |
| Switch-board button visibility | API / Backend (membership count) | — | Depends on the viewer's own memberships only |
| Visibility-aware cross-board resolution | Database / Storage (one predicate query in `CrossBoardLinkRepository`) | API / Backend (Domain resolver computes role and page-availability sets) | Tenancy boundary stays in the one file allowed to `IgnoreQueryFilters()` |
| Reference id for 500 | API / Backend (`HttpContext.TraceIdentifier`) | Operations (journald scopes) | The id only helps if the log line carries it |
| Rate-limit refusal page | API / Backend (`OnRejected` + re-execute) | — | Limiter owns the status; the page comes from the re-execute |

## Findings that correct or extend CONTEXT.md

| # | Finding | Evidence |
|---|---------|----------|
| F1 | The registry has **18** routes (8 kinds), not 19 | `CrossBoardLinkRegistry.cs:33-62` (18 `[DefaultRoute<...>]` entries, counted with grep) and the existing unit fact `RegisteredRoutes_HasExactly18Entries` (`QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs:50-52`: `CrossBoardLinkRegistry.RegisteredRoutes.Should().HaveCount(18);`) [VERIFIED] |
| F2 | D-12/D-13 premise is false by default: the exception handler's log line carries **no** request id unless console scopes are on | Spike (Production env, default builder): the `fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]` line printed only "An unhandled exception has occurred while executing the request." plus the stack; with `Logging__Console__IncludeScopes=true` the same line carried `=> SpanId:..., TraceId:... => ConnectionId:0HNQ5HMTIBRS3 => RequestPath:/boom RequestId:0HNQ5HMTIBRS3:00000001`, and `HttpContext.TraceIdentifier` was that same `0HNQ5HMTIBRS3:00000001` value [VERIFIED: spike]. The repo configures no console options (`appsettings.json` has only `Logging:LogLevel`) and production runs the default console logger under systemd (`docs/server-setup.md:222` `journalctl -u questboard -f`). The CT 102 journal is not readable by the `claude` account (0 lines returned), so production output is unconfirmed [VERIFIED: ssh probe] |
| F3 | `ICrossBoardLinkResolver` has **two** callers: the middleware and `GroupPickerController.Index` (post-login picker skip) | `GroupPickerController.cs:54`: `var resolved = await crossBoardLinkResolver.ResolveAsync(target.Kind, target.Id, userId);` and `CrossBoardDeepLinkMiddleware.cs:77` [VERIFIED]. The picker derives the target from a returnUrl via `CrossBoardRouteTarget.TryFromLocalUrl`, so it leaks the same way and must apply the same predicate |
| F4 | The "2 `StatusCode()` returns" are in `CalendarFeedController` (410 and 304), not admin code; `AdminController` writes its 429 **with a body** (`Response.StatusCode = StatusCodes.Status429TooManyRequests;` at `:265` and `:386`), so those responses keep their text under D-07 | grep [VERIFIED] |
| F5 | Existing facts pin today's behaviour and must be rewritten to the same intent | `CrossBoardOracleParityTests.CrossBoardOracleParity_UnresolvableEntity_Returns404WithEmptyBody` (`:260`, asserts `body.Should().BeEmpty()`); `CrossBoardAuthorizationBoundaryTests.ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself` (`:244`) and `DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsRefusedTheQuestEditPage` (`:124`, asserts the board "must have switched"); the class doc says "the resolver itself never consults roles at all"; `ICrossBoardLinkResolver` and `CrossBoardLinkResolverService` carry comments stating the same [VERIFIED] |
| F6 | `Account/Logout` redirects to `Home/Index`, not the login page, and a fact pins it | `AccountController.cs:173-179` (`return RedirectToAction(nameof(HomeController.Index), "Home");`), `AccountControllerIntegrationTests.cs:237` `Logout_Post_ShouldRedirectToHome`. D-17 needs a new, explicit way to end on login |
| F7 | `Account/AccessDenied` is an ordinary **200** page reached by a 302 from the cookie handler; a fact pins 200 and the heading text | `AccountController.cs:43-48`; `AccountControllerIntegrationTests.cs:27-36` asserts `HttpStatusCode.OK`, `Contain("You Don't Have Permission")`, `NotContain("Dungeon Master")`. `AccessDenied.cshtml` is also in the Phase 96 account guard (`AccountConversionGuardTests.cs:26,54,414`) |
| F8 | A third "refusal" outcome exists besides 404/403: `BoardPageGateFilter` redirects to the board's home with a notice when the target board does not offer the page, after the switch has already happened | `BoardPageGateFilter.cs` (resource filter, `context.Result = new RedirectToActionResult(route.Action, route.Controller, new { area = "" })`). Registered controllers carry `[BoardPage(...)]`. It leaks "this id exists on that board" exactly like a hidden record |
| F9 | 20 of 33 `BadRequest(...)` calls carry a message body (e.g. `CalendarController.cs:38` "Invalid month...") and so keep their plain text under D-07 | grep [VERIFIED]; accepted limitation, document it in the plan |
| F10 | A re-executed request keeps its **method** (POST stays POST) and MVC `[HttpGet]` does not match HEAD, so the Error action needs no verb attribute | Spike: re-executed `POST /nf` showed `method=POST`; `HEAD` against a GET/POST-only error route returned 405 with `Allow: GET, POST` [VERIFIED: spike] |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET / ASP.NET Core | SDK 10.0.115, runtime 10.0.15 | Pipeline, MVC, Razor, rate limiter | Already the stack; no change [VERIFIED: `dotnet --list-sdks`, `--list-runtimes`] |
| `Microsoft.AspNetCore.Diagnostics` (in the shared framework) | 10.0.x | `UseStatusCodePagesWithReExecute`, `UseExceptionHandler`, `IStatusCodePagesFeature`, `IStatusCodeReExecuteFeature`, `IExceptionHandlerPathFeature` | Built-in; do not hand-roll [CITED: github.com/dotnet/aspnetcore release/10.0 `StatusCodePagesMiddleware.cs`, `StatusCodePagesExtensions.cs`, `ExceptionHandlerOptions.cs`] |
| ASP.NET Core Antiforgery, Razor `FormTagHelper` | 10.0.x | Token on the Logout form | Existing pattern (`_Layout.cshtml:256`) |

### Supporting (tests)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Test framework | All new facts (`TestContext.Current.CancellationToken` style) [VERIFIED: csproj] |
| FluentAssertions | 8.10.0 | Assertions | All new facts [VERIFIED: csproj] |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | `WebApplicationFactory<Program>`, Testing environment | Integration facts [VERIFIED: csproj] |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Test database | Resolver predicate facts |
| NSubstitute | 5.3.0 (UnitTests only) | Stubs | Unit facts |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Gate middleware that flips `IStatusCodePagesFeature.Enabled` | Custom `UseStatusCodePages(async ctx => ...)` that re-executes by hand | More code and loses the built-in endpoint/route-value reset; the gate is 10 lines |
| `[SkipStatusCodePages]`-style endpoint metadata | Runtime gate | Metadata is static; the decision here is per request (headers). Metadata remains right for any future JSON-only controller (the middleware reads `ISkipStatusCodePagesMetadata`, source quoted below) |
| Full `_Layout` for the error page | Dedicated minimal layout | The full layout does 4+ database reads (see Q6); it can throw exactly when the error page is needed |

**Installation:** none. No package is added by this phase.

## Package Legitimacy Audit

No external package is installed or added by this phase (framework features and existing test packages only), so the Package Legitimacy Gate does not apply. Packages named above were read from the repo's own `.csproj` files, not discovered externally.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Browser request
   |
   v
UseForwardedHeaders
   |
   v
UseExceptionHandler(/Error, CreateScopeForErrors)   [non-Development only; Development gets the auto-added developer page]
   |
   v
UseHttpsRedirection -> UseStaticFiles
   |
   v
UseStatusCodePagesWithReExecute("/Error/{0}", scope)   NEW, all environments
   |
   v
ErrorPageGate (NEW): not a document navigation, or path is /hangfire or /health
   |             -> IStatusCodePagesFeature.Enabled = false  (response stays exactly as today)
   v
MobileDetection -> UseRouting -> UseSession -> UseAuthentication
   -> GroupSessionMiddleware (exempt: /Error, /groups/pick, /Account, /platform, /Agenda)
   -> CrossBoardDeepLinkMiddleware (visibility-aware resolver)
   -> UseRateLimiter (OnRejected: status 429, body only for non-navigations)
   -> UseAuthorization -> [hangfire] -> endpoints
   |
   +-- endpoint returns bodiless 4xx/5xx ---------+
   +-- unhandled exception -> ExceptionHandler ---+---> re-execute /Error/{code} or /Error
                                                   |
                                                   v
                              ErrorController (any verb, AllowAnonymous)
                                |  navigation?  no -> bare StatusCode(n), as today
                                |  yes
                                v
                              View "ErrorPage" (+ .Mobile twin) in _Layout.Error (+ .Mobile): no DB reads
                                |  optional: Switch-board flag from membership count (try/catch, fail closed)
                                v
                              [Go back][Home][Log out | Log in][Switch board]
```

### Recommended Project Structure
```
QuestBoard.Service/
  Controllers/ErrorController.cs             # one action, any verb; also serves /Error for the exception handler
  Helpers/BrowserNavigation.cs               # IsDocumentNavigation(HttpRequest), SameSiteLocalReferer(HttpRequest)
  Middleware/ErrorPageGateMiddleware.cs      # flips IStatusCodePagesFeature.Enabled
  ViewModels/ErrorPageViewModel.cs           # StatusCode, Heading, Sentence, Reference?, Variant, ShowSwitchBoard, BackHref?, LoginReturnUrl?
  Views/Shared/ErrorPage.cshtml              # name-based so the expander finds the twin
  Views/Shared/ErrorPage.Mobile.cshtml
  Views/Shared/_Layout.Error.cshtml          # brand bar only, no injected services that read the DB
  Views/Shared/_Layout.Error.Mobile.cshtml
  wwwroot/css/error.css                      # @layer pages, tokens only
  wwwroot/css/error.mobile.css               # twin sheet, no @media
  wwwroot/js/error-actions.js                # history.back() wiring, no inline code
QuestBoard.Domain/ (+ Repository)            # CrossBoardAccess enum, resolver/repository predicates
```
Delete `Views/Shared/AccessDenied.cshtml` once `AccountController.AccessDenied` returns the shared view (update `AccountConversionGuardTests` lists).

### Pattern 1: Pipeline order (current and target)

Current order [VERIFIED: `QuestBoard.Service/Program.cs:317-368`]:

| # | Middleware | Line |
|---|-----------|------|
| 1 | `UseForwardedHeaders` | 317 |
| 2 | `if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }` | 319-323 |
| 3 | `UseHttpsRedirection`, `UseStaticFiles`, `MobileDetectionMiddleware` | 325-327 |
| 4 | `UseRouting` | 329 |
| 5 | `UseSession`, `UseAuthentication`, `GroupSessionMiddleware` | 331-333 |
| 6 | `CrossBoardDeepLinkMiddleware` (comment: runs before authorization on purpose) | 338 |
| 7 | `UseRateLimiter`, `UseAuthorization` | 339-340 |
| 8 | non-Testing: inline `/hangfire` redirect middleware, `UseHangfireDashboard` | 342-368 |
| 9 | `MapAreaControllerRoute("platform")`, `MapControllerRoute("default")`, `MapHealthChecks("/health")` | 370-388 |

Target: insert, after `UseStaticFiles` and before `MobileDetectionMiddleware`/`UseRouting`:
```csharp
app.UseStatusCodePagesWithReExecute("/Error/{0}", createScopeForStatusCodePages: true);   // every environment (D-11)
app.UseMiddleware<ErrorPageGateMiddleware>();
```
and change line 321 to the `ExceptionHandlerOptions` overload with `ExceptionHandlingPath = "/Error"` and `CreateScopeForErrors = true` (a failed `SaveChanges` leaves the request scope's `DbContext` unusable; the error page must not run on it) [CITED: `ExceptionHandlerOptions.CreateScopeForErrors`, release/10.0 source]. Development keeps the auto-added developer exception page: verified by spike (Development, no `UseExceptionHandler`, `/boom` returned the developer HTML while `/nf` still re-executed to `/Error/404`) [VERIFIED: spike].

How re-execute interacts with the rest [VERIFIED: spike unless noted]:
- **StatusCodePages is outermost of the app pipeline, so it sees the rate limiter's 429.** `OnRejected` that sets 429 and writes nothing yielded `ERRCODE 429 ... status=429` for `Accept: text/html`; writing the sentence for other callers kept the plain text.
- **It only fires on bodiless responses.** Source: skipped when `Response.HasStarted`, status outside 400-599, `ContentLength.HasValue`, or a non-empty `ContentType`; also skipped when `IStatusCodePagesFeature.Enabled` is false or the endpoint carries skip metadata [CITED: `StatusCodePagesMiddleware.cs`].
- **Endpoint and route values are reset and routing runs again**; the original is exposed as `IStatusCodeReExecuteFeature` (`OriginalPath`, `OriginalStatusCode`) [CITED: `StatusCodePagesExtensions.cs`]. Never render `OriginalPath` (D-02 parity).
- **Method and body state carry over**; `HEAD`/`POST` need an error endpoint that accepts them (F10).
- **Session, authentication, `GroupSessionMiddleware`, `CrossBoardDeepLinkMiddleware`, rate limiter, authorization all run again** on `/Error/{code}`. `GroupSessionMiddleware` exempts `/Error` with `StartsWithSegments` (`GroupSessionMiddleware.cs:63-70`, entry `"/Error",`), which also covers `/Error/404`. The deep-link middleware finds no registry entry for controller `Error`. The error endpoint has no rate-limit metadata. `[AllowAnonymous]` keeps authorization out of the way. The global `BoardPageGateFilter` only acts when an endpoint has `[BoardPage]`.
- **Antiforgery**: MVC here has no global auto-validate (`Program.cs:38` registers only `BoardPageGateFilter`), so a re-executed POST is not validated; add `[IgnoreAntiforgeryToken]` anyway. A failed `[ValidateAntiForgeryToken]` yields a bodiless 400 [ASSUMED: framework behaviour; add a fact] and so gets the themed 400.
- **Exception path**: the handler clears the response, sets 500, re-executes the path and logs; if the handler itself throws, the original exception is rethrown and Kestrel answers a bare 500 (acceptable last resort) [CITED: `ExceptionHandlerMiddlewareImpl.cs`].
- **Today's `/Error` is broken exactly as D-14 says**: with no endpoint the handler sees 404 and rethrows (`AllowStatusCode404Response` defaults to false) [CITED: `ExceptionHandlerOptions.cs`]; in TestServer that surfaces as an exception to the client, so no fact can assert a 500 page today.

### Pattern 2: Browser-navigation rule (D-09 refined)

```csharp
// Sketch, not source.
internal static class BrowserNavigation
{
    public static bool IsDocumentNavigation(HttpRequest r)
    {
        if (string.Equals(r.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)) return false;
        if (!r.Headers.Accept.Any(v => v != null && v.Contains("text/html", StringComparison.OrdinalIgnoreCase))) return false;
        var dest = r.Headers["Sec-Fetch-Dest"].ToString();
        return dest.Length == 0 || dest is "document" or "iframe" or "frame";   // fetch/XHR send "empty", images "image"
    }
}
```
Rationale: a browser navigation sends `Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8` [CITED: developer.mozilla.org Accept]; `fetch()`, XHR and `sendBeacon` send `Sec-Fetch-Dest: empty`, a top-level navigation sends `document`, frames send `iframe`/`frame` [CITED: developer.mozilla.org Sec-Fetch-Dest]. `Accept` is the primary test because older Safari does not send `Sec-Fetch-*` (hence "when present"); `Sec-Fetch-Dest` is the guard that stops a future `fetch` with `Accept: text/html` from receiving a page. This differs from the switch middleware's own `IsRealTopLevelNavigation` on purpose (that one requires `Sec-Fetch-Dest: document` and `Sec-Fetch-Mode: navigate` and also rejects prefetch hints because it moves the board; the error page has no side effect so it should not).

Audit of every JS fetch caller [VERIFIED: grep of `wwwroot/js` plus reading each call]. None sets `Accept` or `X-Requested-With`, so every one sends `Accept: */*` and `Sec-Fetch-Dest: empty`, and none is a navigation under the rule:

| Caller | Request | What it does with a non-OK answer | Under the rule |
|--------|---------|-----------------------------------|----------------|
| `quest-details.js:101,122` revoke signup / change vote | DELETE/POST FormData | `res.text()` into an `alert`; a page body would be dumped into the alert | stays bare |
| `quest-manage.js:16,39` delete quest / remove player | DELETE with `RequestVerificationToken` header | alert; logs status | stays bare |
| `event-details.js:22` availability | POST FormData | `res.text()` into an `alert` | stays bare |
| `event-series-form.js:190,314` preview / collision | POST FormData, `res.json()` | `.catch` fallback | stays bare |
| `markdown-editor.js:46`, `shop-item-preview.js:70` | POST JSON | `!response.ok` throws, caught | stays bare |
| `shop-item-modal.js:38` | `fetch(url)` of `Shop/Details?isModal` | `!response.ok \|\| response.redirected` throws | stays bare (a 404 stays empty; a `Forbid()` 302 to AccessDenied still sets `redirected`) |
| `image-crop.js:353` | `fetch(cropOriginalUrl)` | `!response.ok` handled | stays bare |

Static assets, images and the calendar feed are never navigations (`Accept` lacks `text/html`). Existing integration tests send no `Accept`, so with this rule every existing status assertion is untouched; new facts must add `Accept: text/html` (the existing cross-board facts send only `Sec-Fetch-Dest/Mode`, which are not enough under this rule; see Q10).

Gate:
```csharp
// Sketch, not source.
public async Task InvokeAsync(HttpContext ctx)
{
    var excluded = ctx.Request.Path.StartsWithSegments("/hangfire") || ctx.Request.Path.StartsWithSegments("/health");
    if (excluded || !BrowserNavigation.IsDocumentNavigation(ctx.Request))
        ctx.Features.Get<IStatusCodePagesFeature>()!.Enabled = false;
    await next(ctx);
}
```
The middleware reads `Enabled` after the rest of the pipeline runs, so a downstream flip works [VERIFIED: spike with `Accept: */*` returning a bare 404 and bare plain-text 429].

### Pattern 3: `ErrorController`

One action, no `[Http*]` attribute (matches every verb including HEAD), `[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`, no-store, `[Route("Error/{code:int?}")]` (attribute route keeps `GroupPickerController`'s precedent of explicit routes). Behaviour:
- Status = `Response.StatusCode` when 400-599 (already the original status on both re-execute paths), else the route `code` when 400-599, else 404. A direct visit to `/Error/418` returns that status with the generic page; `/Error` with no feature present returns 500.
- Not a document navigation (only reachable on the exception path, since the gate disables the status-code path): return `StatusCode(500)` with no body, which is what Kestrel produced after the failed re-execute today.
- 500: `Reference = HttpContext.TraceIdentifier`; nothing from `IExceptionHandlerFeature` reaches the model (D-12).
- The Switch-board flag is `memberships.Count >= 2` from `IGroupService.GetGroupsForUserAsync(userId)` inside `try/catch`; any exception means "no button" (fail closed; the page never errors).
- Anonymous: `LoginReturnUrl` is the original path+query from `IStatusCodeReExecuteFeature` only when `Url.IsLocalUrl(...)`; for AccessDenied the cookie handler already supplies `?ReturnUrl=`, validated the same way.

### Pattern 4: Minimal layout (Q6)

Dedicated `_Layout.Error.cshtml` (desktop) and `_Layout.Error.Mobile.cshtml` (phone), selected by **name** from the view (`Layout = "_Layout.Error";`) so the expander finds the twin, exactly as `Areas/Platform/Views/_ViewStart.cshtml` does with `Layout = "_Layout.Platform";`. Model the markup on `_Layout.GroupPicker.cshtml` (links `app, tokens, base, components, shell, legacy-pages`, brand bar, skip link, `main#main-content`, scripts) but: no `@inject`-driven reads, no user-name lookup, no `BoardPages`, brand links to `/`, no `_Toasts` (an error page must not consume a pending one-shot message; the toast cookie survives for the next page), footer optional (`_Footer` reads only `AppVersion.Current`, safe).

### Pattern 5: Resolver becomes visibility-aware (Q3)

Keep one query shape and no hidden/nonexistent branch:
1. Domain resolver reads memberships (already does) and now also each membership's role and board type/disabled pages. `GroupWithMemberCount` has `BoardType` and `HomePage` but neither role nor `DisabledPages` (`GroupRepository.cs:32-46`), so extend that projection or add one method to `CrossBoardLinkRepository` (the only file allowed to call `IgnoreQueryFilters`; `CrossBoardIgnoreQueryFiltersSeamTests` lists it).
2. From those it computes **board-level** sets: member boards where the page is available (`BoardPagePolicy.IsAvailable(type, disabled, page)`) and where the viewer is DM-tier or Admin (SuperAdmin counts as Admin on every board they are a member of).
3. It passes the sets, the viewer id and `boardNow`/`boardToday` (from `IBoardClock`) to the repository, whose single query adds the **record-level** predicate (shop status and window, contact revealed/creator, quest-log completion, quest/character ownership). Precedent for passing the board clock into an EF predicate: `ShopRepository.OnSale(query, boardNow)` (`ShopRepository.cs:121-125`).
4. Signature change, deliberate: `ResolveAsync(kind, access, id, userId, viewerIsSuperAdmin, token)` with a new Domain enum `CrossBoardAccess`, carried by `CrossBoardRouteTarget` and set once per route in the registry (the registry stays the single closed table; both callers read it). The current doc comment says a SuperAdmin branch cannot be added "without changing the signature"; this is that deliberate change, and D-04 explicitly names SuperAdmin. Membership is still required, so a SuperAdmin who is not a member still resolves to nothing (`CrossBoardOracleParity_SuperAdmin_...` stays green).

### Anti-Patterns to Avoid
- **Echoing the failing URL, id or `IStatusCodeReExecuteFeature.OriginalPath` into the page.** Breaks D-02 and the parity facts (two requests with different ids must produce the same body).
- **Rendering the page with the main `_Layout`.** It reads the database and can throw on the error path.
- **A different response for "exists but hidden".** Any `if (exists && !visible)` branch in Service code; the predicate belongs inside the one SQL query.
- **`View("~/Views/...")` for the shared view.** An app-relative path is looked up exactly and bypasses `MobileViewLocationExpander`, so the phone twin would never be chosen [ASSUMED: Razor engine behaviour, pin with a fact; `GroupPicker/Index` uses an explicit layout path and has no twin]. Use a name that resolves from `Views/Shared/`.
- **Copying business rules into the Service middleware.** Put each rule in Domain once and have both the controller and the resolver call it (see Don't Hand-Roll), with the agreement matrix as the drift guard.

## D-04 / D-05 audit: the 18 registered routes

Every target action was read this session. "After the switch" means the request continues into the action with the target board active, so the action's policy, ownership checks and the global `BoardPageGateFilter` run against the target board. Verbatim anchors for the discrete values used:

- `ItemStatus`: `Draft, Published, Archived, Denied` [VERIFIED: `QuestBoard.Domain/Enums/ItemStatus.cs:3-9`: `public enum ItemStatus { Draft, Published, Archived, Denied }`]
- `GroupRole`: `Player = 0, DungeonMaster = 1, Admin = 2` [VERIFIED: `GroupRole.cs:3-8`]
- `BoardPage`: `Quests = 0, Calendar = 1, Events = 2, QuestLog = 3, Characters = 4, Contacts = 5, Players = 6, Shop = 7, DungeonMasterProfile = 8` [VERIFIED: `BoardPage.cs`]; Players, Shop and DungeonMasterProfile exist on `BoardType.OneShot` boards only (`BoardPagePolicy.AllowedOn`) and `QuestLog` needs `Quests` available (`BoardPagePolicy.IsAvailable`) [VERIFIED]
- `ShopItemAvailability.HasOpened`: `item.AvailableFrom == null || item.AvailableFrom <= boardNow` [VERIFIED: `ShopItemAvailability.cs:21-22`]

| # | Route (registry) | Kind | What the target action does (after the switch) | Refusal today | Predicate the resolver must apply (access level) |
|---|------------------|------|-----------------------------------------------|---------------|--------------------------------------------------|
| 1 | `Quest/Details` | Quest | `[BoardPage(Quests)]`; any viewer; null quest -> 404 only; DM-only sessions are open by URL (comment at `QuestController.cs:~359`) | page gate redirect if Quests is off | **Member**: Quests page available |
| 2 | `Quest/Edit` | Quest | `[Authorize(Policy="DungeonMasterOnly")]`; `Forbid()` unless `IsQuestOwner(currentUser, quest.DungeonMaster)` or role is `GroupRole.Admin` (`QuestController.cs:150-172`) | 302 to AccessDenied | **QuestOwnerOrAdmin**: DM-tier on board AND (`DungeonMasterId == viewer` OR Admin/SuperAdmin) |
| 3 | `Quest/Manage` | Quest | `DungeonMasterOnly`; renders for any DM-tier viewer, sets `ViewBag.IsAuthorized = isQuestDm \|\| isAdmin` (`:960-985`) | 302 to AccessDenied for a Player | **DungeonMasterTier**: DM-tier on board |
| 4 | `Quest/CreateFollowUp` | Quest | `DungeonMasterOnly`; `Forbid()` unless owner or Admin; not-finalized or already-followed-up redirect to Manage with a toast (`:995-1020`) | 302 to AccessDenied | **QuestOwnerOrAdmin** (the redirects land the viewer on a page they can open) |
| 5 | `QuestLog/Details` | Quest | `[Authorize] [BoardPage(QuestLog)]`; 404 unless `quest.HasBeenPlayed(boardClock.Today) && !quest.DungeonMasterSession` or `quest.IsClosed` (`QuestLogController.cs:47-49`, same at `:94-96`, `:122-124`) | 404 for an open or future quest that exists | **CompletedQuest**: QuestLog page available AND `(IsFinalized && FinalizedDate < start of boardToday && !DungeonMasterSession) \|\| IsClosed` (`HasBeenPlayed` is `DateOnly.FromDateTime(finalizedDate) <= boardToday.AddDays(-1)`, `QuestExtensions.cs:23-25`, i.e. `FinalizedDate < boardToday 00:00`) |
| 6 | `QuestLog/EditRecap` | Quest | same completed-quest 404, then `Challenge()` if anonymous | 404 as row 5 | **CompletedQuest** |
| 7 | `Events/Details` | Event | `[Authorize] [BoardPage(Events)]`; null -> 404 only; cancelled events render (pinned by `ResolvedCancelledEvent_...`) | page gate only | **Member**: Events page available. Recorded as checked, no record rule |
| 8 | `Events/Edit` | Event | `DungeonMasterOnly`; null -> 404 only | 302 to AccessDenied | **DungeonMasterTier** |
| 9 | `Characters/Details` | Character | `[Authorize] [BoardPage(Characters)]`; null -> 404 only; any status | page gate only | **Member**: Characters page available. Checked, no record rule |
| 10 | `Characters/Edit` | Character | `Forbid()` unless `character.OwnerId == currentUser.Id` or Admin (`CharactersController.cs:188-217`, `CanManageCharacterAsync`) | 302 to AccessDenied | **CharacterOwnerOrAdmin**: `OwnerId == viewer` OR Admin/SuperAdmin on board |
| 11 | `Contacts/Details` | Contact | 404 unless `IsVisibleTo`: `contact.IsRevealed`, or `CreatedByUserId == currentUserId`, else `includeHidden` (`ContactsController.cs:704-717`); `includeHidden` = DM-tier AND the session toggle `ShowHiddenContactsKey(groupId)` is 1 (`:691-699`; key is `$"ShowHiddenContacts_{groupId}"`) | 404 for a hidden contact the viewer did not create | **ContactVisible**: Contacts page available AND (`IsRevealed` OR `CreatedByUserId == viewer` OR DM-tier on board). Recommendation: do not model the per-session toggle (see Open Questions) |
| 12 | `Contacts/Edit` | Contact | `DungeonMasterOnly`; null -> 404 only (no `IsVisibleTo`) | 302 to AccessDenied | **DungeonMasterTier** |
| 13 | `Shop/Details` | ShopItem | `[Authorize] [BoardPage(Shop)]`; 404 when `item == null \|\| (IsUnreleased(item, boardNow) && !MayReviewUnreleasedItems)` where `IsUnreleased` = `Status == Draft \|\| Status == Denied \|\| !HasOpened` (`ShopController.cs:77-99`) | 404 for Draft/Denied/scheduled to a non-DM (the phase 99 debt) | **ShopItemVisible**: Shop page available AND (DM-tier on board OR (`Status` in `{Published, Archived}` AND `AvailableFrom == null \|\| AvailableFrom <= boardNow`)) |
| 14 | `ShopManagement/Edit` | ShopItem | class-level `DungeonMasterOnly`, `[BoardPage(Shop)]`; null -> 404 only | 302 to AccessDenied for a Player | **DungeonMasterTier** (Shop page available). Must not reuse row 13's member rule |
| 15 | `Series/Details` | EventSeries | `[Authorize] [BoardPage(Events)]`; null -> 404 only | page gate only | **Member**: Events page available. Checked |
| 16 | `ContactCategoryManagement/Edit` | ContactCategory | class `DungeonMasterOnly`, `[BoardPage(Contacts)]`; null -> 404 only | 302 to AccessDenied | **DungeonMasterTier** (Contacts page available) |
| 17 | `DungeonMaster/Profile` | BoardMember | `[BoardPage(DungeonMasterProfile)]`; 404 unless the target user is a member of the active board (`IsTargetInActiveGroupAsync`) | page gate; ambiguity rule: exactly one shared board | **Member**: DM-profile page available; shared-board set filtered by it, then the existing "exactly one" rule |
| 18 | `DungeonMaster/EditProfile` | BoardMember | `DungeonMasterOnly`; 404 unless target in group; `Forbid()` unless self or Admin (`DungeonMasterController.cs:56-80`) | 302 to AccessDenied | **AdminForOtherUser**: DM-profile page available AND viewer is Admin/SuperAdmin on that board (self-edit never reaches the resolver: the route reader needs an id and the self form omits it). Filter the shared-board set by this predicate before the "exactly one" rule |

Result: the kinds that need no record rule and are recorded as checked and left alone: Event (Details), Character (Details), EventSeries, and Quest (Details). Kinds with a refusal that must be applied: Quest (Edit, Manage, CreateFollowUp, QuestLog x2), Character (Edit), Contact (Details), Event (Edit), ShopItem (both), ContactCategory (Edit), BoardMember (EditProfile). The page-availability predicate applies to every row (F8).

Access-level vocabulary for the registry: `Member`, `DungeonMasterTier`, `QuestOwnerOrAdmin`, `CharacterOwnerOrAdmin`, `CompletedQuest`, `ContactVisible`, `ShopItemVisible`, `AdminForOtherUser`. Rows 1, 7, 9, 15, 17 share `Member`.

How "indistinguishable" is pinned today [VERIFIED: files read]: `CrossBoardOracleParityTests.AssertIndistinguishableAsync` compares status, full body and every header except `Date`, `Request-Id`, `traceparent` for an existing-but-foreign id versus `int.MaxValue`, one theory row per kind (`EntityFamilyRoutes`), plus a SuperAdmin pair and a "board unchanged after an unresolvable request" fact; `CrossBoardPickerSkipTests.cs:187,202` compare status and body for the picker path; `CrossBoardIgnoreQueryFiltersSeamTests` confines `IgnoreQueryFilters` to five files (`AllowedCallSites`, includes `CrossBoardLinkRepository.cs`); `CrossBoardLinkRegistryTests` pins the 18 routes and exclusions; `CrossBoardRouteCoverageTests` seeds an **Admin** viewer so every route switches. None pins hidden-versus-nonexistent for a board the viewer belongs to; that is the new pair set.

## Q4: The 403 path

- `ConfigureApplicationCookie` sets `options.AccessDeniedPath = "/Account/AccessDenied";` (`Program.cs:96-99`) [VERIFIED].
- `Forbid()` (21 calls) and a failed `DungeonMasterOnly`/`AdminOnly` policy go to the cookie handler's forbid path: a **302** to `/Account/AccessDenied?ReturnUrl=...` for navigations. The default cookie event answers an `X-Requested-With: XMLHttpRequest` request with a bare 403 instead of the redirect [ASSUMED: `CookieAuthenticationEvents.OnRedirectToAccessDenied` default; none of this repo's JS sends that header]. The existing facts rely on the 302 (`DungeonMasterControllerIntegrationTests.cs:152` comment: "Forbid() with Identity.Application scheme redirects to /Account/AccessDenied (302)").
- A bodiless `StatusCode(403)` does not occur in controllers today; the status-code re-execute would cover one.
- `AccountController.AccessDenied` is `[HttpGet] [AllowAnonymous]` returning `View()` with status 200 (`:43-48`). The view has the two cases to preserve: authenticated ("If you believe you should have access to this page, please contact an administrator.") and anonymous ("Please log in to access this page."), heading "You Don't Have Permission", with `ViewData["Title"] = "Access Denied"`.
- Recommendation: `AccessDenied` returns `View("ErrorPage", model)` with the 403 variant and **status 403** (Open Questions, decision 3). Keep the heading text exactly (fact at `AccountControllerIntegrationTests.cs:34-35`) and keep the absence of "Dungeon Master" in the copy.

## Q5: The 500 path

- Environments: Development relies on the developer exception page the host adds automatically; Production and Testing use `UseExceptionHandler` (`Program.cs:319-323`, `!IsDevelopment()`), and `WebApplicationFactoryBase` calls `builder.UseEnvironment("Testing")` (`WebApplicationFactoryBase.cs:44`) so integration tests exercise the handler [VERIFIED].
- `HttpContext.TraceIdentifier` under Kestrel is `<connectionId>:<requestIndex>` (spike: `0HNQ5HMN4U2M4:00000001`), not the W3C `Activity.TraceId` (`b21c65648ba8348350553d87cb08f8a1` in the same request); both are in the hosting log scope (`RequestId:` and `TraceId:`) [VERIFIED: spike]. The handler adds `Cache-Control: no-cache,no-store` itself (`ClearCacheHeaders`) [CITED: `ExceptionHandlerMiddlewareImpl.cs`].
- Consequence for D-12: with `IncludeScopes` off the operator cannot find the Reference in the journal (F2). Recommended fix, no new log line: turn scopes on in code, for example `builder.Logging.AddSimpleConsole(o => o.IncludeScopes = true);` [ASSUMED: API form; the effect was verified through the env var]. This changes the shape of every console line (adds one `=> ...` scope line), so it must be called out in the plan and a fact must pin it (`IOptionsMonitor<SimpleConsoleFormatterOptions>.CurrentValue.IncludeScopes` is true; a scope-capturing test provider proves `RequestId` equals the page's Reference). `CapturingLoggerProvider.BeginScope` returns null today (`Helpers/CapturingLoggerProvider.cs`), so the helper needs `ISupportExternalScope` for that fact. Alternative with the same effect and no code change: `Logging__Console__IncludeScopes=true` in `/etc/questboard/env` plus a line in `docs/server-setup.md`; that fixes production only, not the Docker self-host path.
- The exception handler re-executes `/Error`, so for a navigation the action returns the 500 view; the response status stays 500 because the handler sets it before re-executing and the view result does not override it [VERIFIED: spike `status=500`]. An over-limit upload (Kestrel `MaxRequestBodySize` 10 MB, `Program.cs:34`) surfaces as a handled `BadHttpRequestException` that the handler turns into a 500 with no `fail:` log line (spike with a 100-byte limit); the Reference would not be findable for that case. Low value, note it, do not build for it.
- Forcing a 500 in a fact: register a throwing stub in `ConfigureTestServices` (for example `IQuestService`) and request `/Quest/Details/1` with `Accept: text/html`; for the "layout cannot throw" fact, replace `IUserService`, `IBoardPageResolver` and `IGroupService` with throwing stubs and assert the page still renders.

## Q7: Log out on the error page

- Existing mechanism: `AccountController.Logout` is `[HttpPost] [ValidateAntiForgeryToken]` and calls `userService.SignOutAsync()` (`:173-179`); the nav dropdown posts `<form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">` (`_Layout.cshtml:256-260`) and the `FormTagHelper` injects the token [VERIFIED].
- Recommendation: add an optional `bool toLogin = false` parameter; `toLogin` redirects to `Account/Login`, the default keeps `Home/Index` (fact `Logout_Post_ShouldRedirectToHome` stays valid). The error page form posts a hidden `toLogin=true`. A boolean cannot carry a URL, so there is no open redirect, and logout stays POST-only so no cross-site GET or link can trigger it.
- Antiforgery on a re-executed GET and on a 500 page renders normally; it adds `Cache-Control: no-cache, no-store`, an `X-Frame-Options: SAMEORIGIN` header and, on the first token of a session, a `Set-Cookie` for the antiforgery cookie [ASSUMED: framework behaviour]. Two consequences for the parity facts: the hidden token value differs per response, and the first response carries a `Set-Cookie` that later ones do not. The parity helper must mask `__RequestVerificationToken` values and prime the cookie with a throwaway request (see Pitfalls).
- Anonymous viewers get "Log in": `asp-controller="Account" asp-action="Login" asp-route-returnUrl="@model.LoginReturnUrl"` only when the model set it (local URLs only). The Login POST sends `returnUrl` to the picker, which validates it with `Url.IsLocalUrl` (`GroupPickerController.cs:94-98`).

## Q8: Excluded paths

| Path / request | Behaviour | Action |
|----------------|-----------|--------|
| `/hangfire` (non-Testing, terminal middleware after authorization) | Hangfire writes its own responses; unauthorized users are redirected to login by the inline middleware (`Program.cs:344-362`) | Gate excludes `/hangfire` (operator tooling, never themed) |
| `/health` | `MapHealthChecks` writes a text body with `ContentType: text/plain` (`Program.cs:379-388`), so the middleware already skips it | Gate excludes `/health` as belt and braces |
| Static files | A missing file falls through to a bodiless 404; image/CSS/JS requests lack `text/html` in `Accept` | Rule handles it; a person typing a missing static URL gets the page |
| `/platform/*` | MVC area controllers with views; same behaviour as the main area | Same page and minimal layout (Q6) |
| JSON/fetch endpoints (`/markdown/preview`, `Events/PreviewSeries`, collision check, calendar feed) | Not navigations | Stay bare; feed 304 unaffected (`StatusCode(304)` is below 400) |
| `HEAD` | Re-executes to the same error endpoint | Error action has no verb attribute (F10) |
| 405, 409, 410 | Bodiless; 409 comes from `GroupSessionMiddleware` for a write with no active board | Generic fallback copy; 409 gets its own short sentence (below) |

## Q9: 429

Current: `options.OnRejected = async (context, cancellationToken) => { context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests; await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken); };` (`Program.cs:163-168`) [VERIFIED]. The three named policies are `forgot-password`, `set-password` (both POST forms, so browser navigations) and `calendar-feed` (calendar clients).

Do not render a view from inside `OnRejected`. Instead set the status and write the sentence only when the caller is **not** a document navigation; the re-execute that wraps the limiter produces the page. Spike result: navigation got `ERRCODE 429 method=GET status=429` (status kept, `Retry-After` header untouched), a plain fetch got the original plain-text sentence with the same 429. A re-executed `/Error/429` is not limited again because the error endpoint carries no limiter metadata and there is no global limiter [VERIFIED: spike + `Program.cs:122-169`].

Phase 100 touch: plan 100-07 adds a per-actor create limiter registration and may replace the Admin resend 429s with toasts; it does not edit `OnRejected` (see Overlap).

## Q12: Claude's-discretion answers

| Item | Recommendation | Evidence |
|------|----------------|----------|
| Log out mechanism | `Account/Logout` POST + token, optional `toLogin` boolean | Q7 |
| Per-code copy | Table below | Guidelines tone; D-02 no-leak |
| Layout under `/platform` and fallback | One dedicated minimal layout pair for every error page, including `/platform/*`; the page itself needs no board | `_Layout.cshtml` reads: `BoardPages.GetAsync()` (`:30`), two `AuthorizationService.AuthorizeAsync` role checks (`:52`, `:93`), `UserService.GetUserAsync(User)` (`:224`); the Platform layout shows `User.Identity?.Name` only but its brand/nav assume a SuperAdmin area. `_Layout.GroupPicker.cshtml` is the existing no-DB precedent |
| Excluded paths | Gate excludes `/hangfire` and `/health`; rule handles static files | Q8 |
| No active board | Works unchanged: the layout never reads the board, `/Error` is exempt in `GroupSessionMiddleware`, Switch board is a membership count | `GroupSessionMiddleware.cs:63-70` |

Recommended copy (headings and sentences; the 404 sentence is D-02's example and is identical for every viewer):

| Status | Heading | Sentence |
|--------|---------|----------|
| 404 | Page not found | It may have been removed, or it may belong to a board you're not viewing right now. |
| 403 | You Don't Have Permission | Authenticated: If you believe you should have access to this page, please contact an administrator. Anonymous: Please log in to access this page. (today's two cases, unchanged) |
| 400 | That request could not be processed | Something about that request was not valid. Go back and try again. |
| 500 | Something went wrong | We hit a problem on our side. Please try again in a moment. Reference: {TraceIdentifier} |
| 429 | Too many requests | Too many requests. Please try again later. (D-10, unchanged sentence) |
| 409 | Your board session has changed | Pick your board again and retry. (the write was refused, nothing was saved) |
| other | Something went wrong | That request could not be completed. Go back or return home. |

Also show the numeric status as a small muted line ("Error 404"). Buttons (starting point for Gate 1 drawings, roles from `.claude/ui-guidelines.md`): Go back `btn-secondary` (left, `fa-arrow-left`), Home `btn-primary` (`fa-home`), Log out `btn-info` (`fa-sign-out-alt`) or, for an anonymous viewer, Log in `btn-success` (`fa-sign-in-alt`). The exact role mapping is a Gate 1 decision. Switch board is a fourth, quieter action (`fa-arrows-rotate`, the nav's "Switch Group" icon). Card pattern: `modern-card` with `h1` title in the header, `<hr>` before the action row, `.empty-state` or `.refusal-line` for the sentence.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Re-executing an error page | A custom middleware that rewrites the path and re-runs the pipeline | `UseStatusCodePagesWithReExecute` + the gate flipping `IStatusCodePagesFeature.Enabled` | Handles endpoint/route-value reset, restores the path, exposes `IStatusCodeReExecuteFeature` |
| 500 page | A try/catch middleware | `UseExceptionHandler(ExceptionHandlerOptions)` | Clears the response, sets 500, no-cache headers, logs, rethrows when the handler fails |
| Local-URL validation | String checks on `returnUrl` or `Referer` | `Url.IsLocalUrl` for return URLs; for `Referer` parse with `Uri.TryCreate(..., UriKind.Absolute)`, compare authority with `Request.Host` ignoring scheme (the app trusts only `X-Forwarded-For`, `Program.cs:105-115`, so `Request.Scheme` behind Traefik may be http), then emit only `PathAndQuery` | The existing `CrossBoardRouteTarget.TryFromLocalUrl` shows the stricter shape the codebase prefers |
| Logout | A GET link or a JS fetch | The existing POST + `ValidateAntiForgeryToken` | CSRF-safe by construction |
| Page-availability rule | A second copy of "Shop is OneShot only" | `BoardPagePolicy.IsAvailable(type, disabled, page)` | One rule set for menus, gate and now the resolver |
| Shop visibility | A second copy of the Draft/window rule | Extract the member-visibility rule to Domain (next to `ShopItemAvailability`) and call it from `ShopController.Details` and the resolver's EF predicate; add a parity fact between the in-memory and EF forms | `ShopRepository.OnSale` is the precedent for an EF twin of a Domain rule |
| Board "today"/"now" | `DateTime.Now/UtcNow/Today` | `IBoardClock` | `AmbientClockSeamTests` bans ambient reads in listed files; add the resolver and repository to its `GuardedRelativePaths` |

**Key insight:** every hand-rolled variant here fails the same way: it works on the happy path and diverges on an edge (HEAD, POST, a fetch caller, DB down, a hidden id). The framework pieces and the existing predicates already carry those edges.

## Common Pitfalls

### Pitfall 1: The page is returned to fetch callers and breaks their error handling
**What goes wrong:** `event-details.js` and `quest-details.js` put `res.text()` into an `alert`; an HTML page would be dumped into it.
**Why it happens:** the status-code middleware has no idea who is asking.
**How to avoid:** the gate (Pattern 2). Pin with facts that send `Accept: */*` and `Sec-Fetch-Dest: empty` and assert an empty body for a bodiless 404/400/403.
**Warning signs:** a test that asserts `Contain("<html")` for a request without `Accept: text/html`.

### Pitfall 2: Parity facts fail because of the antiforgery token and cookie
**What goes wrong:** the themed 404 contains a Logout form; `__RequestVerificationToken` differs per response and the first response sets the antiforgery cookie, so "existing foreign id" versus "nonexistent id" never compare equal.
**How to avoid:** extend the comparison helper: send one throwaway authenticated navigation first (cookie primed), then mask the token value with a regex before comparing bodies; keep the header comparison otherwise unchanged.
**Warning signs:** the new parity theory fails on only the first row.

### Pitfall 3: The mobile twin is never served
**What goes wrong:** the view works on desktop, the phone gets the desktop view.
**Why it happens:** an explicit `~/Views/...` path skips the view-location expander.
**How to avoid:** name-based `View("ErrorPage", model)` from both controllers; layout by name (`_Layout.Error`). Fact: mobile UA gets the `.Mobile` markers (use the existing `MobileViewsTests` UA helper style).

### Pitfall 4: The error page throws on the error path
**What goes wrong:** DB down, an unhandled exception in `IUserService`, or a failed `DbContext` produces a bare 500 again.
**How to avoid:** minimal layout, no `_Toasts`, `CreateScopeForErrors`/`createScopeForStatusCodePages`, Switch-board flag in try/catch. Note `_ViewImports.cshtml` injects `IUserService`, `IBoardPageResolver`, `IAuthorizationService`, `IAntiforgery` into every view; construction does not hit the database, but do not use them in the error views.

### Pitfall 5: Existing facts silently cover the old behaviour
**What goes wrong:** the facts listed in F5 pass or fail for the wrong reason after the change. `ResolvedDraftShopItem_...` would keep asserting "the board must have switched".
**How to avoid:** rewrite to the same intent (record the new expectation: no switch, themed 404, board unchanged, no banner), never delete (phase 100's "no test deleted" check is the precedent). Add `CrossBoardLinkResolverService.cs` and `CrossBoardLinkRepository.cs` to `AmbientClockSeamTests.GuardedRelativePaths`.

### Pitfall 6: Picker skip leaks while the middleware does not
**What goes wrong:** only the middleware is fixed; `GroupPickerController.Index` still switches for a hidden record (F3).
**How to avoid:** both callers take `target.Access` from the registry/`TryFromLocalUrl`; extend `CrossBoardPickerSkipTests` with the hidden pair.

### Pitfall 7: Re-executed request leaks the original URL
**What goes wrong:** rendering `IStatusCodeReExecuteFeature.OriginalPath` (for a friendlier page or a login `returnUrl`) makes the 404 differ by id.
**How to avoid:** use it only for the anonymous Log-in `returnUrl` on non-404 pages (403/anonymous), never on 404, and never in the body of a 404.

### Pitfall 8: `appsettings.json`, `GroupSessionMiddleware` and line numbers
**What goes wrong:** editing `appsettings.json` (forbidden) to enable scopes; or planning against Program.cs line numbers that phase 100 shifts.
**How to avoid:** code-level logging option; anchor every Program.cs edit by content (`UseExceptionHandler`, `options.OnRejected`), not line.

### Pitfall 9: BadRequest bodies and Admin 429s stay plain text
**What goes wrong:** a reviewer expects every error themed. D-07 keeps bodies (F4, F9).
**How to avoid:** list them in the plan as known, not defects.

## Code Examples

Sketches only; this research edits no source.

### Resolver contract
```csharp
// Domain/Enums: closed list, one per row of the audit table.
public enum CrossBoardAccess { Member, DungeonMasterTier, QuestOwnerOrAdmin, CharacterOwnerOrAdmin,
                               CompletedQuest, ContactVisible, ShopItemVisible, AdminForOtherUser }

// ICrossBoardLinkResolver
Task<CrossBoardTarget?> ResolveAsync(CrossBoardLookupKind kind, CrossBoardAccess access, int id,
                                     int userId, bool viewerIsSuperAdmin, CancellationToken token = default);
```

### Repository predicate shape (shop, mirrors `OnSale`)
```csharp
// Board-level sets come from the Domain resolver; this stays one membership-pinned query.
dbContext.ShopItems.IgnoreQueryFilters()
    .Where(s => s.Id == id && pageAvailableGroupIds.Contains(s.GroupId))
    .Where(s => dmTierGroupIds.Contains(s.GroupId)
        || ((s.Status == (int)ItemStatus.Published || s.Status == (int)ItemStatus.Archived)
            && (s.AvailableFrom == null || s.AvailableFrom <= boardNow)))
    .Select(s => (int?)s.GroupId).FirstOrDefaultAsync(token);
```

### Same-site Referer (Go back fallback)
```csharp
// Returns a local path+query or null; never an absolute URL.
internal static string? SameSiteLocalReferer(HttpRequest r)
{
    var raw = r.Headers.Referer.ToString();
    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;
    if (!string.Equals(uri.Authority, r.Host.Value, StringComparison.OrdinalIgnoreCase)) return null;
    var local = uri.PathAndQuery;
    return local.StartsWith("/Error", StringComparison.OrdinalIgnoreCase) ? null : local;   // no loop back onto an error page
}
```

### Back button markup and script (no inline code)
```html
<a class="btn btn-secondary" data-history-back href="@Model.BackHref"><i class="fas fa-arrow-left me-2" aria-hidden="true"></i>Go back</a>
<!-- no Referer: render the button with the hidden attribute; the script reveals it when history exists -->
```
```js
// wwwroot/js/error-actions.js
document.querySelectorAll('[data-history-back]').forEach(function (el) {
    if (history.length > 1) { el.hidden = false; }
    el.addEventListener('click', function (event) {
        if (history.length > 1) { event.preventDefault(); history.back(); }
    });
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `UseStatusCodePagesWithReExecute` took only a path/query format | Overloads with `createScopeForStatusCodePages`; `ExceptionHandlerOptions.CreateScopeForErrors` | ASP.NET Core 6+ | Error page runs on a fresh DI scope, not the one that failed [CITED: release/10.0 source] |
| Endpoint metadata could not exempt an endpoint | `ISkipStatusCodePagesMetadata` is honoured by `StatusCodePagesMiddleware` | 7+ | Static opt-out for future API-only controllers [CITED: `StatusCodePagesMiddleware.cs`] |
| Developer page added by hand | `WebApplication` adds it automatically in Development | 6+ | No Development branch needed for D-11 [VERIFIED: spike] |

**Deprecated/outdated:** the default `/Error` convention from the project template (`ErrorViewModel` with `Activity.Current?.Id ?? HttpContext.TraceIdentifier`) is not used here; D-12 specifies `TraceIdentifier`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `View("~/Views/...")` bypasses the mobile view-location expander | Pitfall 3 / Anti-patterns | Low: the recommended name-based form works either way; a fact pins the twin |
| A2 | A failed `[ValidateAntiForgeryToken]` yields a bodiless 400 | Pattern 1 | A body would keep the bare text; the themed 400 would not appear for that case. Add one fact |
| A3 | `IAntiforgery` adds `Cache-Control: no-cache, no-store`, `X-Frame-Options` and a first-request `Set-Cookie` | Q7, Pitfall 2 | Parity facts might need less masking than planned |
| A4 | Default cookie `OnRedirectToAccessDenied` answers XHR with a bare 403 | Q4 | None of this repo's JS sends `X-Requested-With`; low |
| A5 | `builder.Logging.AddSimpleConsole(o => o.IncludeScopes = true)` is the right API form | Q5 | Effect verified through the env var; the exact call may need adjusting at implementation |
| A6 | Production console output has scopes off (CT 102 journal unreadable to `claude`) | F2 | If the operator already set `IncludeScopes`, D-12 works as written; the code-level setting is then redundant but harmless |
| A7 | Recommended button role mapping (Home primary, Log out info, Log in success) | Q12 | Gate 1 may choose differently; no code risk |
| A8 | v6.6.0 is the version for this phase | Plan shape | Operator decides; depends on what ships between 100 (v6.5.0) and 103 |

## Open Questions (RESOLVED)

1. **Navigation rule (D-09 refinement)** - RESOLVED: `Accept` contains `text/html` AND not `X-Requested-With: XMLHttpRequest` AND (`Sec-Fetch-Dest` absent OR `document`/`iframe`/`frame`). All seven JS callers audited; none change.
2. **D-12/D-13: the Reference is not in the log by default** - RESOLVED with a flagged deviation: turn console scopes on in code (one option, no new log line) and pin it with a fact; the plan must tell the operator this changes every console line's shape. Alternative (env var only) is recorded.
3. **Status of the AccessDenied page** - RESOLVED (recommendation): 403, because it is the "403 variant" and the status is what monitoring and `fetch().ok` should see. Update `AccountControllerIntegrationTests.AccessDenied_Get_...` from `OK` to `Forbidden`, keep its text assertions. If the operator prefers 200, nothing else changes.
4. **Does the page-gate redirect (F8) count as a "refusal" for D-05?** - RESOLVED (recommendation): yes, apply `BoardPagePolicy.IsAvailable` for the route's page in the same query; otherwise a switch plus a "page not used" notice still tells the member the id exists. One sentence in the plan makes the scope explicit so the operator can veto at Gate 1.
5. **Contacts hidden toggle** - RESOLVED (recommendation): model only "DM-tier on that board", not the per-session `ShowHiddenContacts_{groupId}` toggle. A DM-tier viewer is entitled to know hidden contacts exist (the toggle only filters their own list); they land on the 404 until they turn the toggle on. Modelling the toggle would put session state into the Domain resolver.
6. **SuperAdmin in the resolver** - RESOLVED: add `viewerIsSuperAdmin`; membership stays mandatory (D-01); update the class comments and keep `CrossBoardOracleParity_SuperAdmin_...`.
7. **Layout** - RESOLVED: dedicated minimal layout pair, used for `/platform` too (Q12).
8. **Where the views live** - RESOLVED: `Views/Shared/ErrorPage(.Mobile).cshtml`, name-based, called from `ErrorController` and `AccountController.AccessDenied`; remove `Views/Shared/AccessDenied.cshtml` and update the Phase 96 guard lists.
9. **Release version** - RESOLVED (provisional): feature work, minor bump; phase 100 is v6.5.0 per the plan; if phase 101 (email restyle) does not take a minor first, 103 is **v6.6.0**. The operator confirms at hand-off.
10. **Can server slices precede Gate 1?** - RESOLVED: yes, as in phase 100 (server fixes 06-10 before Gate 1 at 11-12). Behaviour (resolver, pipeline, gate, `Logout(toLogin)`, `OnRejected`, logging option) lands first with a structural view that uses only existing shared classes; `error.css`, `error.mobile.css`, the layouts' chrome and `error-actions.js` wait for Gate 1 (D-19).

Still open (for the operator or planner, none block planning):
- **413 / `BadHttpRequestException`** reaches the user as a themed 500 whose Reference is not in the log (low value; document, do not build).
- **Production log format** (A6): ask the operator to run `journalctl -u questboard -g "unhandled exception" -n 1` once to confirm whether scopes already show.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.115 (runtime 10.0.15) | none needed |
| SQL Server `localhost:1433` | live review runs (not tests; tests use InMemory) | yes, listening (operator's; never started or stopped by agents) | n/a | If unreachable: stop and tell the operator (project rule) |
| Node + Playwright | Gate 1/2 capture | yes | Node v24.19.0; Playwright via `~/qb-ui-review/phase-94/tools/node_modules` | none |
| Python / tesseract | gate hygiene, sheet building | yes | 3.14.4 / `/usr/bin/tesseract` | none |
| Mailpit (`qb-mailpit`, 1025/8025) | not needed (no mail in this phase) | not required | n/a | `run-app.mjs` forces email suppression regardless |
| Ports 8350 / 8351 | the phase's working app and frozen baseline | free now | n/a | Pick another free pair; 8341 is currently held by phase 100's baseline, never touch it |
| CT 102 journal | confirm log format | no (the `claude` account reads 0 lines) | n/a | Ask the operator for one `journalctl` line |

**Missing dependencies with no fallback:** none. **With fallback:** production journal (operator).

## Validation Architecture

`workflow.nyquist_validation` is true in `.planning/config.json`, so this section is required.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2, FluentAssertions 8.10.0, Microsoft.AspNetCore.Mvc.Testing 10.0.9, EF Core InMemory 10.0.9, NSubstitute 5.3.0 (unit) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`); no separate config for UnitTests |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ErrorPage\|FullyQualifiedName~CrossBoard"` |
| Full suite command | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project; a `0x80131506` CLR error is a flake, re-run once; never set GC env vars) |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-02/D-03/D-07 | 404 page for navigations: same body for missing and foreign id; Switch board only with 2+ memberships; per-code copy | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~ErrorPageRender"` | no, Wave 0 |
| D-09 | Fetch-shaped requests stay bare (404/400/403 empty body, 429 plain text) | integration | `... ~ErrorPageNavigationRule` | no, Wave 0 |
| D-09 | Rule truth table (Accept, X-Requested-With, Sec-Fetch-Dest, HEAD) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BrowserNavigation"` | no, Wave 0 |
| D-10 | 429 navigation themed and still 429; non-navigation plain sentence | integration | `... ~ErrorPageRateLimit` | no, Wave 0 (use `forgot-password`, limit 3) |
| D-11/D-12/D-14 | Testing env: throwing service gives themed 500 with `Reference`; no exception text; Reference equals the logged `RequestId` scope; scopes option is on | integration | `... ~ErrorPageServerError` | no, Wave 0 (extend `CapturingLoggerProvider` for scopes) |
| D-08 | AccessDenied is the 403 variant, both viewer cases, URL unchanged; cookie path unchanged | integration | `... ~AccountControllerIntegrationTests` (rewrite) | exists, rewrite |
| D-17 | `Logout(toLogin)` redirects to login; default still Home; GET is not allowed | integration | `... ~AccountControllerIntegrationTests` | exists, extend |
| Claude's-discretion | Page still renders when `IUserService`/`IBoardPageResolver`/`IGroupService` throw | integration | `... ~ErrorPageFallbackLayout` | no, Wave 0 |
| D-04/D-05 | Per route x role x record state: resolver switches iff the action would serve 2xx with the target active (agreement matrix) | integration | `... ~CrossBoardVisibilityMatrix` | no, Wave 0 |
| D-04/D-06 | Hidden record and nonexistent id are indistinguishable (status, masked body, headers), board unchanged, no banner | integration | `... ~CrossBoardOracleParity` (extend, rewrite the empty-body fact) | exists, extend |
| D-05 | Picker skip applies the same predicate | integration | `... ~CrossBoardPickerSkip` | exists, extend |
| D-05 | Registry carries an access level per route; 18 routes | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CrossBoardLinkRegistry"` | exists, extend |
| D-05 | Predicate shapes against InMemory (shop, contact, quest log, owners, page availability, SuperAdmin) | unit | `... ~CrossBoardLinkVisibility` | no, Wave 0 |
| D-04 | Shop member-visibility Domain rule and its EF twin agree | unit | `... ~ShopItemVisibility` | no, Wave 0 |
| Seams | `IgnoreQueryFilters` stays in the five listed files; resolver/repository added to the ambient-clock guard | unit | `... ~CrossBoardIgnoreQueryFiltersSeam` and `~AmbientClockSeam` | exist, extend |
| D-15 | Back button: same-site Referer renders a local relative `href`; off-site Referer renders none and the button is hidden; no inline script | integration | `... ~ErrorPageRender` | no, Wave 0 |
| D-18 | Phone UA gets the twin and the phone layout; twin has no `@media`; targets 44px; guard scanners (inline code, planning refs, literals, layer, token pairs) cover the new sheets and layouts | integration (static) | `... ~ErrorPagesGuard` and `~UiFoundationLayerGuard` | no / exists, extend lists |
| D-07 | `/Error/{code}` exempt in `GroupSessionMiddleware`; authenticated user with no active board sees the page | integration | `... ~GroupSessionMiddlewareIntegration` | exists, extend |
| D-19 | Gate 1 look check and phone mockup; live click-through of Back/Home/Log out | manual + Playwright tooling | `node ~/qb-ui-review/phase-103/tools/...` | no, Wave 0 (tooling copy) |

The **agreement matrix** is the drift guard for D-05: for each of the 18 routes and each of Player/DungeonMaster/Admin/SuperAdmin-member x record state (shop: Draft, Denied, scheduled, Published, Archived; contact: revealed, hidden, own; quest log: open, finalized-past, closed; quest/character: own, other's), seed the record on board B, first select B and record the real action's outcome (the oracle), then select A and follow the deep link and assert: switched and served if the oracle was 2xx, otherwise unchanged board, themed 404, and a body/header-equal match with a nonexistent id. It needs no copy of the business rules in the test, so a later change to a controller rule fails it.

### Sampling Rate
- **Per task commit:** the quick command above, narrowed to the touched class.
- **Per wave merge:** both projects' full suites, run separately.
- **Phase gate:** both full suites green plus the static guards, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Helpers/BrowserNavigationTests.cs` - rule truth table, same-site Referer helper
- [ ] `QuestBoard.UnitTests/Services/CrossBoardLinkVisibilityTests.cs` and `ShopItemVisibilityTests.cs` - predicates on InMemory
- [ ] `QuestBoard.IntegrationTests/Tests/ErrorPageRenderTests.cs`, `ErrorPageNavigationRuleTests.cs`, `ErrorPageRateLimitTests.cs`, `ErrorPageServerErrorTests.cs`, `ErrorPageFallbackLayoutTests.cs`
- [ ] `QuestBoard.IntegrationTests/Security/CrossBoardVisibilityMatrixTests.cs`
- [ ] `QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs` - converted-view list, sheet links, no inline code, no planning references, twin has no `@media`, scanner self-tests on seeded strings (the phase 98-100 pattern, e.g. `ShopConversionGuardTests`)
- [ ] Helpers: a parity helper that primes the antiforgery cookie and masks the token; `CapturingLoggerProvider` gains `ISupportExternalScope`; a factory variant with throwing stubs
- [ ] Update `UiFoundationLayerGuardTests` lists (`Layouts`, `LayeredFiles`, `NoLiteralFiles`): a new layout must either link a legacy sheet or the guard gets an explicit "no legacy sheet" allowance (the picker layout does link `legacy-pages.css`)
- [ ] Rewrite (same intent): `CrossBoardOracleParity_UnresolvableEntity_Returns404WithEmptyBody`, the two `CrossBoardAuthorizationBoundaryTests` facts, `AccessDenied_Get_...`, the `AccessDenied` entries in `AccountConversionGuardTests` (`:26`, `:54`, `:414`)
- [ ] Framework install: none

## Security Domain

`security_enforcement` is not set to false, so this section applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no change | Existing Identity cookie; Log in link only |
| V3 Session Management | yes (logout) | POST-only `Account/Logout` with antiforgery; redirect target is a fixed action, never a client URL |
| V4 Access Control | yes (core) | Visibility-aware resolver inside the one membership-pinned query; `AdminOnly`/`DungeonMasterOnly` remain the boundary on the landed page |
| V5 Input Validation | yes | `Url.IsLocalUrl` for return URLs; `Uri.TryCreate` plus authority check for `Referer`; status `code` route value constrained to int and clamped to 400-599 |
| V6 Cryptography | no | Antiforgery uses framework data protection; nothing hand-rolled |
| V7 Error handling / logging | yes | Generic message plus a unique id (`TraceIdentifier`), no exception text, type or path on the page; Production/Testing never show the developer page |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Existence oracle across boards (switch/banner/status/body/header differs for "hidden" vs "absent") | Information disclosure | One query, no branch; parity facts on status, masked body, headers, session board, TempData banner; both resolver callers covered |
| Error page echoes the failing URL or id | Information disclosure / XSS | Never render `OriginalPath`/route values; fixed copy; Razor encodes the only dynamic value (`Reference`) |
| Logout CSRF via GET or cross-site link | Tampering | POST + `ValidateAntiForgeryToken`; `toLogin` is a bool |
| Open redirect via `returnUrl` or `Referer` | Spoofing / Tampering | `Url.IsLocalUrl`; Referer reduced to its local `PathAndQuery` after an authority match |
| Stack trace or config disclosure on 500 | Information disclosure | Developer page only in Development (automatic); themed page shows only `Reference` |
| Error page cached with a per-session token | Information disclosure | `[ResponseCache(NoStore = true)]`; antiforgery already sets no-store |
| Rate-limit bypass by a themed page | Denial of service | `OnRejected` keeps 429 and `Retry-After`; the error endpoint is not itself limited but does no work |
| Fetch caller receives HTML where it expected text | Tampering (behavioural) | Navigation gate (Pattern 2) |

## Overlap with Phase 100

Phase 100 is executing in this checkout (STATE: plan 6 of 23; 01-05 done). 103 is built on its finished state. Every seam both phases touch, from grepping the 23 plans, `100-CONTEXT.md`, `100-RESEARCH.md`, `100-PATTERNS.md` and `100-UI-SPEC.md`:

| Seam | What phase 100 does | What 103 does | 103 must assume / do |
|------|--------------------|---------------|----------------------|
| `QuestBoard.Service/Program.cs` | 100-07 edits the `Resend` named-client block (`ResendBaseUrl` only in Development) and may add a per-actor create-limiter registration near the existing `PartitionedRateLimiter<int>` singleton (`Program.cs:171-187`); 100-20 only passes `--env` to run-app | Edits the exception-handler line, adds status-code pages and the gate, edits `options.OnRejected`, may add the logging option | Different hunks, no logical conflict. **Anchor by content, never by line number**; re-read the file at plan time. Keep the one `https://api.resend.com/` literal 100-07 greps for |
| Rate limiter / `OnRejected` | Does **not** edit `OnRejected` (no plan mentions it). 100-07 reorders the per-target email limiter in `AdminController`, and on operator answer O-4 may turn the Admin resend 429s (which write a body, `AdminController.cs:265,386`) into toast redirects; tests in `AdminControllerRateLimitIntegrationTests` change | `OnRejected` stops writing the sentence for navigations | Either outcome is compatible: those responses carry bodies (or become redirects) and are not touched by D-07. Do not edit `AdminControllerRateLimitIntegrationTests` beyond what 103 needs |
| `AccessDenied` / `AccountController` | No plan touches `AccessDenied`, `AccountController` or `Views/Shared/AccessDenied.cshtml` (grep: zero hits) | Owns the 403 variant, `Logout(toLogin)` | Free to change; update the Phase 96 account guard lists |
| `GroupSessionMiddleware` | Referenced in research only (read, not changed) | Reads `/Error` exemption | Unchanged; optionally derive the controller name with `ControllerNameOf<ErrorController>()` |
| `CrossBoardDeepLinkMiddleware`, `CrossBoardLinkRegistry`, resolver, repository, `CrossBoard*` tests | No plan mentions `CrossBoard` (grep: zero hits) | Owns the visibility-aware resolver and the rewritten facts | No coordination needed; 100-04's "cross-board delete pin" is an Admin/Platform rule, not the resolver |
| `_Layout.cshtml`, `_Layout.Mobile.cshtml` | Not edited by any plan | Not edited (error pages use their own layout) | Unchanged |
| Platform layouts (`_Layout.Platform*.cshtml`), `shell.css`, `shell.mobile.css`, `UiFoundationDesktopShellGuardTests`, `UiFoundationMobileShellGuardTests` | 100-20 verifies them and edits only if a probe fails | Not used by 103 | If 103's new layouts link `shell.css`/`shell.mobile.css`, do so after 100-20 is merged so the final sheets are the ones referenced; do not edit them |
| `components.css` | 100-13 adds the shared admin classes; 100-21 closes with counts | Reuses `.empty-state`, `.refusal-line`, cards and buttons only | Put 103's rules in new `error.css`/`error.mobile.css`, **not** in `components.css`, to avoid merge conflicts and the layered-sheet guards |
| `UiFoundationLayerGuardTests` (`Layouts`, `LayeredFiles`, `NoLiteralFiles`) | 100-13, 100-16, 100-21 append admin/platform sheets and badge partials | Appends `error.css`, `error.mobile.css`, and the new layouts | Append to the post-100 lists; the scanners already pick up every new `*.css`, `*.js`, `*.cshtml` by directory walk |
| `BoardPageStaticGuardTests`, `MobileViewsTests`, `KeyboardScriptGuardTests` | 100-13/14/15/16/19 edit them | Probably untouched | Re-read before planning any change |
| `AdminPlatformConversionGuardTests` | Phase 100's guard (view/sheet/script lists, `PhaseSourceFiles`) | None | Do not edit; `Views/Shared/*` additions by 103 are outside its lists |
| `Views/Shared/*` partials | 100 adds `_QuestStatusBadge`, `_RoleBadge`, `_BoardTypeBadge` | Adds `ErrorPage*`, `_Layout.Error*` | Name collisions: none |
| `wwwroot/js` | 100-19 adds `platform-members.js`; `confirm-submit.js` already exists | Adds `error-actions.js` | Independent |
| Review tooling and ports | `~/qb-ui-review/phase-100`, ports 8340/8341 (8341 up now) | New scratch root `~/qb-ui-review/phase-103`, ports 8350/8351 | Never stop or reuse 8340/8341 |
| Release | 100 hands over as **v6.5.0** (100-23) | Next minor | See Open Questions 9 |

Post-100 state 103 should assume: Admin/Platform views and sheets converted; `Program.cs` has grown by 100-07's registrations; `AdminController` actions that used `fetch`+DELETE are POST forms with toasts (so the JS fetch audit above is the post-100 list, minus any DELETE callers 100-08 removed; re-grep `wwwroot/js` at plan time: `quest-manage.js` currently has two DELETE `fetch` callers that 100-08 may rewrite); no change to the cross-board code, the account views or the layouts.

## Plan shape, fixtures and tooling (Q11)

Precedent (phase 100's 23 plans, 98 and 99 similarly): opening operator checkpoint (SQL up, fixtures approved, ports, scratch root); fixtures through the app's own forms only; BEFORE capture and a frozen baseline copy before any source change; a server-rules audit that reproduces each gap as a skipped failing fact; server fixes in slices; Gate 1 (mockups beside BEFORE and the live converted look reference, desktop and phone, measured, hygiene-checked local sheet, operator answers recorded); UI slices; closing guards; closing sweep; Gate 2 sheet; release hand-off by the operator's PR. Tools live in `~/qb-ui-review/phase-100/tools` (`run-app.mjs`, `login.mjs`, `lookcheck.mjs`, `mockup-shoot.mjs`, `serve-v1.mjs`, `gate-hygiene.py`, `history-click-99-19.mjs`); `run-app.mjs` runs Development with `EmailSettings__SuppressSending=true` and SMTP `127.0.0.1:1025` forced and refuses any `--env` that changes that [VERIFIED: file header]. Copy the toolset to `~/qb-ui-review/phase-103/tools` and re-point the root and ports, as phase 100 did from phase 99.

Suggested slices:
1. Opening checkpoint and environment (SQL on 1433 confirmed by the operator, ports, fixtures approval).
2. p103 fixtures; BEFORE capture of today's bare 404/403/429/500 and of the leak (Draft deep link); frozen baseline on 8351.
3. Server-rules audit: reproduction facts (skipped until fixed) per row of the audit table; pins for what must keep working.
4. Resolver slice: `CrossBoardAccess`, registry, resolver, repository predicates, both callers, Domain shop-visibility rule, rewritten and new facts, guard-list updates.
5. Error foundation slice: pipeline, gate, `BrowserNavigation`, `ErrorController`, structural `ErrorPage` view with shared classes only, `Logout(toLogin)`, `AccessDenied` as 403 variant, `OnRejected`, logging option, helper/factory updates, behaviour facts.
6. Gate 1 package (mockups: 404 with and without Switch board, 500 with Reference, 403 authenticated and anonymous, 429, generic; desktop and phone) and hold Gate 1.
7. UI slice: `_Layout.Error(.Mobile)`, `error.css`, `error.mobile.css`, `ErrorPage.Mobile.cshtml`, `error-actions.js`, guards.
8. Closing guards, closing sweep (both UAs, 320 and 1440, focus order, contrast, history-click probe, live proof of the leak closed on p103 data), Gate 2, release hand-off.

Fixtures (fantasy names, `@example.invalid`, all through the app's forms, no SQL writes; the local DB holds real member data): boards `P103 Audit One-Shot` (active), `P103 Audit One-Shot Two` (target; Shop is OneShot-only), `P103 Audit Campaign` (viewer is not a member, for the non-member parity). Accounts `qb-audit-p103player` (Player on One and Two), `qb-audit-p103dm` (DM on Two, Player on One), `qb-audit-p103single` (One only, so no Switch board button), `qb-audit-p103admin`; reuse the existing SuperAdmin `qb-audit-platform@example.invalid` (ask the operator whether to add it as a Player of the two boards, as 100 did). Records on `Two`: a Draft shop item (a Rare item created through `ShopManagement/Create`, which now stamps `GroupId`, `ShopManagementController.cs:81-83`), a scheduled item (future `AvailableFrom`), a Published item, a hidden contact created by the DM, an open quest and a finalized-past quest. Ports **8350** (working tree) and **8351** (frozen baseline); both free now, 8340/8341 belong to phase 100.

Look reference for Gate 1 (D-19): the live converted `Account/Login` and `Account/AccessDenied` cards (`account.css`, phase 96) and a phase 98/99 page, compared with `lookcheck.mjs`-style computed-style equality; the senior-frontend skill applies to the mockup work. The phone mockup must come from a mobile User-Agent with touch (the layout is chosen by User-Agent, not width: `MobileDetectionMiddleware` keywords `Mobi, Android, iPhone, iPad, Windows Phone, BlackBerry`).

Housekeeping the planner must carry from CLAUDE.md and memory: no GSD IDs in source comments or strings; EF only in `QuestBoard.Repository`; never edit `appsettings.json`; never commit to `main`; stay on `milestone/v9-rolling-improvements`; writes to `Events`/`Quests` untouched (this phase adds none); `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` untouched. Memory says push the milestone branch after each green wave; phase 100's environment contract says executors never push: the orchestrator decides which applies.

## Project Constraints (from CLAUDE.md)

- Never commit directly to `main`; work on a feature branch (current: `milestone/v9-rolling-improvements`; do not switch).
- Local database is `localhost:1433`; never install, start or provision it; tests use InMemory. Do not "fix" `Cannot generate SSPI context` by deleting `Trusted_Connection=true`.
- `dotnet build` / `dotnet test` / `dotnet run --project QuestBoard.Service`; locked-file failures mean the debugger is running; on Linux never set `DOTNET_GCHeapHardLimit` or other GC variables; `0x80131506` is a flake, re-run once.
- No GSD planning references (requirement IDs, phase/plan numbers, review IDs, planning file names) in comments, XML docs or string literals; commit messages may carry them.
- Read `.claude/ui-guidelines.md` before any Razor view: card pattern, tokens only (no colour literals outside `tokens.css`), no `!important`, layer order `vendor, tokens, base, components, pages, utilities`, buttons by role with `me-2` icons, 44px targets on the phone, `.Mobile.cshtml` twins with no `@media` and their own `*.mobile.css`, no inline `style`/`<script>`/handlers, scripts in `wwwroot/js`, date helpers `Html.LocalTime`/`Html.WallClock`.
- EF packages only in `QuestBoard.Repository`; writes to `Events`/`Quests` through the change tracker; `IBoardClock` for "today", never `DateTime.Now/UtcNow/Today`.
- Time and board clock rules in `.claude/architecture.md`: wall-clock values never converted.
- `.claude/skills/` is empty; the user-level `senior-frontend` skill covers the mockup and CSS work.

## Sources

### Primary (HIGH confidence)
- Repository files read this session (cited inline with paths and lines): `Program.cs`, `GroupSessionMiddleware.cs`, `CrossBoardDeepLinkMiddleware.cs`, `CrossBoardLinkRegistry.cs`, `CrossBoardRouteTarget.cs`, `CrossBoardLinkResolverService.cs`, `CrossBoardLinkRepository.cs`, `ICrossBoardLinkResolver.cs`, `ICrossBoardLinkRepository.cs`, `GroupPickerController.cs`, `AccountController.cs`, `QuestController.cs`, `QuestLogController.cs`, `EventsController.cs`, `SeriesController.cs`, `CharactersController.cs`, `ContactsController.cs`, `ContactCategoryManagementController.cs`, `DungeonMasterController.cs`, `ShopController.cs`, `ShopManagementController.cs`, `BoardPageGateFilter.cs`, `BoardPagePolicy.cs`, `BoardPageResolver.cs`, `ShopItemAvailability.cs`, `ShopRepository.cs`, `QuestExtensions.cs`, `BoardClock.cs`, all four layouts, `_ViewImports.cshtml`, `_ViewStart.cshtml`, `AccessDenied.cshtml`, `MobileViewLocationExpander.cs`, `MobileDetectionMiddleware.cs`, every `wwwroot/js` fetch caller, the integration/unit test files named above, phase 99 `deferred-items.md`, phase 100 plans/context/research.
- Spikes (scratchpad, throwaway app on net10.0, Production and Development environments): status-code re-execute behaviour, `IStatusCodePagesFeature.Enabled` gate, 429 through `OnRejected`, HEAD/POST re-execute, exception handler log line with and without console scopes, `TraceIdentifier` versus `Activity.TraceId`, developer page in Development, 413 handling.
- github.com/dotnet/aspnetcore release/10.0 `StatusCodePagesMiddleware.cs`, `StatusCodePagesExtensions.cs`, `ExceptionHandlerMiddlewareImpl.cs`, `ExceptionHandlerOptions.cs` (fetched this session).

### Secondary (MEDIUM confidence)
- developer.mozilla.org: `Sec-Fetch-Dest` header values and `Accept` navigation example.

### Tertiary (LOW confidence)
- The behaviours listed in the Assumptions Log (A1-A8).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; built-in middleware verified by spike and source.
- Architecture: HIGH - pipeline, gate, 429 and exception paths exercised; layout and twin mechanics read from the repo.
- Resolver audit: HIGH - every one of the 18 target actions and their filters read; predicate shapes grounded in existing rules.
- Pitfalls: MEDIUM-HIGH - parity/antiforgery interaction is reasoned (A3), not yet run against the real views.
- Production logging: MEDIUM - default behaviour verified, production journal unreadable.

**Research date:** 2026-11-18
**Valid until:** 2026-12-18 (stable framework behaviour; the Overlap section goes stale as phase 100 lands, so re-run its greps at plan time)
