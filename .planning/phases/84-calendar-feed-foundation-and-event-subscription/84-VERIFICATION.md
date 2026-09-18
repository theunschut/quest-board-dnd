---
phase: 84-calendar-feed-foundation-and-event-subscription
verified: 2026-09-18T13:52:28Z
status: human_needed
score: 17/17 must-haves verified
covered_files: [".planning/REQUIREMENTS.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-01-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-01-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-02-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-02-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-03-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-03-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-04-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-04-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-05-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-05-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-06-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-06-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-07-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-07-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-PLAN.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-08-SUMMARY.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md", ".planning/phases/84-calendar-feed-foundation-and-event-subscription/84-VALIDATION.md", "QuestBoard.Domain/Enums/CalendarFeedSource.cs", "QuestBoard.Domain/Enums/CalendarFeedStatus.cs", "QuestBoard.Domain/Extensions/ServiceExtensions.cs", "QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs", "QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs", "QuestBoard.Domain/Interfaces/ICalendarSubscriptionService.cs", "QuestBoard.Domain/Models/CalendarFeedEntry.cs", "QuestBoard.Domain/Models/CalendarFeedOptions.cs", "QuestBoard.Domain/Models/CalendarFeedResult.cs", "QuestBoard.Domain/Models/CalendarSubscription.cs", "QuestBoard.Domain/Services/CalendarFeedWriter.cs", "QuestBoard.Domain/Services/CalendarSubscriptionService.cs", "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs", "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs", "QuestBoard.IntegrationTests/Tests/ProfileCalendarSubscriptionTests.cs", "QuestBoard.Repository/CalendarSubscriptionRepository.cs", "QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs", "QuestBoard.Repository/EventSignupRepository.cs", "QuestBoard.Repository/GroupRepository.cs", "QuestBoard.Service/Controllers/Admin/AccountController.cs", "QuestBoard.Service/Controllers/CalendarFeedController.cs", "QuestBoard.Service/Helpers/CalendarSubscriptionAddress.cs", "QuestBoard.Service/Helpers/CalendarSubscriptionQrCode.cs", "QuestBoard.Service/Jobs/CalendarSubscriptionRetentionJob.cs", "QuestBoard.Service/Program.cs", "QuestBoard.Service/ViewModels/AccountViewModels/CalendarSubscriptionViewModel.cs", "QuestBoard.Service/Views/Account/Profile.Mobile.cshtml", "QuestBoard.Service/Views/Account/Profile.cshtml", "QuestBoard.UnitTests/Extensions/CalendarFeedOptionsValidationTests.cs", "QuestBoard.UnitTests/Repository/CalendarSubscriptionRepositoryTests.cs", "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs"]
covered_digest: "v1:sha256:cb9fd9bfd68ba2e5c86e0561c632aef62cbbd00ce5249cda54e33f90d795314a"
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Subscribe a real phone (iOS Calendar, Google Calendar, and Outlook) to a minted subscription address and confirm events render"
    expected: "The address is accepted, a calendar named per X-WR-CALNAME (or the client's own fallback) appears, the timed event renders as a one-hour block, the all-day entry spans exactly one day, neither marks the reader busy, and the entries do not require opening the quest board"
    why_human: "Apple/Google/Outlook fetch the feed server-side from their own infrastructure; no automated harness can drive their pollers or renderers, and no localhost/LAN address can satisfy them without a public tunnel or the deployed application. This is task 84-08-T3, a blocking checkpoint:human-verify that was deferred to deployment by explicit operator decision -- not run, not approved, not failed."
  - test: "Edit an already-subscribed event, note the time, and watch a real client until the change appears; separately confirm the webcal:// handoff actually launches a calendar app when tapped/scanned on a real device"
    expected: "The edited event eventually reflects the new value on the device (latency is vendor-chosen and only observable this way), and the QR/webcal link opens the device's calendar app rather than a browser download"
    why_human: "Client poll cadence and the webcal:// OS-level handoff are not observable from the server or from an automated test; this is the same deferred 84-08-T3 checkpoint covering the remaining two manual-only verifications recorded in 84-VALIDATION.md (refresh latency, and the webcal handoff / camera QR scan)."
