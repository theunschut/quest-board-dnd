---
phase: 77-availability-overview-page
reviewed: 2026-08-29T11:45:00Z
depth: standard
files_reviewed: 31
files_reviewed_list:
  - QuestBoard.Domain/Enums/AvailabilityCellState.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/IEventService.cs
  - QuestBoard.Domain/Models/AvailabilityMember.cs
  - QuestBoard.Domain/Models/EventAvailabilityOverview.cs
  - QuestBoard.Domain/Models/EventAvailabilityRow.cs
  - QuestBoard.Domain/Models/EventWithSignups.cs
  - QuestBoard.Domain/Models/EventsOverviewOptions.cs
  - QuestBoard.Domain/Services/EventService.cs
  - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  - QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/ViewModels/EventViewModels/EventOverviewRowViewModel.cs
  - QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs
  - QuestBoard.Service/ViewModels/EventViewModels/OverviewMemberViewModel.cs
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Events/Index.cshtml
  - QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml
  - QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/wwwroot/css/events-overview.css
  - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
  - QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs
  - QuestBoard.UnitTests/ViewModels/EventsOverviewViewModelMappingTests.cs
findings:
  critical: 1
  warning: 9
  info: 8
  total: 18
status: issues_found
---

# Phase 77: Code Review Report

**Reviewed:** 2026-08-29T11:45:00Z
**Depth:** standard
**Files Reviewed:** 31
**Status:** issues_found

## Summary

The availability overview slice (repository read → domain aggregation → view models → two view surfaces) was reviewed adversarially with emphasis on the five security items called out for this phase.

**The security focus items came back clean, and I verified each rather than taking the code comments at their word:**

1. **No filter bypass.** `EventRepository.GetUpcomingWithSignupsAsync` (`QuestBoard.Repository/EventRepository.cs:132-157`) uses `DbContext.Events` with no `IgnoreQueryFilters()` anywhere in the phase's code. `EventEntity` and `EventSignupEntity` both carry fail-closed filters (`QuestBoardContext.cs`, `activeGroupContext.ActiveGroupId != null && ...`), and EF Core applies the `EventSignupEntity` filter to the `Include`d collection as well, so the member axis cannot be widened by the include. The two-group isolation tests exercise this for events, member columns, counts, an oversized `take`, and the no-active-board case.
2. **`take` is bounded server-side.** `Math.Clamp(take ?? DefaultTake, 1, MaxTake)` runs before the value reaches `.Take()`; the service asks for `take + 1` only. Non-integer or overflowing query strings fail model binding and fall back to the default. (See WR-04 for the one input that makes this throw, and WR-05 for the fact that the clamp is not actually covered by a test that can fail.)
3. **SuperAdmin with no active group short-circuits, does not throw.** `Index` deliberately avoids `RequireActiveGroupId()`, and `GroupSessionMiddleware` redirects an authenticated GET with a null `ActiveGroupId` to `/groups/pick` before the action runs. Even if it did run, the filters yield zero rows.
4. **Output encoding is clean.** No `Html.Raw`, no `@Html.Encode` misuse, no unencoded interpolation. Member display names and event titles all go through Razor's encoder, including inside the `onclick` attributes (which only carry `Url.Action` output for an `int` id).
5. **No N+1.** One round trip, in-memory aggregation, and the unit test asserts `Received(1)` with `take + 1`.

**What is wrong is on the presentation and coverage side, and it is not cosmetic.** The mobile surface ships without the paging control the design contract specifies, so a mobile member cannot see past the first ten events and gets no signal that more exist (CR-01). The desktop "highlight your own column" affordance is dead CSS — it is written without `!important` and is unconditionally overridden by `modern-card.css`'s `!important` cell backgrounds, which the very same stylesheet already works around ten lines earlier for the sticky columns (WR-02). The "Show More" control becomes a no-op link once `take` reaches `MaxTake` (WR-01). And the two tests that appear to cover the clamp cannot fail (WR-05), while the mobile view has no rendering coverage at all (WR-07) — which is precisely why CR-01 shipped unnoticed.

## Critical Issues

### CR-01: Mobile availability overview has no paging control — members cannot see past the first 10 events

