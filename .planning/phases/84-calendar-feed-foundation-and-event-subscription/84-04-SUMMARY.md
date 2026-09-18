---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 04
subsystem: calendar-feed
tags: [options-pattern, rate-limiting, http-caching, hangfire, ef-core]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "Plan 84-02's tracer (CalendarSubscriptionService/Repository, CalendarFeedController, CalendarSubscriptions table with RevokedAt tombstone) and plan 84-03's complete CalendarFeedWriter, both widened here without changing their public shapes"
provides:
  - "CalendarFeedOptions (MonthsBack/MonthsAhead/LastFetchedThrottleMinutes/RetentionDays), registered and validated at startup following AgendaOptions' exact shape"
  - "A rolling feed window driven by configuration and the injected clock instead of hard-coded AddMonths literals"
  - "A throttled TouchLastFetchedAsync that performs no database write at all inside the configured interval, not merely a no-op write"
  - "A calendar-feed fixed-window rate-limiting policy (20/15min) partitioned by the address route value, plus address-free logging on the whole feed path"
  - "A body-derived ETag with 304 Not Modified support on a matching If-None-Match"
  - "CalendarSubscriptionRetentionJob: a nightly sweep that purges tombstones retired past RetentionDays, the only code permitted to remove a row from this table"
affects: [84-05, 84-06, 84-07, 84-08, 85]

actuals:
  tokens: 6241
  tasks: 5
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Options-class + AddOptions().Validate().ValidateOnStart() following AgendaOptions' exact shape, now a fourth instance (after AgendaOptions/EventsOverviewOptions/EventSeriesOptions)"
    - "Throttled write: read the tracked row, compare the new time against a nullable last-write time with an inclusive `>=` boundary, and skip SaveChangesAsync entirely when the guard rejects -- no write, not a no-op write"
    - "Rate-limit partition keyed by a route value rather than client IP, following the forgot-password/set-password AddRateLimiter shape"
    - "A single cross-board Hangfire sweep with groupId: null, following RecurringOccurrenceTopUpJob's HangfireJobHelper.RunInScopeAsync shape but for a table that carries no GroupId at all"

key-files:
  created:
    - QuestBoard.Domain/Models/CalendarFeedOptions.cs
    - QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs
    - QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs
    - QuestBoard.Service/Jobs/CalendarSubscriptionRetentionJob.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
    - QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs
    - QuestBoard.Domain/Models/CalendarFeedResult.cs
    - QuestBoard.Repository/CalendarSubscriptionRepository.cs
    - QuestBoard.Service/Controllers/CalendarFeedController.cs
    - QuestBoard.Service/Program.cs

key-decisions:
  - "Amendment carried in from the operator (84-CONTEXT.md D-08, dated 2026-09-18): a retired subscription's 410 is now a bounded guarantee, not indefinite -- CalendarFeedOptions.RetentionDays (default 30) and CalendarSubscriptionRetentionJob implement the nightly purge; a tombstone older than the window answers 404 like an address that never existed."
  - "The 410/404 disclosure cost (T-84-04) is accepted, not mitigated: differing answers confirm to a prober that an address was once real. Judged low-risk given the 256-bit address space, and now explicitly bounded in time by the retention sweep rather than lasting forever."
  - "Rate limiting is partitioned by the feedToken route value, not client IP -- ties the budget to the leaked-or-not address itself so several members' clients behind one home network never share a budget, at the accepted cost that a guesser gets a fresh budget per guess."

requirements-completed: [CALFEED-04, CALFEED-13, CALFEED-15, CALFEED-16, CALFEED-17]

