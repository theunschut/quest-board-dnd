---
phase: 103-themed-error-pages
plan: 06
subsystem: error-pages
tags: [error-pages, exception-handler, trace-identifier, rate-limiter, logging-scopes, integration-tests, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: error pipeline, ErrorController, structural ErrorPage, action row (103-04, 103-05)
provides:
  - Exception handler on a real endpoint in a fresh scope (ExceptionHandlerOptions with CreateScopeForErrors)
  - The 500 page with a Reference equal to HttpContext.TraceIdentifier and nothing else from the exception
  - Themed 429 for a browser navigation, plain sentence for every other caller
  - CapturingLoggerProvider with external scope support and a parallel scoped-entry list
  - ThrowingServicesWebApplicationFactory family (throwing stubs that record the trace identifier)
  - Setup-guide line for Logging__Console__IncludeScopes=true (logScopes env branch)
affects: [103-07, 103-08, 103-09, 103-10, 103-11, 103-12]

actuals:
  tokens: 7800
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "A DispatchProxy stub that records the request's trace identifier through IHttpContextAccessor and then throws a sentinel exception"
    - "A log provider that implements ISupportExternalScope and keeps scope key-value pairs beside the rendered record without changing its string form"

key-files:
  created:
    - QuestBoard.IntegrationTests/Helpers/ThrowingServicesWebApplicationFactory.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPageServerErrorTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPageRateLimitTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPageFallbackLayoutTests.cs
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.Service/Controllers/ErrorController.cs
    - QuestBoard.Service/ViewModels/ErrorViewModels/ErrorPageViewModel.cs
    - QuestBoard.Service/Views/Shared/ErrorPage.cshtml
    - QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs
    - docs/server-setup.md

key-decisions:
  - "logScopes branch env ran: no code turns console scopes on; the setup guide names Logging__Console__IncludeScopes=true in its environment-file section and for the Docker compose path"
  - "The Reference is HttpContext.TraceIdentifier, the value the hosting layer logs as RequestId, set on the 500 model only"
  - "The Reference sits in a plain span (class error-reference) with the hint as form-text: styling it is left to the visual gate"

requirements-completed: [D-10, D-11, D-12, D-13, D-14]

duration: 50min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 06: Server error and rate limit Summary

**An unhandled exception now ends on the themed 500 with a quotable Reference that equals the request's trace identifier and the RequestId in the service log, a rate-limited navigation sees the themed 429 with the unchanged sentence, and the page renders even when the user, board-page and group services all throw.**

## What was built

- **Handler:** inside the not-Development block, `app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/Error", CreateScopeForErrors = true })`, with a plain comment that a failed save leaves the request's database context unusable. The old string form pointed at a path no endpoint served; `GroupSessionMiddleware`'s /Error exemption covers it unchanged. Development keeps the host's developer exception page (nothing here adds a Development branch); status-code pages stay themed in every environment. A guard fact pins both placements and rejects the bare string form.
- **Reference rule:** `ErrorController` passes `HttpContext.TraceIdentifier` only when the status is 500; `ErrorPageViewModel.Reference` is set only for 500. Nothing is read from the exception feature. The page renders `Reference: <span class="error-reference">value</span>` and the hint "Quote this reference if you ask for help." (Razor encodes the value). It appears on no other variant (fact). A non-navigation keeps the bare empty 500.
- **429:** `OnRejected` always sets 429 and writes "Too many requests. Please try again later." only when `BrowserNavigation.IsDocumentNavigation` is false; the status-code re-execute renders the themed page for a navigation. The sentence stays written once in Program.cs. No policy, limit or per-target limiter changed; no Retry-After handling was added (the limiter never set one).
- **Capture helper:** `CapturingLoggerProvider` implements `ISupportExternalScope`; `Entries` holds category, level, rendered message and the scope key-value pairs of each record. `Records` and its strings are untouched; the full suites pass.
- **Throwing factory:** `ThrowingServicesWebApplicationFactory(params Type[])` registers a DispatchProxy stub per service type that records `TraceIdentifier` through `IHttpContextAccessor`, then throws `SentinelFailureException` with a sentinel message. Concrete fixtures: `ThrowingQuestServiceFactory`, `ThrowingLayoutHelpersFactory` (user, board-page, group and quest services), `ThrowingQuestServiceWithConsoleScopesFactory` (adds the console key).

## logScopes branch that ran: env

The operator's answer was `env` and the setting is already live on CT 102 through a systemd drop-in. As the plan specifies for `env`, no code changes: `Program.cs` configures no console option (a fact asserts `IncludeScopes`, `AddConsole` and `SimpleConsole` are absent). `docs/server-setup.md` now lists `Logging__Console__IncludeScopes=true` in the environment-file block, explains in one paragraph that it puts the request id in each service log line so `journalctl -u questboard | grep <reference>` finds the failure, and names the Docker self-host path (the `environment` list of `docker-compose.yml`, then recreate the container).

Facts that pin it (`ErrorPageLogScopeSettingTests`): the console options report scopes on when the key is set (read through the obsolete `ConsoleLoggerOptions.IncludeScopes`, warning silenced locally with a comment, because the key binds there in this framework); they report scopes off without it; the guide's environment-file section contains the setting line and the compose path; Program.cs adds no logging option. In every branch the Reference still shows and equals the TraceIdentifier (`ErrorPageServerErrorTests`: the page value equals what the stub recorded and the handler's error record carries the same value as its `RequestId` scope).