**File:** `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:50-95`
**Issue:** The mobile view never reads `Model.HasMore` or `Model.NextTake`. The card loop ends at line 94 and the container closes at line 96 with no "Show More Events" control and no indication that further events exist. The desktop view renders it (`Index.cshtml:79-87`), and the design contract puts it "below the grid/card list" — i.e. on both surfaces (`77-UI-SPEC.md` §7 Paging).

This is not a cosmetic omission. `EventsOverviewOptions.DefaultTake` is 10, and the option's own comment states the motivating case is "a board running several recurring series [that] holds many future occurrences per series". On such a board a mobile member sees exactly ten events, has no control to load more, and cannot even tell that the list is truncated — the data is unreachable on the surface most players use. Mobile views are user-agent-selected, so this cannot be worked around by resizing a desktop browser.

**Fix:** Mirror the desktop block inside the `else` branch, after the `foreach` at line 94 (note WR-01 applies to both surfaces):

```razor
        }

        @if (Model.HasMore && Model.NextTake > Model.Take)
        {
            <div class="d-grid mb-3">
                <a href="@Url.Action("Index", new { take = Model.NextTake })" class="btn btn-primary">
                    <i class="fas fa-chevron-down me-2"></i>Show More Events
                </a>
            </div>
        }
    }
```

## Warnings

### WR-01: "Show More Events" renders as a dead link once `take` reaches `MaxTake`

**File:** `QuestBoard.Service/Controllers/Events/EventsController.cs:44-46`, `QuestBoard.Service/Views/Events/Index.cshtml:79-87`
**Issue:** `NextTake = Math.Min(effectiveTake + options.PageIncrement, options.MaxTake)`. When `effectiveTake == MaxTake` (100 by default) and more events still exist, `HasMore` is `true` while `NextTake == Take`. The view gates the button on `HasMore` alone, so it renders a button linking to `?take=100` — the page the user is already on. Clicking it reloads identical content forever, which reads as a broken page rather than as "you have reached the end".
**Fix:** Gate on growth, not just on existence. Either add a computed flag on the view model:

```csharp
// EventOverviewViewModel
public bool CanShowMore => HasMore && NextTake > Take;
```

and use `@if (Model.CanShowMore)` in both views, or surface a distinct "showing the maximum of N events" note when `HasMore && NextTake == Take`.

### WR-02: The viewer's own-column highlight is dead CSS — overridden by `modern-card.css`

**File:** `QuestBoard.Service/wwwroot/css/events-overview.css:98-103`
**Issue:** `.avail-col-self { background-color: rgba(255, 255, 255, 0.08); }` has specificity `(0,1,0)` and no `!important`. `modern-card.css:232-248` declares `.modern-card .table th { background-color: rgba(139,69,19,0.8) !important; }` and `.modern-card .table td { background-color: rgba(244,228,188,0.85) !important; }`. The `!important` declarations win unconditionally, so the highlight never paints on either the `<th>` (`Index.cshtml:36`) or the `<td>` (`Index.cshtml:67`). The feature the UI contract calls for — "the viewer's own column gets a subtle highlight … so a player can find themselves in a wide grid" (`77-UI-SPEC.md:257`) — is silently absent; only the `(you)` text suffix survives.

This is a self-inflicted miss: the same stylesheet at lines 71-82 documents the exact `!important` problem and works around it for the sticky columns, then does not apply the same treatment 20 lines later.

**Fix:** Match the specificity-and-`!important` shape already used for the sticky columns immediately above:

```css
.modern-card .table th.avail-col-self {
    background-color: rgba(139, 69, 19, 0.65) !important;
}

.modern-card .table td.avail-col-self {
    background-color: rgba(255, 235, 180, 0.95) !important;
}
```

Pick tints that stay distinguishable from the sticky-column and `.table-hover` colours, and verify against a self column that is also being hovered.

### WR-03: Second sticky column's offset is hardcoded to the first column's *minimum* width — the two overlap on wide content

