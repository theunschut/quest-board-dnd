---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 05
subsystem: calendar-feed
tags: [integration-test, tenant-isolation, rate-limiting, http-caching, ef-core, aspnetcore]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "Plan 84-02's tracer (CalendarSubscriptionService/Repository, CalendarFeedController, CalendarFeedWriter) and plan 84-04's hardening (CalendarFeedOptions, the throttled TouchLastFetchedAsync, the calendar-feed rate-limit policy, address-free logging, the ETag/304 branch, the retention sweep) -- this plan proves all of it end to end over real HTTP without changing any of it"
provides:
  - "A shared CapturingLoggerProvider wired into WebApplicationFactoryBase, available to every integration suite that needs to assert what the application actually logged rather than what its source appears to log"
  - "A widened CalendarSubscriptionFeedTests suite: 29 facts (up from the tracer's 1) covering four two-group tenant isolation cases, every response code, every content/marker rule, both window bounds in both directions, the fetch-time throttle, and the full conditional-request round trip"
affects: [84-06, 84-07, 84-08, 85]

actuals:
  tokens: 39500
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Runtime log-capture harness (CapturingLoggerProvider): an ILoggerProvider registered via ConfigureLogging that renders every message AND every structured state key/value into one string, so a test can assert a value's total absence from logging rather than trust a message-template grep"
    - "Boundary-derived window facts: date offsets for window-bound tests are computed from IOptions<CalendarFeedOptions> resolved through the running host's own DI container, not from literals, so a future change to MonthsBack/MonthsAhead cannot silently invalidate the fact"

key-files:
  created:
    - QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs
  modified:
    - QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs

key-decisions:
  - "The log-safety fact's 'harness itself works' smoke test is inlined into the same fact (a second, dedicated revoke-then-fetch inside Feed_LogsNoSubscriptionAddress_WhenServingALiveAddress) rather than trusting a sibling fact's side effect, since xUnit gives no ordering guarantee across facts and each fact clears the database independently."
  - "The rate-limit policy's PermitLimit is pinned as a named constant (CalendarFeedPolicyPermitLimit = 20) with a comment tying it to Program.cs, rather than read at runtime -- the policy is defined in code, not configuration, so there is nothing to read; the constant is the single point that would need updating if Program.cs's policy ever changes."
  - "Two window facts (before/inside the past bound, and their future-window mirror) needed a byte-length fix mid-execution: RFC 5545's 75-octet line-folding rule broke a substring assertion that spanned the fold point on the longer of two originally-chosen names. Shortened the future-window fact's board/event names below the fold threshold rather than changing the fold-detection logic itself, since folding a long SUMMARY line is correct writer behavior, not a bug."

requirements-completed: [CALFEED-04, CALFEED-06, CALFEED-07, CALFEED-08, CALFEED-13, CALFEED-15, CALFEED-16]

coverage:
  - id: D1
    description: "A viewer belonging to two boards receives events from both boards in one feed; a non-member board's event stays absent even behind a deliberately stale signup row; a board the viewer leaves disappears on the very next fetch; a viewer with no boards gets a valid empty calendar with no error logged"
    requirement: CALFEED-07
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_IncludesEventsFromEveryBoardTheViewerBelongsTo"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ExcludesAnEventOnABoardTheViewerDoesNotBelongTo"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_StopsIncludingABoardTheViewerHasLeft"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ReturnsAValidEmptyCalendar_ForAViewerWithNoBoards"
        status: pass
    human_judgment: true
    rationale: "These four facts prove the application's own scoping logic over the EF Core InMemory provider, which evaluates every predicate as ordinary LINQ-to-Objects. They do not prove the query translates identically on the relational provider the application actually runs on in production -- the suite's own class doc comment states this explicitly, matching AgendaTenantIsolationTests' precedent. A human should weigh whether that gap needs a separate relational test before treating this as full closure of the tenant-isolation threat (T-84-01/T-84-23)."
  - id: D2
    description: "No log line emitted while serving a feed request contains the subscription's address, in either the rendered message or any structured state value; a request for an address that never existed emits no log line from the feed controller at all; exceeding the endpoint's per-address budget returns 429"
    requirement: CALFEED-15
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_LogsNoSubscriptionAddress_WhenServingALiveAddress"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_LogsNothing_ForAnAddressThatNeverExisted"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_Returns429_WhenTheAddressExceedsItsRequestBudget"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every response code (410 for retired, 404 for unknown/malformed, 200 for live), every content/inclusion rule (no-signup-row exclusion, cancelled-event exclusion, plain/maybe/declined title markers, the date-valued all-day branch), both window bounds in both directions derived from configuration, the fetch-time throttle over real HTTP, and the full conditional-request round trip (ETag issuance, matching/stale If-None-Match, an edit changing the tag, the fetch time still advancing on a 304) are each proven by their own fact"
    requirement: CALFEED-08
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs (21 facts added in Task 3, enumerated in the Accomplishments section below)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The rolling date window (CALFEED-13) actually changes which events reach the feed at both bounds, derived from CalendarFeedOptions rather than hard-coded literals"
    requirement: CALFEED-13
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ExcludesAnEventBeforeTheWindowStarts, #Feed_IncludesAnEventInsideThePastWindow, #Feed_ExcludesAnEventBeyondTheWindowEnd, #Feed_IncludesAnEventInsideTheFutureWindow"
        status: pass
    human_judgment: false
  - id: D5
    description: "A live response carries an entity tag whose value round-trips correctly through a 304/200 branch, and an event edit changes the tag"
    requirement: CALFEED-16
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ReturnsAnEntityTag_OnALiveResponse, #Feed_Returns304_WhenTheClientPresentsTheMatchingEntityTag, #Feed_ReturnsAFreshBody_WhenTheClientPresentsAStaleEntityTag, #Feed_ChangesTheEntityTag_WhenAnEventIsEdited, #Feed_RecordsTheFetchTime_EvenOnANotModifiedResponse"
        status: pass
    human_judgment: false