### Visible deviation from D-13's wording

D-13 said no logging change at all. The operator's chosen option changes one logging setting in the deployment environment (console scopes on) so the Reference can be found. No new log call, logger, sink or alert exists in code, and the setting is configuration, not code.

## Live check (port 8350, Development, both User-Agents)

`after/103-06/report.json`: `server-error` answered 500 and `too-many` answered 429 with non-empty bodies for desktop and mobile (themed 3221 and 2543 bytes, against a 41-byte plain sentence). A direct `/Error` with an HTML Accept shows the heading "Something went wrong", `Error 500`, a Reference and the hint; with Accept `*/*` it is a bare 500 with no body. Development still gives the developer exception page for a real exception; this plan does not change that. `logging.json`: without the setting the console log has no RequestId; restarted with `Logging__Console__IncludeScopes=true`, the Reference from the page matched exactly one log scope line (`RequestPath:/Error RequestId:<reference>`). 8350 was then stopped.

## Tracer gate

Operator rule: the mid-plan tracer stop is satisfied by the green automated verify plus a live check. Task 1's verify was green (9 facts) before the expansion task and the live check above ran after Task 3; the operator reviews the page at Gate 1.

## Verification

- Unit: 1679 passed. Integration: 3163 passed, 24 skipped, 0 failed.
- Mutation checks: pointing the handler at a path with no endpoint failed three server-error facts; disabling the navigation test in `OnRejected` failed the themed-429 fact; both restored.

## Known limitations (not built for)

- An over-limit upload surfaces as a handled 500 with no error log line, so its Reference cannot be found in the log.
- `AdminController`'s own 429 responses and the 20 BadRequest calls that carry a message keep their plain-text bodies.
- The Reference span has no styling of its own yet; the first visual gate decides.

## Deviations from Plan

**1. [Process] The Program.cs rate-limit edit landed in the Task 1 commit**
- A single scripted edit applied the handler and the `OnRejected` change together, so commit b3e16f0e carries both; Task 2's commit holds only its tests and guard. No behavior difference; the 429 facts were still shown failing by mutation.

**2. [Process] Facts were written together with the production code**
- The plan asks for facts first. RED was shown afterwards by mutation (see Verification), not before.

**3. [Rule 3 - Blocking] The throwing factory is an abstract base with concrete fixtures**
- xUnit class fixtures need a parameterless constructor, so the "constructor-chosen set of stubs" became three small subclasses.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register: the model carries only the trace identifier (sentinel message, type name and stack frames are asserted absent), the developer page stays Development-only (guard), the page runs in a fresh scope and survives throwing helpers, and OnRejected keeps 429 for every caller.

## Left running

The frozen baseline on 8351 only. 8350 is stopped. SQL Server was not touched.

## Self-Check: PASSED

- Present: the four created files, `after/103-06/report.json`, `after/103-06/logging.json`, this file.
- Commits b3e16f0e and bf54f22f exist.
