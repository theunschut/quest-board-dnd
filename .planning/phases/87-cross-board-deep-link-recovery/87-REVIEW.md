---
phase: 87-cross-board-deep-link-recovery
reviewed: 2026-09-22T00:00:00Z
depth: standard
files_reviewed: 28
files_reviewed_list:
  - QuestBoard.Domain/Enums/CrossBoardLookupKind.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs
  - QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs
  - QuestBoard.Domain/Models/CrossBoardTarget.cs
  - QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs
  - QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs
  - QuestBoard.IntegrationTests/Helpers/CrossBoardWebApplicationFactory.cs
  - QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs
  - QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs
  - QuestBoard.IntegrationTests/Middleware/CrossBoardTestHarnessTests.cs
  - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
  - QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs
  - QuestBoard.Repository/CrossBoardLinkRepository.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Service/Constants/TempDataKeys.cs
  - QuestBoard.Service/Controllers/GroupPickerController.cs
  - QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs
  - QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs
  - QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Services/ActiveBoardSwitcherService.cs
  - QuestBoard.Service/Services/IActiveBoardSwitcher.cs
  - QuestBoard.Service/Views/Shared/_Toasts.cshtml
  - QuestBoard.Service/wwwroot/css/modern-card.css
  - QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs
  - QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs
  - QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 87: Code Review Report

**Reviewed:** 2026-09-22T00:00:00Z
**Depth:** standard
**Files Reviewed:** 28
**Status:** issues_found

## Summary

This phase adds one narrow, well-audited escape hatch (`CrossBoardLinkRepository`) from the
application's tenant query filters, gated end-to-end by a closed registry of routes
(`CrossBoardLinkRegistry`), a membership-pinned resolver (`CrossBoardLinkResolverService`), and a
pipeline-ordered middleware (`CrossBoardDeepLinkMiddleware`) that repoints the session only for
requests that look like a real top-level browser navigation.

I traced the full call chain — route parsing (`CrossBoardRouteTarget`), registry lookup
(`CrossBoardLinkRegistry`), resolution (`CrossBoardLinkResolverService`), the repository's
membership-pinned queries (`CrossBoardLinkRepository`), the session write
(`ActiveBoardSwitcherService`), and the two write sites (`GroupPickerController.Index`,
`CrossBoardDeepLinkMiddleware`) — against the locked decisions in the brief (D-12 no SuperAdmin
branch, D-13 no membership/existence oracle, GET/HEAD-only, no silent session repoint from a
non-navigation request) and against the EF Core global-filter configuration in
`QuestBoardContext`. I did not find a violation of any of those locked decisions: the resolver
takes no role, every repository query pins the caller's own membership set inside the predicate,
`IgnoreQueryFilters()` is confined to the one allow-listed class (and the architecture test that
enforces this — `CrossBoardIgnoreQueryFiltersSeamTests` — genuinely scans production source rather
than a fixed list of "known good" files), the middleware's Fetch-Metadata gate runs before any
header-independent 404/DB work, and pipeline placement (before `UseAuthorization`, after
`UseAuthentication`) is itself pinned by
`CrossBoardAuthorizationBoundaryTests.PlayerOnActiveBoard_DungeonMasterOnTargetBoard_ReachesTargetBoardsQuestEditPage`.

I also independently confirmed (not asserted by any test in this phase) that `UserGroupEntity` and
`GroupEntity` carry no `HasQueryFilter` at all in `QuestBoardContext`, which affects how one method
in the audited allow-list should be read (see IN-02 below).