---

# Phase 84: Calendar Feed Foundation and Event Subscription Verification Report

**Phase Goal:** A board member can point their phone's calendar at a personal subscription URL once and have every event from every board they belong to appear there on its own — all-day entries, timed entries, later edits, and cancellations included — without opening the quest board.
**Verified:** 2026-09-18T13:52:28Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (requirement) | Status | Evidence |
|---|---|---|---|
| 1 | CALFEED-01: No subscription exists until Add is pressed; pressing Add mints exactly one 256-bit random URL-safe address | VERIFIED | `AccountController.AddCalendarSubscription` (10s double-submit guard) -> `CalendarSubscriptionService.MintForUserAsync` -> `RandomNumberGenerator.GetBytes(32)` + `WebEncoders.Base64UrlEncode`. `84-06-T3` full-suite gate is green. |
| 2 | CALFEED-02: Several subscriptions per member, each independently named, addressed and revocable | VERIFIED | `CalendarSubscriptionRepository.GetForUserAsync` (excludes revoked), `RenameAsync`/`RevokeAsync` matched on `(id, userId)` together so one row's action can never touch another's. |
| 3 | CALFEED-03 (amended 2026-09-18 UAT): Every row shows name, created date, last-fetched, offers rename/delete, address copyable but not displayed except on clipboard-denied fallback | VERIFIED | `Profile.cshtml`/`Profile.Mobile.cshtml`: `readonly` input carries `visually-hidden`; `showManualFallback()` JS removes `visually-hidden` only when `navigator.clipboard` is unavailable. Confirmed present, identical logic, on both layouts. |
| 4 | CALFEED-04 (amended 2026-09-18): Delete retires a tombstone; retired address answers 410 for a bounded retention window; never-existed answers 404 | VERIFIED | `CalendarSubscriptionRepository.RevokeAsync` sets `RevokedAt` only (no delete); `CalendarFeedController.Feed` switches `Revoked` -> 410, `NotFound` -> 404; `PurgeRetiredBeforeAsync` is the only method permitted to `RemoveRange`. `Feed_Returns410_ForARetiredAddress`, `Feed_Returns404_ForAnAddressThatNeverExisted`, `Feed_KeepsAnsweringGone_AfterARetiredAddressIsFetchedRepeatedly` pass. |
| 5 | CALFEED-05: Feed serves `text/calendar` to an anonymous caller with no cookie, no session, no active board | VERIFIED | `[AllowAnonymous]` on `CalendarFeedController`; not in `GroupSessionMiddleware`'s exempt list (unnecessary — unauthenticated requests bypass it). `CalendarSubscriptionService.GetFeedAsync` never resolves `IActiveGroupContext` (confirmed by grep — the only hit is the explanatory comment, not an injection). Integration tests set `factory.TestGroupContext.ActiveGroupId = null` before every feed fetch. |
| 6 | CALFEED-06: Feed carries exactly the events the owner holds a signup row on, across every board still a member of | VERIFIED | `EventSignupRepository.GetFeedRowsForUserAsync` is rooted at `EventSignups` (not `Events`) with `IgnoreQueryFilters()` + explicit `memberGroupIds.Contains(...)`. `Feed_IncludesEventsFromEveryBoardTheViewerBelongsTo`, `Feed_ExcludesAnEventOnABoardTheViewerDoesNotBelongTo`, `Feed_StopsIncludingABoardTheViewerHasLeft`, `Feed_ExcludesAnEventTheViewerHoldsNoSignupRowOn` all pass. |
| 7 | CALFEED-07: A row surviving the board predicate but outside membership is dropped and logged as an error | VERIFIED (code inspection; defense-in-depth branch not independently unit-tested) | `CalendarSubscriptionService.GetFeedAsync` lines 84-91: `checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId))`, `logger.LogError(...)` on any drop. This mirrors the pre-existing, equally untested `EventService.GetCrossBoardAgendaAsync` second-layer check — an established codebase idiom that cannot be triggered without breaking the query it defends, so absence of a forcing test is consistent with prior practice, not a gap unique to this phase. |
| 8 | CALFEED-08: A cancelled event never appears, on any board, for any subscription | VERIFIED | `es.Event.CancelledAt == null` in the repository predicate; `Feed_ExcludesACancelledEvent` passes. |
| 9 | CALFEED-09: Title is `[Board] Title` with `(maybe)`/`(declined)` suffix only when applicable | VERIFIED | `CalendarFeedWriter.BuildSummary`; `Write_YesAnswer_EmitsPlainTitleWithNoSuffix`, `Write_MaybeAnswer_AppendsMaybeSuffix`, `Write_NoAnswer_AppendsDeclinedSuffix` exact-byte tests pass. |
| 10 | CALFEED-10: Timed event -> one-hour floating-local-time entry; no-start-time event -> true all-day entry | VERIFIED | `AppendTimedEvent`/`AppendAllDayEvent` (exclusive next-day `DTEND;VALUE=DATE`); `Write_TimedEntry_EmitsStartAndEndOneHourApart`, `Write_NullStartTimeEntry_EmitsDateValuedStartAndExclusiveNextDayEnd` pass. |
| 11 | CALFEED-11: Every entry is TRANSPARENT with no description, link, or alarm | VERIFIED | Writer emits only UID/DTSTAMP/DTSTART/DTEND/SUMMARY/TRANSP/SEQUENCE — no `DESCRIPTION`/`URL`/`VALARM` anywhere in the writer. `Write_Document_NeverEmitsForbiddenProperties` passes. |
| 12 | CALFEED-12: Identifier is stable across repeated fetches and namespaced by source | VERIFIED | `BuildUid` = fixed literal prefix + source + numeric id, no request-derived input; `DTSTAMP` derives from `entry.CreatedAt`, not ambient clock. `Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange`, `Write_SameEntryTwice_UidLineIsIdenticalAcrossRenders`, `BuildUid_EventFortyTwo_ReturnsExactLiteral` pass. |
| 13 | CALFEED-13: Rolling window recomputed on every fetch, both bounds configurable with no code change | VERIFIED | `CalendarFeedOptions` bound via `services.AddOptions<CalendarFeedOptions>().BindConfiguration(...).Validate(...).ValidateOnStart()` (`ServiceExtensions.cs`); `windowStart`/`windowEnd` computed per-request from `timeProvider.GetUtcNow()` in `GetFeedAsync`. `Feed_ExcludesAnEventBeforeTheWindowStarts`/`...BeyondTheWindowEnd`/`...InsideThePastWindow`/`...InsideTheFutureWindow` pass; `CalendarFeedOptionsValidationTests` covers the predicate and its startup wiring. |
| 14 | CALFEED-14: Both Profile layouts carry the section; every row offers copy control, webcal link, and scannable QR code | VERIFIED | Confirmed by direct markup read of both `Profile.cshtml` and `Profile.Mobile.cshtml` — same `calendar-subscription-copy-btn`, `WebcalAddress` anchor, and `QrCodeSvg` modal present in both, not just asserted by tests. `CalendarSubscriptionStaticGuardTests` additionally guards copy parity, touch-target sizing, and QR sizing on both layouts. |
| 15 | CALFEED-15: Feed endpoint rate limited per address; no application log line ever contains a full address | VERIFIED | `RateLimitPartition.GetFixedWindowLimiter` partitioned on `feedToken` route value (20/15min); `CalendarFeedController` logs only `SubscriptionId`, never the token, in every branch. `Feed_Returns429_WhenTheAddressExceedsItsRequestBudget`, `Feed_LogsNoSubscriptionAddress_WhenServingALiveAddress` (with its own harness self-test against a false negative), `Feed_LogsNothing_ForAnAddressThatNeverExisted` pass. |
| 16 | CALFEED-16: Repeated fetching updates last-fetched at most once per throttle interval | VERIFIED | `TouchLastFetchedAsync` returns without calling `SaveChangesAsync` when the guard rejects. `Feed_RecordsAFetchTime_OnTheFirstFetch`, `Feed_DoesNotRewriteTheFetchTime_OnAnImmediateSecondFetch` pass; repository unit tests cover the throttle boundary directly. |
| 17 | CALFEED-17: A tombstone retired longer than the retention window is purged; its address then answers 404 like one that never existed | VERIFIED | `CalendarSubscriptionRepository.PurgeRetiredBeforeAsync` (only method allowed to `RemoveRange`); `CalendarSubscriptionRetentionJob` registered via `RecurringJob.AddOrUpdate<CalendarSubscriptionRetentionJob>` in `Program.cs`. Unit tests cover never-retired (never purged), inside window (not purged), exactly at boundary (not purged), beyond window (purged), and multi-owner counting. |
| 18 | Phase goal (end-to-end): a real phone's calendar actually renders the feed after one-time subscribe, including later edits and cancellations, without opening the board | ⚠️ Not independently verifiable here — see Human Verification | The document itself is proven (external RFC 5545 conformance validator: 0 errors, 0 warnings) and 96 integration + 53 relevant unit tests exercise every server-side contract (isolation, status codes, throttle, escaping, folding). No automated harness can drive Apple/Google/Outlook's own poller or renderer; task `84-08-T3` (the real-device checkpoint) was deferred to deployment by explicit operator decision, not run and not approved. |