duration: 24min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 5: Calendar Feed Verification Suite Summary

**The calendar feed's tracer-proven single fact is now a 29-fact suite over real HTTP: a shared log-capturing test harness proves the address never reaches a log line, four facts pin cross-board aggregation and tenant isolation the way the two-joined-boards case actually catches a collapse-to-one-board bug, and 21 more facts pin every response code, content rule, window bound, throttle behavior and conditional-request branch the endpoint carries.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-18T08:54:00Z (previous plan's close-out)
- **Completed:** 2026-09-18T09:18:00Z
- **Tasks:** 3
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- `CapturingLoggerProvider` (new): an `ILoggerProvider` rendering every message and every structured state key/value into one captured string, wired into `WebApplicationFactoryBase` via `ConfigureLogging` alongside the existing `CapturingBackgroundJobClient` pattern -- available to every integration suite in this project, not just this one
- `Feed_LogsNoSubscriptionAddress_WhenServingALiveAddress`, `Feed_LogsNothing_ForAnAddressThatNeverExisted`, `Feed_Returns429_WhenTheAddressExceedsItsRequestBudget` -- the runtime proof that address-free logging and per-address rate limiting (both shipped in plan 84-04) actually hold over real HTTP, not just by source inspection
- Four two-group tenant isolation facts (`Feed_IncludesEventsFromEveryBoardTheViewerBelongsTo`, `Feed_ExcludesAnEventOnABoardTheViewerDoesNotBelongTo`, `Feed_StopsIncludingABoardTheViewerHasLeft`, `Feed_ReturnsAValidEmptyCalendar_ForAViewerWithNoBoards`) -- the first proves aggregation across two boards rather than mere absence, catching a collapse-to-one-board regression that a single-board suite structurally cannot
- Four response-code facts (`Feed_Returns410_ForARetiredAddress`, `Feed_Returns404_ForAnAddressThatNeverExisted`, `Feed_Returns404_ForAnAddressThatIsNotWellFormed`, `Feed_KeepsAnsweringGone_AfterARetiredAddressIsFetchedRepeatedly`)
- Six content-rule facts pinning which events reach the feed and how they render: no-signup-row exclusion, cancelled-event exclusion, the plain-title auto-created row, the `(maybe)`/`(declined)` suffixes, and the date-valued all-day branch
- Four window-bound facts, each deriving its test dates from `IOptions<CalendarFeedOptions>` rather than a literal, so a future change to `MonthsBack`/`MonthsAhead` cannot silently invalidate them
- Two throttle facts and five conditional-request facts, together proving the fetch-time write happens once on a first fetch, stays unchanged on an immediate second fetch, and still advances on a 304 -- the full round trip plan 84-04 wired but never drove end to end

## Task Commits

Each task was committed atomically:

1. **Task 1: A log-capturing harness, and the runtime proof that the address never reaches a log line** - `ec3b9070` (test)
2. **Task 2: Four two-group tenant isolation facts** - `3978ca5b` (test)
3. **Task 3: Response-code and content facts** - `b9c590ae` (test), with a follow-up fix `9cb67932` (fix) stripping three phase-document references the drafting pass had left in test comments

**Plan metadata:** committed as part of this SUMMARY.

## Files Created/Modified

- `QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs` - shared log-capturing `ILoggerProvider`, renders message + structured state + exception into one string per record
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` - new `LogCapture` property, one `ConfigureLogging` call registering the provider; no existing registration touched
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - widened from 1 fact to 29; new helpers `RemoveMembershipAsync`, `RevokeSubscriptionAsync`, `ReadLastFetchedAsync`, `GetFeedOptions`; `SeedSignupAsync` gained an `answered` parameter; `SeedEventAsync` gained a `cancelledAt` parameter; class doc comment extended with the InMemory-provider honesty statement

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: the log-safety fact proves the capture harness itself works by triggering its own guaranteed-to-log revoke-then-fetch inline, rather than depending on another fact's side effect or run order.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] RFC 5545 line-folding broke a substring assertion in the future-window fact**
- **Found during:** Task 3, first test run
- **Issue:** `Feed_IncludesAnEventInsideTheFutureWindow`'s board+event name combination produced a `SUMMARY` content line of 77 octets, one over the writer's 75-octet fold threshold. The fold split the word "Session" itself, so `body.Should().Contain("Inside Future Window Suite Session")` failed against genuinely correct output.
- **Fix:** Shortened the fact's board and event names (dropping "Inside") to bring the combined line under the fold threshold, with a comment explaining why the length matters for this specific fact.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`
- **Verification:** Fact passes; full suite re-run confirms no other fact's names are long enough to fold.
- **Committed in:** `b9c590ae` (Task 3 commit)