Three findings below don't rise to blocker status under the phase's own stated review scope (the
filter bypass itself is not to be reported unless wider than one class, unguarded, or reachable
outside the seam test's coverage — none of that applies here), but are worth fixing for
maintainability and to keep the registry's own documented invariant true in practice.

## Warnings

### WR-01: Cross-board registry key omits MVC area, so the "closed table" invariant is not actually area-safe

**File:** `QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs:14-29`, `QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs:69-78`

**Issue:** `CrossBoardLinkRegistry`'s own doc comment states: "A route absent from this table can
never be resolved across boards at all -- adding an action to the application later never silently
gains the ability to repoint a viewer's session." The registry key, however, is only
`"{controller}/{action}"` (case-insensitive) — it never looks at the `area` route value.
`CrossBoardRouteTarget.TryFromRouteValues` reads `context.GetRouteValue("controller")` and
`("action")` but ignores `context.GetRouteValue("area")`, and `TryFromLocalUrl` has no concept of
area at all.

The application already has an MVC area (`Areas/Platform`, containing `GroupController` and
`UsersController`) registered via `MapAreaControllerRoute` alongside the default route. Today there
is no name collision between a Platform-area controller/action and a registered
`CrossBoardLookupKind` mapping, so this is not currently exploitable. But the registry's stated
guarantee ("adding an action later never silently gains this ability") does not actually hold: if a
future area (or the existing Platform area) ever gains a controller/action pair that happens to
share a name with one of the nine registered controllers (`Quest`, `QuestLog`, `Events`,
`Characters`, `Contacts`, `Shop`, `ShopManagement`, `Series`, `ContactCategoryManagement`,
`DungeonMaster`) and a registered action (`Details`, `Edit`, `Manage`, `CreateFollowUp`,
`EditRecap`, `Profile`, `EditProfile`), that unrelated area route would silently start resolving
through the wrong `CrossBoardLookupKind` — e.g. an id meant for an area-scoped entity being looked
up against `QuestEntity`. Because the repository still pins the caller's membership set, this
would not expose data outside the caller's own boards, but it would misbehave: either an
unintended board switch keyed off a coincidental id match, or a false 404 for a route that should
have worked. This is exactly the kind of "route that quietly starts (or stops) resolving" the
registry's own unit tests (`CrossBoardLinkRegistryTests`) are trying to guard against, but neither
the production code nor the tests account for area.

**Fix:** Include the area in the composite key (normalizing a null/empty area to a constant, since
most of the registered routes are area-less):

```csharp
// CrossBoardRouteTarget.TryFromRouteValues
var area = context.GetRouteValue("area") as string;
...
if (int.TryParse(idRaw, out var id)
    && CrossBoardLinkRegistry.TryGetLookupKind(area, controller, action, out var kind))

// CrossBoardLinkRegistry
private static readonly Dictionary<string, CrossBoardLookupKind> Routes = new(StringComparer.OrdinalIgnoreCase)
{
    [$"/{ControllerNameOf<QuestController>()}/{nameof(QuestController.Details)}"] = CrossBoardLookupKind.Quest,
    // ...
};

internal static bool TryGetLookupKind(string? area, string? controller, string? action, out CrossBoardLookupKind kind)
{
    if (controller == null || action == null) { kind = default; return false; }
    return Routes.TryGetValue($"{area}/{controller}/{action}", out kind);
}
```

(`TryFromLocalUrl` cannot recover an area from a bare path without a routing lookup, but it could
at minimum be restricted to already-known area-less controllers, or documented as accepting only
the default route's shape — which is true today but not guaranteed by the code itself.)

## Info

### IN-01: `TempDataKeys` doc comment omits `GroupPickerController` as a second writer

**File:** `QuestBoard.Service/Constants/TempDataKeys.cs:4-10`

**Issue:** The class doc comment says the three keys are "Written by the cross-board deep-link
middleware and read by the shared toast partial -- two places that cannot see each other's
literals, so both sides reference this file rather than a repeated string." In fact
`GroupPickerController.Index` also writes `TempDataKeys.BoardSwitchTargetName` directly
(`GroupPickerController.cs:56`) for the "picker skips straight to the page" flow. The comment's
"two places" framing under-documents the actual write sites, which could mislead a future
maintainer auditing "everywhere this one-shot banner gets set" (e.g. when deciding whether it's
safe to rename a key, or when trying to reason about whether the previous-group fields could ever
be populated by the picker path — they currently cannot, but that fact lives only in a comment
inside the controller, not here).

**Fix:** Update the comment to name both writers, e.g.: "Written by the cross-board deep-link
middleware (all three keys) and by `GroupPickerController.Index`'s return-URL skip path
(target-name only, since there is no previous board to offer a way back to) -- read by the shared
toast partial."

### IN-02: `ResolveSharedBoardIdsForUserAsync`'s `IgnoreQueryFilters()` call bypasses a filter that doesn't exist

**File:** `QuestBoard.Repository/CrossBoardLinkRepository.cs:50-56`

**Issue:** `ResolveSharedBoardIdsForUserAsync` calls `dbContext.UserGroups.IgnoreQueryFilters()`.
Checking `QuestBoardContext.OnModelCreating`, neither `UserGroupEntity` nor `GroupEntity` has a
`HasQueryFilter` registered at all (unlike `QuestEntity`, `ShopItemEntity`, `CharacterEntity`,
`ContactEntity`, `EventEntity`, `EventSeriesEntity`, etc., which all carry an
`ActiveGroupId`-scoped filter). Membership rows are intentionally queryable without an active board
selected — that's how the group picker itself works. So this particular `IgnoreQueryFilters()`
call bypasses nothing; it is a no-op with respect to filtering.

This isn't a behavioral bug (the method's actual safety comes from the `memberGroupIds.Contains(...)`
predicate, which is correct regardless), but it does slightly overstate what's being audited: the
class-level comment says "Each method pins the caller's own membership set inside the query
predicate before it ever touches the filtered tables" — `UserGroups` isn't a filtered table, so
this method doesn't actually need to appear on the `IgnoreQueryFilters()` allow-list surface at
all, and a future reader scanning `CrossBoardIgnoreQueryFiltersSeamTests`'s allow-list for "which
tenant filters does this class bypass" would get a slightly inflated picture for this call site.

**Fix:** Either remove the redundant `.IgnoreQueryFilters()` call here (it changes nothing
functionally, since there's no filter to ignore), or add a one-line comment noting it's
defensive/future-proofing against a filter being added to `UserGroupEntity` later, so the
discrepancy between "why this call exists" and "what it currently bypasses" is explicit rather than
implied.

---

_Reviewed: 2026-09-22T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