**Score:** 17/17 requirement-level truths verified. The 18th row (the literal end-to-end phone experience) is the phase's own irreducible manual gate, honestly carried as deferred rather than claimed — it does not count against the 17/17 requirement score and does not, per the operator's recorded decision, block phase closure.

### Deferred Items

None — no gap identified here is deferred to a later phase. The one open item (real-device verification) is deferred to *deployment*, not to a later roadmap phase, and is already recorded in `.planning/STATE.md`'s Deferred Items table, `.planning/ROADMAP.md`'s Phase 84 risks block, and `.planning/WINDOWS.md`.

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | Hand-rolled RFC 5545 writer | VERIFIED | Exact-byte tested; folds at 75 octets on UTF-8 boundaries; CRLF line endings; escapes `\`, `;`, `,`, newlines. |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | Fresh-membership feed orchestration with second-layer re-check | VERIFIED | Membership re-read per request; second-layer drop + `LogError`; no `IActiveGroupContext` dependency. |
| `QuestBoard.Repository/CalendarSubscriptionRepository.cs` | Tombstone-aware CRUD, throttled touch, retention purge | VERIFIED | `RevokeAsync` never deletes; `PurgeRetiredBeforeAsync` is the sole delete path; `TouchLastFetchedAsync` no-ops under throttle. |
| `QuestBoard.Repository/EventSignupRepository.cs::GetFeedRowsForUserAsync` | Signup-rooted, `IgnoreQueryFilters()`, explicit membership predicate, cancellation/window filters | VERIFIED | Confirmed rooted at `EventSignups`, not `Events`; explicit `memberGroupIds.Contains`. |
| `QuestBoard.Service/Controllers/CalendarFeedController.cs` | Anonymous, rate-limited, address-free-logging feed endpoint | VERIFIED | `[AllowAnonymous]`, `[EnableRateLimiting("calendar-feed")]`, logs `SubscriptionId` only. |
| `QuestBoard.Service/Jobs/CalendarSubscriptionRetentionJob.cs` | Recurring purge sweep | VERIFIED | Registered via Hangfire `RecurringJob.AddOrUpdate` in `Program.cs`; group-context-free (`groupId: null`). |
| `QuestBoard.Service/Views/Account/Profile.cshtml` + `.Mobile.cshtml` | Calendar Subscription section on both layouts | VERIFIED | Full parity read directly from both files: name/created/last-fetched, rename/delete, hidden-by-default address with clipboard-fallback reveal, webcal link, QR modal. |
| `QuestBoard.Service/Helpers/CalendarSubscriptionAddress.cs` / `CalendarSubscriptionQrCode.cs` | Absolute HTTPS/webcal address builders and QR renderer | VERIFIED | Built from configured `AppUrl`, never request scheme/host; wired into `AccountController.Profile`. |
| `QuestBoard.Domain/Models/CalendarFeedOptions.cs` | Configurable rolling window / throttle / retention | VERIFIED | Bound, validated, `ValidateOnStart()`; defaults documented and tested. |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `Profile.cshtml` / `.Mobile.cshtml` "Add Subscription" form | `AccountController.AddCalendarSubscription` | `asp-action="AddCalendarSubscription"` POST | WIRED | Confirmed by controller action and markup form both present. |
| `AccountController.Profile` | `ICalendarSubscriptionService.GetForUserAsync` | Direct call, real user id | WIRED, DATA FLOWS | Rows built from live service call, not a static/mocked list; `HttpsAddress`/`WebcalAddress`/`QrCodeSvg` computed per-row from real `subscription.Token`. |
| `CalendarFeedController.Feed` | `ICalendarSubscriptionService.GetFeedAsync` | Direct call | WIRED | Route token passed straight through; response body/status derived from the service result, not a static return. |
| `CalendarSubscriptionService.GetFeedAsync` | `IGroupService.GetGroupsForUserAsync` → `GroupRepository.GetGroupsForUserAsync` | Direct call, fresh per-request DB query | WIRED, DATA FLOWS | Confirmed a real EF query (`Groups.Where(g => g.UserGroups.Any(ug => ug.UserId == userId))`), not session/claims. |
| `CalendarSubscriptionService.GetFeedAsync` | `IEventSignupRepository.GetFeedRowsForUserAsync` | Direct call with fresh `memberGroupIds` | WIRED, DATA FLOWS | Called unconditionally including empty-membership case (verified by comment + `Feed_ReturnsAValidEmptyCalendar_ForAViewerWithNoBoards`). |
| `CalendarSubscriptionRetentionJob` | `ICalendarSubscriptionRepository.PurgeRetiredBeforeAsync` | Hangfire recurring job, scoped DI | WIRED | Registered in `Program.cs`; single scope, `groupId: null`. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|---|---|---|---|---|
| `Profile` view's `CalendarSubscriptions` | `subscriptionRows` | `calendarSubscriptionService.GetForUserAsync(user.Id, token)` | Yes — real per-user DB rows | FLOWING |
| Feed response body | `result.Body` | `CalendarSubscriptionService.GetFeedAsync` -> `writer.Write(entries, ...)` built from `checkedRows` (real signup/event rows) | Yes | FLOWING |
| `subscription.HttpsAddress`/`WebcalAddress` | Built in `AccountController.Profile` | `CalendarSubscriptionAddress.BuildHttps/BuildWebcal(emailSettings.Value.AppUrl, subscription.Token)` | Yes — real per-subscription token | FLOWING |
| `subscription.QrCodeSvg` | Built in `AccountController.Profile` | `CalendarSubscriptionQrCode.ToSvg(row.WebcalAddress)` (logs a warning and leaves it null on failure, does not fabricate) | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| `dotnet build` (whole solution) | `dotnet build QuestBoard.slnx --nologo -v:q` | 6 projects, 0 errors, 22 (pre-existing, unrelated AngleSharp version) warnings | PASS |
| Calendar-feed-specific unit tests | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests\|...CalendarFeedOptionsValidationTests\|...CalendarSubscriptionRepositoryTests"` | 53 passed, 0 failed | PASS |
| Calendar-feed-specific integration tests | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarSubscriptionFeedTests\|...ProfileCalendarSubscriptionTests\|...CalendarSubscriptionStaticGuardTests"` | 96 passed, 0 failed | PASS |