**File:** `QuestBoard.Service/wwwroot/css/events-overview.css:84-92`
**Issue:** `.avail-col-event { left: 0; min-width: 200px; }` and `.avail-col-attendance { left: 200px; ... }`. `min-width` is a floor, not a fixed width: a long event title (up to the 200-character `Event.Title` limit) makes the browser's table layout widen column one well beyond 200px. The attendance column is then pinned at `left: 200px`, i.e. *inside* the event column, and the two sticky columns visibly overlap as soon as the grid is scrolled horizontally — which on a many-member board is the normal viewing mode.
**Fix:** Give the first sticky column a determinate width so the offset stays true:

```css
.avail-col-event {
    left: 0;
    width: 200px;
    min-width: 200px;
    max-width: 200px;
}
```

and let the title cell wrap/ellipsis inside it, or drive the offset from a shared custom property (`--avail-event-col-width`) used by both rules.

### WR-04: `Math.Clamp` throws when `MaxTake` is configured below 1 — every request to the page 500s

**File:** `QuestBoard.Service/Controllers/Events/EventsController.cs:35`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs:18-20`
**Issue:** `Math.Clamp(value, min: 1, max: options.MaxTake)` throws `ArgumentException` when `min > max`. `EventsOverviewOptions` is bound from configuration with `BindConfiguration` and no validation, so an `EventsOverview:MaxTake` of `0` (or any negative, or a non-numeric value that binds to `0`) turns every GET of the page into an unhandled exception rather than a clamped page size. Deployment configuration for this app lives in a server env file, so a typo there is a realistic path to a hard-down page. A negative `PageIncrement` similarly produces a `NextTake` below `Take`, i.e. a "Show More" link that shrinks the list.
**Fix:** Validate the options at startup so a bad value fails loudly at boot rather than per request:

```csharp
services.AddOptions<EventsOverviewOptions>()
    .BindConfiguration(EventsOverviewOptions.SectionName)
    .Validate(o => o.MaxTake >= 1 && o.DefaultTake >= 1 && o.PageIncrement >= 1,
        "EventsOverview take/increment values must all be at least 1.")
    .ValidateOnStart();
