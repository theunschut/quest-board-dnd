---
phase: 103-themed-error-pages
plan: 05
subsystem: error-pages
tags: [error-pages, logout, referer, switch-board, antiforgery, integration-tests, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: error pipeline, ErrorController, structural ErrorPage and _Layout.Error (103-04)
provides:
  - Log out (POST with token and toLogin=true) and Log in actions on every error page
  - AccountController.Logout(bool toLogin = false) ending on the login page
  - BrowserNavigation.SameSiteLocalReferer and the Go back anchor
  - Switch board for a viewer of two or more boards on a 404, failing closed
  - Log in return URL rule, HEAD handling, no-store, and the no-active-board and 409 facts
  - ErrorPageComparison, the shared helper for every themed-body comparison
affects: [103-06, 103-07, 103-08, 103-09, 103-10, 103-11, 103-12]

actuals:
  tokens: 9800
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "A redirect target chosen by a bool flag rather than a URL, so nothing a caller sends can steer it"
    - "Resolving a side-read service from RequestServices inside the try, so a failing registration cannot fail the page"
    - "A DispatchProxy decorator that fails one method of a real service, for fail-closed facts"

key-files:
  created:
    - QuestBoard.IntegrationTests/Helpers/ErrorPageComparison.cs
  modified:
    - QuestBoard.Service/Helpers/BrowserNavigation.cs
    - QuestBoard.Service/Controllers/ErrorController.cs
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/ViewModels/ErrorViewModels/ErrorPageViewModel.cs
    - QuestBoard.Service/Views/Shared/ErrorPage.cshtml
    - QuestBoard.UnitTests/Helpers/BrowserNavigationTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPageRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPageNavigationRuleTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs

key-decisions:
  - "Switch board reads IGroupService from HttpContext.RequestServices inside the try, not through the constructor: the same service backs the page-gate filter, so constructor injection would have made a failing registration fail the page instead of the button"
  - "A HEAD gets the bare status in the controller: the test server does not strip a HEAD body, and a body on HEAD is wrong in any host"
  - "ErrorController takes ILogger only; a failed membership read is logged at warning level and never reaches the page"

requirements-completed: [D-03, D-07, D-15, D-16, D-17]

duration: 75min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 05: Error page actions Summary

**Every error page now offers Go back, Home and Log out or Log in as real links, buttons and a POST form, a member of several boards gets Switch board on a 404, and nothing on the page can redirect off-site or sign anyone out by GET.**

## Actions and roles as built

Desktop order in `ErrorPage.cshtml`: Go back at the left, then a right group (`ms-auto`) of Switch board (when shown), Log out or Log in, Home.

| Action | Markup | Role |
|--------|--------|------|
| Go back | anchor, `data-history-back`, `fas fa-arrow-left me-2` | `btn-secondary` |
| Switch board | anchor to `/groups/pick`, `fas fa-arrows-rotate me-2` | `btn-info` |
| Log out (signed in) | form posting to `Account/Logout` with the rendered antiforgery token and hidden `toLogin=true`, `fas fa-sign-out-alt me-2` | `btn-secondary` |
| Log in (anonymous) | anchor to the Login action, `fas fa-sign-in-alt me-2` | `btn-secondary` |
| Home | anchor to `/`, `fas fa-home me-2` | the one `btn-primary` |

Every icon carries `me-2` and `aria-hidden`. No script, no on-event attribute and no `error-actions.js` yet; Go back works through its Referer link, and the later script only reveals it when history exists. The roles are the recommended mapping; Gate 1 item 3 may change them.

## Log out

`AccountController.Logout(bool toLogin = false)` redirects to the Login action when the flag is true and to Home/Index otherwise. A flag rather than a URL keeps the target fixed. It stays POST with `ValidateAntiForgeryToken`; a GET is not routed (answers 405) and never signs anyone out (fact). `Logout_Post_ShouldRedirectToHome` is unchanged and green; a second fact pins the redirect to `/` without the flag and one pins `/Account/Login` with it.

## Go back and the Referer

`BrowserNavigation.SameSiteLocalReferer(HttpRequest)`: the Referer must parse as an absolute http or https address whose authority (host and port, scheme ignored) equals the request host; only path and query come back; a path that starts with a second slash or a backslash is refused (it would read as protocol-relative); a path equal to or under `/Error` is refused after unescaping (`/%45rror/404` too); `/Errors/list` is allowed. A 25-row unit theory covers other host, look-alike host, userinfo trick, other port, protocol-relative, relative, scheme `javascript:`, garbage, empty and none. With no usable Referer the anchor carries `hidden` and no `href`; an off-site value never appears in the body (fact).

## Switch board

Shown only when the status is 404, the viewer is authenticated, and `IGroupService.GetGroupsForUserAsync` for the viewer's own id returns two or more boards. The read, including resolving the service, sits inside a try/catch that answers no button on any exception and logs a warning. It never looks at the requested record: two missing ids with a two-board viewer produce identical masked bodies and header sets. Facts: two boards on a 404 shows it; one board, anonymous, and codes 400, 403, 409, 418, 429 and 500 never show it; a decorator that fails only the membership read still gives the 404 page with Log out and no button.

## Log in return URL (and the GET-only discretion)

The Log in link carries `returnUrl` only when all four hold: anonymous viewer, status not 404, original request was a GET, and `Url.IsLocalUrl` accepts the original path base, path and query from the re-execute feature. The GET-only clause is this plan's discretion: the address of a POST is not a page to return to. A direct visit to `/Error/{code}` has no original request and adds none. Facts: a 404 gets none and the path is not echoed; an anonymous GET of `/Account/Logout?x=1` (405) gets `returnUrl=%2FAccount%2FLogout%3Fx%3D1`; a POST that answers 405 gets none; a direct visit gets none; a signed-in viewer gets no Log in link.

## Request shapes

- HEAD: the controller answers the bare status with no body (a GET-only MVC action answers 405 to HEAD in this host, so the facts pin a missing route to 404 and a GET-only record page to a 4xx, both with no body).
- A re-executed POST to a missing route keeps 404 and gets the page.
- Every themed page answers `Cache-Control: no-store` with `Pragma: no-cache` (facts for a 404, a 500 and a 403).
- An authenticated viewer with no active board asking `/Account/NoSuchAction` sees the 404 page, not a redirect to the picker.
- A navigation POST to `/quests` with no active board gets the themed 409 ("Your board session has changed", `Error 409`).

## The comparison helper

`ErrorPageComparison.PrimeAsync(clients)` sends two throwaway navigations per client (the error endpoint for the antiforgery cookie, a missing board record for the session cookie, which otherwise appears only on the first response of a pair); `MaskToken` replaces every `__RequestVerificationToken` value; `AssertIndistinguishableAsync` compares status, Location, masked body and every header except Date, Request-Id and traceparent; `AssertSameBody` compares two body strings. `ErrorPageRenderTests`, `CrossBoardHiddenRecordTests` and `CrossBoardOracleParityTests` compare through it (the hidden-record reproduction facts stay skipped). The test factory's antiforgery decorator still validates everything, so a missing-token refusal cannot be proven here; the BEFORE bad-request row proved it live.

## Verification

- Unit: 1679 passed. Integration: 3149 passed, 24 skipped, 0 failed. The planning-reference scan now also covers `AccountController.cs`, the helper and the two edited integration test files.
- Tracer gate (interactive, no operator stop per the operator rule): satisfied by the green automated verify of Task 1 plus the live check below, run after the expansion task.
- Live on 8350 (restarted from the working tree, then stopped), both User-Agents, `after/103-05/actions.json` (script `tools/actions-103-05.mjs`): `p103single` on a missing id sees no Switch board and has the Log out form; `p103player` sees Switch board (`btn btn-info`), which opens `/groups/pick` listing both boards; opened from `/quests`, Go back's href is `/quests`, not hidden; opened directly it is hidden with no href; posting Log out from the error page in a throwaway context ends on `/Account/Login` and the next request to `/Account/Profile` lands on the login page; the stored state still signs in afterwards (no re-login needed); anonymous 404 Log in href is `/Account/Login`, anonymous 405 from a GET carries the local returnUrl.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The comparison helper was created in Task 1, not Task 2**
- **Found during:** Task 1
- **Issue:** adding the Log out form put a per-session token in every signed-in error body, so the existing identical-bodies facts (render, direct, oracle parity) failed the moment Task 1 landed.
- **Fix:** `ErrorPageComparison` and its routing into the render, hidden-record and oracle-parity facts moved into the Task 1 commit so every commit is green. `CrossBoardOracleParityTests` also needed the helper (the plan listed only the hidden-record file).
- **Commit:** 54d59fcd

**2. [Rule 1 - Bug] The priming had to start the session cookie as well**
- **Found during:** Task 2
- **Issue:** a header-equality fact still failed because the first board-page request sets the session cookie and the second does not; priming only the error endpoint left that difference.
- **Fix:** `PrimeAsync` also makes one throwaway board-page navigation to a missing id.
- **Commit:** 5267ac20

**3. [Rule 3 - Blocking] The failing-service stub cannot replace IGroupService wholesale**
- **Found during:** Task 2
- **Issue:** the page-gate filter and other pipeline parts resolve the same service, so a registration that throws made every signed-in request fail, not just the button. For the same reason the controller resolves it from `RequestServices` inside the try rather than by constructor.
- **Fix:** the derived factory wraps the real service in a `DispatchProxy` that fails only `GetGroupsForUserAsync`.
- **Commit:** 5267ac20

**4. [Rule 1 - Bug] HEAD body**
- **Found during:** Task 2
- **Issue:** the test host does not strip the body of a HEAD response to a rendered view, so a HEAD got the page.
- **Fix:** the controller returns the bare status for HEAD. Also, an MVC `[HttpGet]` record page answers 405 to HEAD, so the plan's "HEAD of a missing id" fact is pinned on a missing route (404) plus a record page (any 4xx, no body).
- **Commit:** 5267ac20

**5. [Process] `AccountController.cs` is CRLF**
- A scripted edit normalised it to LF; it was restored to CRLF before the commit so the diff is the 8 changed lines.

Tests were written first and confirmed failing for the Log out facts (3 failures) and the actions facts (12 failures) before the production code.

## Known Stubs

None. Go back's hidden anchor is intentional: `error-actions.js` (a later plan) reveals it when history exists.

## Threat Flags

None beyond the plan's threat model: the Referer is reduced to a local path, the return URL passes `Url.IsLocalUrl`, Logout stays POST with antiforgery and a bool flag, the page is no-store, and the membership read fails closed.

## Left running

The frozen baseline on 8351 only. 8350 is stopped. SQL Server was not touched.

## Self-Check: PASSED

- Present: `ErrorPageComparison.cs`, `after/103-05/actions.json`, this file.
- Commits `54d59fcd` and `5267ac20` exist.