**2. [Rule 1 - Bug] Ambient query filter returned zero rows when reading an event through an unfiltered test context**
- **Found during:** Task 3, `Feed_ChangesTheEntityTag_WhenAnEventIsEdited`
- **Issue:** `EventEntity`'s `HasQueryFilter` is `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId` -- a null active group id (which `TestDatabase.CreateContext()` always sets) evaluates the filter to false for every row, not "see all rows" as a stale comment on `TestDatabase` suggested. A plain `ctx.Events.FirstAsync(...)` therefore threw `Sequence contains no elements`.
- **Fix:** Added `.IgnoreQueryFilters()` to the read, matching the established pattern already used throughout this test project (`EventsControllerIntegrationTests`, `EventAvailabilityTenantIsolationTests`, etc.) for exactly this situation.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`
- **Verification:** Fact passes; full suite re-run green.
- **Committed in:** `b9c590ae` (Task 3 commit)

**3. [CLAUDE.md compliance - not a bug, a drafting slip] Three comments cited a phase document by name**
- **Found during:** Post-Task-3 self-review, before writing this Summary
- **Issue:** Three code comments read `(84-CONTEXT.md D-##)` -- a phase-number reference in a source comment, which CLAUDE.md explicitly forbids because it goes stale the moment the phase closes.
- **Fix:** Reworded each comment to state the accepted-cost reasoning in plain language with no phase or requirement-ID reference.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`
- **Verification:** `grep -nE 'CALFEED|84-[0-9]|Phase [0-9]+|Plan [0-9]+'` across every file this plan touched returns nothing; full suite re-run stays green.
- **Committed in:** `9cb67932` (follow-up fix commit)

---

**Total deviations:** 3 auto-fixed (2 Rule 1 bugs, 1 CLAUDE.md-compliance correction). **Impact on plan:** All three are test-file-only; no production code changed. No scope creep.

## Issues Encountered

None beyond the deviations above.

## User Setup Required

None - no external service configuration required. No new NuGet package was added in this plan.

## Next Phase Readiness

- The calendar feed endpoint now carries 29 facts over real HTTP covering cross-board aggregation, tenant isolation, every response code, every content rule, both window bounds, the throttle, and the full conditional-request round trip -- the phase's remaining plans (84-06 Profile UI, 84-07/84-08) can build on a verified, not just implemented, endpoint.
- Explicitly out of scope for this suite, per its own class doc comment: proof that the query translates identically on the relational provider the application runs on in production. The board-containment test over an empty id collection and the filter-bypass/signup-filter join never reach a SQL translator under the EF Core InMemory provider. Closing that gap needs a separate relational test, matching the precedent `AgendaTenantIsolationTests` already recorded for the agenda feature.
- No stubs, skipped tests, or unrun `<verify>` blocks were left behind by this plan.

## Self-Check: PASSED

- `QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs` - FOUND
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` - FOUND (modified)
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - FOUND (modified, 29 facts)
- Commit `ec3b9070` - FOUND in `git log --oneline --all`
- Commit `3978ca5b` - FOUND in `git log --oneline --all`
- Commit `b9c590ae` - FOUND in `git log --oneline --all`
- Commit `9cb67932` - FOUND in `git log --oneline --all`
- `dotnet build` - PASSED (0 errors)
- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` - PASSED (29/29)
- `dotnet test QuestBoard.IntegrationTests` - PASSED (748/748, 0 failures) -- no other suite sharing `WebApplicationFactoryBase` broke
- `dotnet test` (full solution) - PASSED (508 unit + 748 integration, 0 failures)
- All plan-level acceptance-criteria greps re-verified: `ConfigureLogging` (1), `LogCapture` property (1), `ConcurrentQueue|lock \(` (3), `TooManyRequests` (1), isolation fact names (4), `RemoveMembershipAsync` (2), in-memory honesty statement (present), `HttpStatusCode.NotModified` (2), `If-None-Match` (3), `HttpStatusCode.Gone` (4), `HttpStatusCode.NotFound` (3), `(declined)` (2), `(maybe)` (2), `VALUE=DATE` (1), `MonthsBack|MonthsAhead` (5), `ReadLastFetchedAsync` (6) - all PASSED
- No requirement id, phase number or plan number found in any file this plan touched - PASSED

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