Both filtered runs were executed directly in this verification pass (not taken from SUMMARY claims); the full-workspace run was not re-run here since these are the phase-scoped subsets and the SUMMARY-reported full-suite green (508 unit + 815 integration) is consistent with these subset results plus the earlier `dotnet build` success.

### Probe Execution

Not applicable — this phase has no `scripts/*/tests/probe-*.sh` convention; verification relied on `dotnet build`/`dotnet test` as the project's native runnable checks (Step 7b).

### Requirements Coverage

| Requirement | Source Plan(s) | Status | Evidence |
|---|---|---|---|
| CALFEED-01 | 84-01, 84-02, 84-06, 84-08 | SATISFIED | See truth #1 |
| CALFEED-02 | 84-01, 84-02, 84-06, 84-08 | SATISFIED | See truth #2 |
| CALFEED-03 | 84-01, 84-06, 84-07, 84-08 | SATISFIED (amended text) | See truth #3 |
| CALFEED-04 | 84-01, 84-04, 84-05 | SATISFIED (amended text) | See truth #4 |
| CALFEED-05 | 84-01, 84-02 | SATISFIED | See truth #5 |
| CALFEED-06 | 84-01, 84-02, 84-05 | SATISFIED | See truth #6 |
| CALFEED-07 | 84-01, 84-02, 84-05 | SATISFIED | See truth #7 |
| CALFEED-08 | 84-01, 84-03, 84-05 | SATISFIED | See truth #8 |
| CALFEED-09 | 84-01, 84-03 | SATISFIED | See truth #9 |
| CALFEED-10 | 84-01, 84-03 | SATISFIED | See truth #10 |
| CALFEED-11 | 84-01, 84-03 | SATISFIED | See truth #11 |
| CALFEED-12 | 84-01, 84-03 | SATISFIED | See truth #12 |
| CALFEED-13 | 84-01, 84-04, 84-05 | SATISFIED | See truth #13 |
| CALFEED-14 | 84-01, 84-06, 84-07, 84-08 | SATISFIED | See truth #14 |
| CALFEED-15 | 84-01, 84-04, 84-05 | SATISFIED | See truth #15 |
| CALFEED-16 | 84-01, 84-04, 84-05 | SATISFIED | See truth #16 |
| CALFEED-17 | 84-04 (minted mid-phase per 84-CONTEXT D-08 amendment) | SATISFIED | See truth #17 |