```

Defensively clamping the ceiling at the call site (`Math.Clamp(take ?? options.DefaultTake, 1, Math.Max(1, options.MaxTake))`) is an acceptable belt-and-braces addition but is not a substitute for startup validation.

### WR-05: The tests that appear to cover the `take` bound cannot fail

**File:** `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs:237-270`
**Issue:** `Index_TakeAboveMax_IsClampedAndStillReturnsOk` seeds **5** events, requests `?take=100000`, then asserts `rowCount.Should().BeLessThanOrEqualTo(100)`. With only 5 events in the database the assertion holds even if `Math.Clamp` is deleted entirely — the test cannot detect the regression it is named for, and `take` bounding is an explicit security control for this phase. `Index_TakeZeroOrNegative_StillReturnsOk` (lines 256-270) asserts only `200 OK` and never checks that the clamp-to-1 actually produced one row, so it too passes with the clamp removed. `Overview_LargeTakeParameter_DoesNotWidenBeyondActiveBoard` in the tenant test class covers isolation, not bounding.
**Fix:** Seed above the ceiling and assert the boundary:

```csharp
for (var i = 0; i < 105; i++) { await SeedEventAsync($"Clamp Session {i}", DateOnly.FromDateTime(DateTime.Today.AddDays(i + 1))); }
...
var rowCount = html.Split("avail-row-clickable").Length - 1;
rowCount.Should().Be(100);   // exact ceiling, not "<= 100"
```

and for the zero/negative case assert exactly one row renders (`rowCount.Should().Be(1)`).

### WR-06: Mobile card click-through swallows the expanded roster

**File:** `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:62, 76-92`
**Issue:** The whole card carries `onclick="window.location.href=..."`. Only the toggle button calls `event.stopPropagation()` (line 78). The collapse container and every `<li>` inside it are descendants of the card, so once a member expands the roster, any tap on a name, a badge, or the whitespace between them navigates away to the event Details page. The one interaction the mobile design adds — inspecting who answered what — is a minefield of accidental navigations.
**Fix:** Stop propagation at the collapse container so the whole expanded region is inert:

```razor
<div class="collapse mt-2" id="roster-@row.EventId" onclick="event.stopPropagation();">
```

### WR-07: No test renders the mobile availability overview at all

**File:** `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs:67-306`
**Issue:** Every request in the overview test classes uses the default client user agent, which selects the desktop view. `Index.Mobile.cshtml` — a 96-line view with its own stylesheet section, its own legend, its own card list, and its own collapse markup — is never rendered by any test. A missing `@section Styles` target, a missing partial, or (as actually happened) a missing paging control produces no failure. `LayoutNavigationTests` already owns the mechanism (`GetWithUserAgentAsync` + `MobileUserAgent`), and its own new theories exercise the mobile *layout*; the mobile *page* is the gap.
**Fix:** Add mobile-user-agent counterparts to at least the structural facts — rows render, roster collapse renders, empty state renders, and the paging control from CR-01 renders — using a request-level `User-Agent` header rather than the shared client:

```csharp
var request = new HttpRequestMessage(HttpMethod.Get, "/Events");
request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
```

### WR-08: `Index_RendersAllThreeCounts` asserts strings that render regardless of the counts

**File:** `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs:162-186`
**Issue:** The test seeds a deliberate 3-Yes (one unconfirmed) / 2-Maybe shape, then asserts only `Contain("avail-count-headline")`, `Contain("confirmed")` and `Contain("Maybe")`. `avail-count-headline` renders for any non-empty row set; `confirmed` and `Maybe` also appear in the always-rendered legend ("Confirmed maybe", "Confirmed not available"). None of the three numbers the phase exists to compute — total Yes including unconfirmed, the confirmed subset, and the separately-tracked Maybe — is actually verified end to end. The tenant-isolation class already demonstrates the right shape (`Contain("<strong>1</strong> Yes")`).
**Fix:**

```csharp
html.Should().Contain("<strong>3</strong> Yes");
html.Should().Contain("(2 confirmed)");
html.Should().Contain("2 Maybe");
```

### WR-09: Both views index `row.Cells[i]` by `Members.Count` with no guard

**File:** `QuestBoard.Service/Views/Events/Index.cshtml:62-73`, `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:83-90`
**Issue:** The loops run `for (var i = 0; i < Model.Members.Count; i++)` and dereference `row.Cells[i]`. The positional invariant is currently held by `EventService.BuildRow`, but it is enforced nowhere at the boundary the views actually consume — not by the view model, not by the mapper profile, not by a guard in the view. Any future change to the aggregation, the AutoMapper configuration, or a hand-assembled view model turns a display-alignment bug into an unhandled `ArgumentOutOfRangeException` mid-render, i.e. a 500 on a page every member uses, with a half-written response. The comments on `EventAvailabilityRow.Cells` and `EventOverviewRowViewModel.Cells` assert the invariant but nothing checks it.
**Fix:** Degrade instead of throwing — iterate the smaller bound or fall back to `Empty`:

```razor
@for (var i = 0; i < Model.Members.Count; i++)
{
    var member = Model.Members[i];
    var cell = i < row.Cells.Count ? row.Cells[i] : AvailabilityCellState.Empty;
    ...
}
```

## Info

### IN-01: `EventOverviewViewModel.Take` is dead, and its comment is wrong

**File:** `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs:13`
**Issue:** `Take` is populated by the controller but read by no view (`Index.cshtml` uses only `HasMore`/`NextTake`; `Index.Mobile.cshtml` reads neither). The class comment claims it is "the paging state the Show More control and the alignment check both need" — neither consumer exists.
**Fix:** Keep it and use it in the WR-01 gate (`HasMore && NextTake > Take`), which is the better outcome, or remove it and correct the comment.

### IN-02: Stale planning references in `LayoutNavigationTests`

**File:** `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs:8-12`
**Issue:** `/// Nav-visibility tests for NAV-01..06 and D-04 … Tests start RED — the layout gating does not exist until Plan 02 wires GetBoardTypeAsync into _Layout.cshtml/_Layout.Mobile.cshtml.` This is exactly what `CLAUDE.md` ("Code Comments") forbids: requirement IDs, decision IDs and plan numbers in source. It is also factually stale — the gating shipped long ago and the tests are green. Pre-existing, but this phase edited the file and inherited it.
**Fix:** Replace with a plain-language summary, e.g. `/// Asserts which navigation entries each role sees on each board type, for both the desktop and the mobile layout.`