coverage:
  - id: D1
    description: "The feed's date window is a rolling range of recent past and upcoming months, recomputed on every fetch from the injected clock, with both bounds and the throttle interval configurable and working with no configuration present"
    requirement: CALFEED-13
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs (12 facts covering the predicate and its startup wiring)"
        status: pass
    human_judgment: true
    rationale: "The options/validation surface is fully unit-tested, but the window's actual effect on which events reach the feed (changing MonthsAhead changes what appears) is explicitly deferred to plan 84-05's real-HTTP proof per the plan's own text -- a unit test here would only re-assert AddMonths arithmetic."
  - id: D2
    description: "A configured window with a zero or negative forward bound, or a zero/negative throttle or retention value, stops the application at startup rather than serving an unusable feed per request"
    requirement: CALFEED-13
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs#AddDomainServices_InvalidMonthsAhead_ResolvingOptionsThrowsOptionsValidationException"
        status: pass
    human_judgment: false
  - id: D3
    description: "Repeated fetching of one address updates its last-fetched timestamp at most once per configured interval (inclusive boundary), and a fetch inside that interval performs no database write at all"
    requirement: CALFEED-13
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs (5 throttle facts including the inclusive boundary and the missing-row case)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The feed endpoint is rate limited with a budget tied to the individual address rather than a shared client address"
    requirement: CALFEED-15
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests (structural grep-backed acceptance criteria: AddPolicy(\"calendar-feed\"), PermitLimit = 20, feedToken partition key, [EnableRateLimiting] on the action)"
        status: pass
    human_judgment: true
    rationale: "No automated request-level test drives a client past the 20-request/15-minute budget and asserts a 429; the policy's registration and wiring are proven structurally and the endpoint's happy path stays green, but the rejection path itself is unexercised by any test in this plan."
  - id: D5
    description: "No log line written anywhere on the feed path contains a subscription address; the identifier written instead is the subscription's integer id; a request for an address that never existed writes no log line at all"
    requirement: CALFEED-15
    verification:
      - kind: unit
        ref: "grep -nE 'Log(Error|Warning|Information|Debug|Trace|Critical)' CalendarFeedController.cs CalendarSubscriptionService.cs | grep -cE 'feedToken|\\.Token' -> 0"
        status: pass
    human_judgment: false
  - id: D6
    description: "A live response carries an entity tag derived from the emitted document, and a repeat request presenting that tag receives a not-modified answer with no body; the fetch time is recorded on that conditional request too"
    requirement: CALFEED-16
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests (existing tracer fact stays green with the ETag/304 branch added; structural greps for Status304NotModified, If-None-Match, SHA256.HashData all pass)"
        status: pass
    human_judgment: true
    rationale: "No automated test issues a second request carrying a matching If-None-Match header and asserts a 304 with an empty body -- the happy-path 200 fact and the acceptance-criteria greps prove the code exists and is wired, but the conditional-request round trip itself is not exercised end to end by any test in this plan."
  - id: D7
    description: "Retiring an address never removes its row on the request path; a tombstone retired for longer than the configured retention window is purged by a recurring sweep, after which its address answers 404 rather than 410; the sweep is the only code that removes a row from this table"
    requirement: CALFEED-17
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs (6 purge facts: never-retired, inside-window, exact-boundary, past-window, nothing-to-purge, cross-owner)"
        status: pass
    human_judgment: false
duration: 23min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 4: Calendar Feed Hardening -- Configurable Window, Bounded Retirement, Throttle, Rate Limit, Conditional Requests Summary

**The tracer's feed path is now safe to leave running unattended: a validated, configuration-driven rolling window replaces hard-coded month literals, the last-fetched write is throttled to at most once per interval with zero writes in between, the endpoint carries a per-address 20-requests-per-15-minutes policy, no logging call on the path can reach an address, a body-derived ETag supports 304 Not Modified, and a nightly Hangfire sweep purges retirement tombstones past a configurable window so the 410/404 split is bounded in time rather than permanent.**

## Performance

- **Duration:** 23 min
- **Started:** 2026-09-18T08:28:14Z (previous plan's close-out)
- **Completed:** 2026-09-18T08:51:01Z
- **Tasks:** 5
- **Files modified:** 11 (4 created, 7 modified)

## Accomplishments

- `CalendarFeedOptions` (`MonthsBack`=3, `MonthsAhead`=12, `LastFetchedThrottleMinutes`=15, `RetentionDays`=30) registered via `AddOptions<T>().Validate().ValidateOnStart()`, following `AgendaOptions` line for line; no `CalendarFeed` section was added to `appsettings.json`, so the feature still works from code defaults alone
- `CalendarSubscriptionService.GetFeedAsync` reads its window bounds and throttle interval from `IOptions<CalendarFeedOptions>` and the injected `TimeProvider` instead of the tracer's `AddMonths(-3)`/`AddMonths(12)` literals
- `CalendarSubscriptionRepository.TouchLastFetchedAsync` now performs **no write at all** inside the configured interval (inclusive boundary) instead of writing on every call
- A `calendar-feed` fixed-window rate-limiting policy (20 requests / 15 minutes), partitioned by the `feedToken` route value, applied via `[EnableRateLimiting]` on the controller action
- The unknown-address branch logs nothing at all; the retired-address branch logs only the integer `SubscriptionId`; no logging call on the feed path can reach the address value in any form
- A strong `ETag` (SHA256 over the emitted document's UTF-8 bytes) is returned on every live fetch; a matching `If-None-Match` gets a `304 Not Modified` with no body, and the fetch-time write still runs on that path subject to its throttle
- `CalendarSubscriptionRetentionJob` runs nightly at 04:00 in a single cross-board Hangfire scope, purging subscriptions retired more than `RetentionDays` ago via `PurgeRetiredBeforeAsync` -- the only method permitted to remove a row from this table -- and logs only a count and a cutoff, never a subscription id or address

## Task Commits

Each task was committed atomically:

1. **Task 1: CalendarFeedOptions, its registration and its startup validation** - `096d718a` (feat)
2. **Task 2: The rolling window from configuration, and the throttled last-fetched write** - `3d90e859` (feat)
3. **Task 3: The retired-versus-unknown split, per-address rate limiting, and address-free logging** - `767d999c` (feat)
4. **Task 4: Conditional requests, so a repeat poll that changes nothing transfers nothing** - `32864887` (feat)
5. **Task 5: The retention sweep that bounds how long a tombstone lives** - `349691b4` (feat)

**Plan metadata:** committed as part of this SUMMARY.

_Every task followed a RED-then-GREEN sequence in the same commit (tests written first and confirmed failing for the intended reason -- either a failing assertion against pre-existing behavior for Tasks 1/2, or a compile error against a not-yet-existing member for Task 5 -- before the implementation that made them pass), but each task lands as a single commit rather than separate `test(...)`/`feat(...)` commits: this plan's `type` frontmatter is `execute`, not `tdd`, so the strict RED/GREEN commit-gate contract in `tdd.md` (which is scoped to `type: tdd` plans) does not apply here, and the project's own `workflow.tdd_mode` config flag is `false`._

## Files Created/Modified

- `QuestBoard.Domain/Models/CalendarFeedOptions.cs` - new options class, `AgendaOptions`' exact shape plus `RetentionDays`
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - registration and validation chain
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - options-driven window, throttle interval, ETag computation
- `QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs` - `PurgeRetiredBeforeAsync` contract
- `QuestBoard.Domain/Models/CalendarFeedResult.cs` - new `ETag` property
- `QuestBoard.Repository/CalendarSubscriptionRepository.cs` - throttled `TouchLastFetchedAsync`, new `PurgeRetiredBeforeAsync`
- `QuestBoard.Service/Controllers/CalendarFeedController.cs` - rate-limiting attribute, address-free logging, 410/404 consequence comment, 304 conditional branch
- `QuestBoard.Service/Program.cs` - `calendar-feed` rate-limit policy, `calendar-subscription-retention` job registration
- `QuestBoard.Service/Jobs/CalendarSubscriptionRetentionJob.cs` - new nightly sweep
- `QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs` - 12 facts
- `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` - 11 facts (5 throttle, 6 purge)

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: the operator's 2026-09-18 amendment to 84-CONTEXT.md D-08 made the `410` guarantee time-bounded rather than permanent, which is what this plan's fifth task and `CALFEED-17` implement.

## Deviations from Plan

### Auto-fixed Issues

None -- no bugs, missing-critical-functionality, or blocking issues were found beyond the plan's own instructions.

### Noted, Not Fixed

**1. Task 5's `Token|feedToken` acceptance-criteria grep has an unavoidable false positive**
- **Found during:** Task 5 acceptance-criteria verification
- **Issue:** The criterion `grep -cE 'Token|feedToken' CalendarSubscriptionRetentionJob.cs` outputs `0` is written to prove the sweep never touches an address value. The job's `ExecuteAsync(CancellationToken cancellationToken = default)` signature -- required by the Hangfire job shape this task's own `<read_first>` points at (`RecurringOccurrenceTopUpJob.cs`) and by `Program.cs`'s `job => job.ExecuteAsync(CancellationToken.None)` call site -- contains the substring `Token` inside `CancellationToken`, which the regex cannot distinguish from a real feed address reference.
- **Resolution:** Verified manually and via a scoped Log-line-only grep (mirroring Task 3's own narrower pattern: `grep -nE 'Log(...)' ... | grep -cE 'feedToken|\.Token'`, which correctly outputs `0`) that no subscription token, feed address, or `feedToken` variable appears anywhere in the file -- the two literal matches are both `CancellationToken` parameter declarations. Did not rename or restructure the parameter to dodge the substring match, since that would deviate from the established job-shape convention across the codebase for no safety benefit.
- **Files modified:** None beyond the task's own planned changes.
- **Impact:** None on the actual security property (verified true); only the literal grep as written cannot express it precisely.

**2. `Program.cs`'s line endings were normalized to CRLF in full**
- **Found during:** Task 3 (first task in this plan to modify `Program.cs`)
- **Issue:** `Program.cs` was stored in git with LF line endings prior to this plan (confirmed via `git show <prior-commit>:Program.cs | file -`). Per CLAUDE.md's Windows/CRLF convention and this task's own instruction ("Keep both files at CRLF"), the post-edit CRLF-conversion step was applied to the whole file, not only the new lines, so the diff for `Program.cs` shows the entire file as changed (`--ignore-space-at-eol` confirms the real content diff is 26 lines).
- **Fix:** None needed -- this is CLAUDE.md-compliant and plan-directed behavior, not a bug. Recorded here only because the resulting diff size is disproportionate to the actual change and a reviewer should know why.
- **Files modified:** `QuestBoard.Service/Program.cs` (line-ending only, beyond the intended content changes).
- **Verification:** `dotnet build` and the full `dotnet test` suite pass; `file` confirms CRLF.
- **Committed in:** `767d999c` (Task 3), carried forward by Task 5's further edits to the same file.

---

**Total deviations:** 0 auto-fixed; 2 noted-and-verified (1 unsatisfiable-as-literally-written acceptance-criteria grep, 1 line-ending normalization side effect).
**Impact on plan:** Neither affects correctness, security, or scope. No code was changed to work around either item.

## Issues Encountered

None beyond the two noted items above.

## User Setup Required

None - no external service configuration required. No new NuGet package was added in this plan.

## Next Phase Readiness

- The feed path now carries all five hardening controls the plan set out to add: configurable window, bounded retirement, throttled write, per-address rate limit, and address-free logging, plus the conditional-request support the plan folded in as Task 4.
- Plan 84-05 is explicitly where the two window behaviors (changing `MonthsBack`/`MonthsAhead` changes which events reach the feed) and the rate-limit rejection path get their real-HTTP proof -- this plan's own text defers that coverage there rather than duplicating arithmetic in a unit test.
- Not covered by any test in this plan and explicitly deferred: a request-level 429 assertion for the new rate-limit policy, and a full conditional-request round trip (second request with a matching `If-None-Match` asserting `304`) -- see coverage `D4`/`D6` above for the precise gaps a reviewer should weigh.
- The retention sweep is registered but has not yet run in production; its first real execution will not be observable until the nightly Hangfire schedule fires post-deploy.

## Self-Check: PASSED

- `QuestBoard.Domain/Models/CalendarFeedOptions.cs` - FOUND
- `QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs` - FOUND
- `QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs` - FOUND
- `QuestBoard.Service/Jobs/CalendarSubscriptionRetentionJob.cs` - FOUND
- Commit `096d718a` - FOUND in `git log --oneline --all`
- Commit `3d90e859` - FOUND in `git log --oneline --all`
- Commit `767d999c` - FOUND in `git log --oneline --all`
- Commit `32864887` - FOUND in `git log --oneline --all`
- Commit `349691b4` - FOUND in `git log --oneline --all`
- `dotnet build` - PASSED (0 errors)
- `dotnet test QuestBoard.UnitTests` - PASSED (508/508)
- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` - PASSED (1/1)
- `dotnet test` (full solution) - PASSED (508 unit + 720 integration, 0 failures)
- All plan-level `<acceptance_criteria>` greps re-verified after Task 5's changes: `AddPolicy("calendar-feed"`, `EnableRateLimiting("calendar-feed")`, no hard-coded month bound, no address in any Log* call, no `CalendarFeed` section in `appsettings.json` - all PASSED

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