No orphaned requirements: all 17 CALFEED IDs declared across the 8 plans' frontmatter are accounted for above, and `.planning/REQUIREMENTS.md` shows exactly these 17 IDs mapped to "Phase 84" (confirmed by direct grep, not by trusting the checked-box claim).

### Anti-Patterns Found

None. Scanned every phase-touched implementation file (writer, service, repository, controller, job, helpers, both Profile views) for `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`/"not yet implemented"/hardcoded-empty-return patterns — zero hits outside legitimate HTML `placeholder="e.g. My Phone"` form attributes. `CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree` additionally guards, and passes, against any `CALFEED-\d+`/`Phase 84`/`84-0\d` reference leaking into shipped source, per this project's own CLAUDE.md rule.

### Human Verification Required

See frontmatter `human_verification`. Both items trace to the single deferred `84-08-T3` blocking checkpoint (real phone subscribing via iOS Calendar / Google Calendar / Outlook, calendar naming, refresh latency, webcal handoff, camera QR scan) — deferred to deployment by explicit, recorded operator decision, not silently skipped and not treated as a phase failure.

### Gaps Summary

No gaps. All 17 CALFEED requirements have direct, adversarially-checked evidence in the codebase (source read, not SUMMARY-trusted) plus passing automated tests executed in this verification pass. The tenant-isolation chain specifically requested for scrutiny — fresh per-request membership read (`GroupRepository.GetGroupsForUserAsync`), the second-layer drop with `LogError` (`CalendarSubscriptionService.GetFeedAsync`), and the absence of any `IActiveGroupContext` resolution on the feed path — is confirmed by direct source read, not by trusting the SUMMARY's description of it. The 410/404/retention interaction (CALFEED-04/CALFEED-17) is confirmed to have no hard delete on the request path and exactly one purge path, with boundary-exact unit test coverage. Both Profile layouts were read directly and are at parity, not merely test-asserted to be.

The only open item is the real-device checkpoint, which is inherently outside what a codebase-verification pass (or any automated test) can observe, is already honestly recorded in three separate project ledgers (STATE.md, ROADMAP.md, WINDOWS.md), and is surfaced here as `human_needed` rather than either silently passed or wrongly treated as a blocking gap.

---

_Verified: 2026-09-18T13:52:28Z_
_Verifier: Claude (gsd-verifier)_