### IN-03: Dead statement in an integration test

**File:** `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs:150`
**Issue:** `_ = eventTwoId;` exists only to silence an unused-variable signal; the second event is seeded for its side effect.
**Fix:** Drop the assignment — `await SeedEventAsync("Empty Cell Session Two", ...);` without capturing the id.

### IN-04: `.legend-card` is a no-op on mobile

**File:** `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:23`
**Issue:** The mobile legend uses `class="card modern-card legend-card"`, but `.legend-card` is defined only in `calendar.css`, which `_Layout.Mobile.cshtml` never loads (it loads `mobile.css`, `modern-card.css`, and the two markdown sheets). The class contributes nothing on mobile. Purely cosmetic (`height: fit-content` plus padding/font tweaks), so the legend still renders.
**Fix:** Either drop the class from the mobile view or copy the three rules into `events-overview.mobile.css`, consistent with the duplication policy that file already documents.

### IN-05: Desktop view fully qualifies the enum five times

**File:** `QuestBoard.Service/Views/Events/Index.cshtml:103, 107, 111, 115, 119`
**Issue:** `QuestBoard.Domain.Enums.AvailabilityCellState.ConfirmedYes` etc., where the mobile view correctly declares `@using QuestBoard.Domain.Enums` (line 1) and writes `AvailabilityCellState.ConfirmedYes`. Inconsistent between two files that are meant to be read as a pair.
**Fix:** Add `@using QuestBoard.Domain.Enums` at the top of `Index.cshtml` and shorten the five references.

### IN-06: Row/card click targets are mouse-only

**File:** `QuestBoard.Service/Views/Events/Index.cshtml:48`, `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:62`
**Issue:** Navigation lives in an `onclick` on a `<tr>`/`<div>` with no `href`, `tabindex`, `role="link"`, or key handler, so keyboard and assistive-technology users cannot reach event Details from this page at all. The design contract knowingly reuses the calendar's existing agenda-entry idiom, so this is a pre-existing pattern rather than a new one — noted so it is a deliberate choice rather than an oversight.
**Fix:** Wrap the event title cell in a real `<a asp-action="Details" asp-route-id="@row.EventId">` so there is at least one focusable path to the same destination, keeping the row `onclick` as a mouse convenience.

### IN-07: New nav theories mutate shared fixture state without restoring it

**File:** `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs:317, 332, 347, 361`
**Issue:** Each new theory assigns `_factory.TestGroupContext.BoardType` and never restores it, while the neighbouring pre-existing test at line 283-303 wraps the same mutation in `try/finally`. Two tests in the class (`Nav_DungeonMaster_CreateEventEntryPresent`, `Nav_Player_CreateEventEntryAbsent`) do not set the board type at all and inherit whatever ran last. Collection parallelism is disabled and the assertions those two make are board-type-agnostic today, so nothing is failing now — it is a latent ordering dependency.
**Fix:** Follow the `try/finally` shape already used in the same file, or reset `BoardType` in an `IAsyncLifetime.DisposeAsync` for the class as the two overview test classes do.

### IN-08: `DateTime.Today` mixes a server-local clock into an otherwise UTC domain, and the "testability" comment does not hold

**File:** `QuestBoard.Domain/Services/EventService.cs:43-45`
**Issue:** `DateOnly.FromDateTime(DateTime.Today)` reads the server's local date, while cancellation and signup timestamps elsewhere in the same feature use `DateTime.UtcNow`. Near midnight (and on any container whose TZ differs from the group's), an event can appear or disappear from "upcoming" a few hours early or late. The comment says the clock read lives in the service "so the repository stays testable against a fixed date", but the service itself remains untestable against a fixed date — the unit test at `EventsOverviewAggregationTests.cs:253` has to recompute `DateTime.Today` to match, which means it asserts the implementation rather than a behaviour.
**Fix:** Inject a clock abstraction (`TimeProvider` is available on .NET 10) and read `DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime)`, which fixes both the TZ inconsistency and makes the boundary genuinely testable.

---

_Reviewed: 2026-08-29T11:45:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
