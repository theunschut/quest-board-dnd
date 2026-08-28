---
phase: 75-event-availability-signups
reviewed: 2026-08-28T10:14:49Z
depth: standard
files_reviewed: 30
files_reviewed_list:
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/IEventService.cs
  - QuestBoard.Domain/Interfaces/IEventSignupRepository.cs
  - QuestBoard.Domain/Interfaces/IEventSignupService.cs
  - QuestBoard.Domain/Models/EventSignup.cs
  - QuestBoard.Domain/Services/EventService.cs
  - QuestBoard.Domain/Services/EventSignupService.cs
  - QuestBoard.Domain/Services/GroupService.cs
  - QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/EventEntity.cs
  - QuestBoard.Repository/Entities/EventSignupEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Repository/EventSignupRepository.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/ViewModels/EventViewModels/EventSignupViewModel.cs
  - QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs
  - QuestBoard.Service/Views/Events/Details.cshtml
  - QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
  - QuestBoard.UnitTests/Services/GroupServiceTests.cs
findings:
  critical: 1
  warning: 3
  info: 2
  total: 6
status: issues-found
---

# Phase 75: Code Review Report

**Reviewed:** 2026-08-28T10:14:49Z
**Depth:** standard
**Files Reviewed:** 30
**Status:** issues_found

## Summary

Phase 75 wires an EventSignup data tier, membership-synced backfill/cleanup, controller write actions, and a rendered availability surface. The core multi-tenancy story is solid: `EventSignupEntity` carries its own fail-closed query filter (`es.Event.GroupId == activeGroupContext.ActiveGroupId`), the write actions (`SetAvailability`, `Withdraw`) re-verify board ownership with a second explicit `EventIsOnActiveBoard` check independent of the read-side filter, ownership is enforced entirely from `User` (no user/signup id ever accepted from the request), and the two production bugs plan 75-05 found and fixed (the disconnected `EventSignupEntity.Event`/`EventEntity.Signups` relationship, and the missing `ModelState.IsValid` check in `SetAvailability`) are both correctly and completely fixed in the code as shipped. Board-type and past-date rules are enforced server-side, not just in markup, exactly as the summaries claim.

However, this review found one concrete, previously-unverified bug that defeats a documented safety mechanism, plus a few smaller robustness and hygiene gaps.

## Critical Issues

### CR-01: The Remove-Member confirmation dialog is broken JavaScript and will never prompt

**File:** `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml:77` and `QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml:66`

**Issue:** Both files render:

```html
onsubmit="return confirm('Remove this member from the group? Their availability answers for this board&#39;s events will be deleted and cannot be recovered.');"
```

`&#39;` is an HTML numeric character reference. The browser's HTML tokenizer decodes character references while building an attribute's value — this happens for every attribute, including inline event-handler attributes like `onsubmit` — *before* that value is ever handed to the JavaScript engine as source text. So `&#39;` and a literal `'` character produce an identical decoded string. After decoding, the actual JavaScript source the browser tries to compile is:

```js
return confirm('Remove this member from the group? Their availability answers for this board's events will be deleted and cannot be recovered.');
```

The single-quoted string literal terminates at `...this board'`, leaving `s events will be deleted and cannot be recovered.');` as trailing, syntactically invalid tokens. This is a JavaScript syntax error. When an inline event handler fails to compile, the browser treats it as having no handler (nothing is logged to block submission, and no exception propagates to application code) — `onsubmit` never runs, `confirm()` never shows, and the form submits immediately.

**Why it matters:** This confirmation is the only safeguard the phase adds before an irreversible, destructive action — removing a member deletes every event-signup row they hold on that board (past and future, answered and automatic), per `GroupRepository.RemoveMemberAsync`. Because the dialog silently fails to render, a Dungeon Master or Admin clicking "Remove Member" gets **no warning at all** before that data is permanently deleted. This is precisely the scenario `75-05-SUMMARY.md`/`75-VALIDATION.md` routed to "Manual-Only Verification" ("Both confirmation dialogs read correctly") — a check that, per this review, either was never actually performed in a real browser or was performed and missed the silent JS failure.

**Fix:** Use a JavaScript escape sequence instead of an HTML entity — the backslash is not itself an HTML character-reference trigger, so it passes through HTML decoding unchanged and is interpreted correctly by the JS parser:

```html
onsubmit="return confirm('Remove this member from the group? Their availability answers for this board\'s events will be deleted and cannot be recovered.');"
```

or, more robustly, avoid the apostrophe entirely (also sidesteps the same class of mistake anywhere else this message is duplicated):

```html
onsubmit="return confirm('Remove this member from the group? Their availability answers for events on this board will be deleted and cannot be recovered.');"
```

Apply the identical fix to both files to preserve the "identical wording" requirement from `75-02-SUMMARY.md`, then manually verify in a real browser (not just that the fact tests pass — they only assert markup presence, not that the inline script compiles).

## Warnings

### WR-01: Unhandled `ArgumentException` on a delete/write race in `SetAvailability`

**File:** `QuestBoard.Repository/EventSignupRepository.cs:17-21`, called from `QuestBoard.Service/Controllers/Events/EventsController.cs:255`

**Issue:** `SetAvailability` fetches the event once via `eventService.GetEventWithDetailsAsync` and then, several checks later, calls `eventSignupService.SetAvailabilityAsync`, which re-probes existence with `DbContext.Events.AnyAsync(...)` and throws a bare `ArgumentException("Event not found", nameof(eventId))` if the event no longer exists. If a Dungeon Master deletes the event in the window between the controller's read and this write (plausible — no locking, two independent requests), the action lets that `ArgumentException` propagate unhandled instead of returning a normal 404/400. The fetch endpoint (`setAvailability()` in `Details.cshtml`) expects `res.text()` on failure to show in an `alert()`; an unhandled exception instead produces whatever the app's global exception handling renders (likely an HTML error page), which the `alert()` would then display verbatim as a wall of markup.

**Fix:** Catch the narrow race in the controller (or have the repository return a bool/nullable result instead of throwing) and return `NotFound()`:

```csharp
try
{
    await eventSignupService.SetAvailabilityAsync(id, currentUser.Id, availability, token);
}
catch (ArgumentException)
{
    return NotFound();
}
```

### WR-02: Stale entity comment now contradicts what the phase shipped

**File:** `QuestBoard.Repository/Entities/EventSignupEntity.cs:6-7`

**Issue:** The class comment still reads:

```csharp
// This table carries no GroupId of its own and is tenant-scoped through its required
// Event navigation. No code reads or writes it yet.
```

"No code reads or writes it yet" was true when Phase 74 shipped the table but is now false — Phase 75 added `EventSignupRepository`, `GroupRepository.AddMemberAsync`/`RemoveMemberAsync`, and `EventRepository.AddWithCampaignFanOutAsync`, all of which read and write this table. A future maintainer skimming this comment could reasonably (and incorrectly) conclude the table is still dead/unused.

**Fix:** Update the comment to reflect the current state, e.g.:

```csharp
// This table carries no GroupId of its own and is tenant-scoped through its required
// Event navigation (see the HasQueryFilter in QuestBoardContext).
```

### WR-03: `IEventSignupRepository`/`IEventSignupService` inherit unguarded generic write methods that bypass the cross-board check

**File:** `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs:6`, `QuestBoard.Domain/Interfaces/IEventSignupService.cs:6`

**Issue:** Both interfaces extend `IBaseRepository<EventSignup>` / `IBaseService<EventSignup>`, which contribute generic `AddAsync`, `UpdateAsync`, `RemoveAsync`, and `GetByIdAsync` members (see `BaseRepository<TModel, TEntity>`). `BaseRepository.AddAsync` maps the model straight onto a new entity and saves it with **no existence/board check at all** — the careful "probe `DbContext.Events` for the active board before inserting" logic lives only in `EventSignupRepository.SetAvailabilityAsync`, not in the inherited generic path. Nothing in the current controller calls the generic methods (only `SetAvailabilityAsync`/`WithdrawAsync`/`GetRosterForEventAsync` are used), so this is not exploited today. But the interface surface itself invites a future caller to reach for `AddAsync`/`UpdateAsync` (they're right there on `IEventSignupService`) and silently reintroduce a cross-board insert, since the read-side query filter — the only other protection — does not constrain writes. This mirrors an existing project-wide pattern (e.g. `PlayerSignupRepository`), so it isn't unique malpractice by this phase, but it is a new surface this phase adds and is worth tracking.

**Fix:** Consider not exposing `IBaseRepository<EventSignup>`/`IBaseService<EventSignup>` on these two interfaces at all, since every current and documented use goes through the three narrow methods — an interface that only exposes what's actually safe to call removes the temptation entirely. If the generic surface must stay for consistency with sibling repositories, add a one-line comment on the interface warning that `AddAsync`/`UpdateAsync` do not perform the board check `SetAvailabilityAsync` does.

## Info

### IN-01: `withdrawAvailability()` moved out of the shared `@section Scripts` block

**File:** `QuestBoard.Service/Views/Events/Details.cshtml:110-136`

**Issue:** Every other write-action script in this view (and the `changeVote`/`revokeSignup` idiom on `Quest/Details.cshtml`) lives in the shared `@section Scripts` block at the bottom of the file. `withdrawAvailability()` was deliberately moved inline next to its sole caller, inside the `@if (Model.IsOneShotBoard && Model.HasOwnSignup)` block, per `75-04-SUMMARY.md`'s documented deviation. This is a reasonable, test-driven fix (the function previously existed unconditionally even when its only caller never rendered, which made the control's own conditional visibility untestable from the response body) — not a defect. Flagged only so future contributors editing this page are aware the script layout is deliberately inconsistent with the established idiom for a documented reason, not an oversight to "clean up."

**Fix:** None required. Consider a short code comment cross-referencing this decision is already present in the view (it is) — no further action needed.

### IN-02: Redundant `Enum.IsDefined` check after the `ModelState.IsValid` guard

**File:** `QuestBoard.Service/Controllers/Events/EventsController.cs:228-248`

**Issue:** `SetAvailability` checks `ModelState.IsValid` first (added by plan 75-05 specifically because ASP.NET Core's enum model binder rejects an undefined numeric value like `99` during binding) and then, further down, still checks `Enum.IsDefined(typeof(VoteType), availability)`. Given the `ModelState` check already catches an undefined value before model binding produces one, the second check is likely unreachable for any request that makes it past the first. This isn't a bug — belt-and-suspenders validation is defensible — but it is slightly confusing to a reader trying to determine which check is actually load-bearing.

**Fix:** No action required; if revisited, a short comment noting `Enum.IsDefined` is retained as defense-in-depth (e.g. for `[Flags]`-style edge cases or future binder changes) would remove the ambiguity.

## Pre-Existing Issues (Not Attributed to Phase 75)

- `QuestBoard.Service.csproj` already references `Microsoft.EntityFrameworkCore.Tools` before this phase began (needed for the `dotnet ef` CLI), which is a real violation of the "EF packages belong only in `QuestBoard.Repository`" rule in `CLAUDE.md`. Confirmed pre-existing per `75-01-SUMMARY.md`'s own note (present in the commit immediately prior to this phase's first commit). Not introduced or worsened by any of the 30 files in this phase's scope.

---

_Reviewed: 2026-08-28T10:14:49Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
