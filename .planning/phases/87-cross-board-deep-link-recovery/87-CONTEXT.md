# Phase 87: Cross-Board Deep Link Recovery - Context

**Gathered:** 2026-09-21
**Status:** Ready for planning

<domain>
## Phase Boundary

A member who follows a link to a page on a board they belong to **lands on that page, on that
board**, instead of the empty 404 they get today because a different board — or no board — is
selected in their session. The board is switched automatically, and a one-shot banner on the landed
page says which board they were moved to and offers a way back.

Two request shapes are in scope, and they are deliberately kept as separate code paths:

1. **Wrong board active.** A new middleware, sitting directly after `GroupSessionMiddleware`,
   resolves the URL's target board and repoints the session before the action runs.
2. **No board active** (fresh login, expired session). `GroupPickerController.Index` already
   receives the `returnUrl`; when that URL resolves to exactly one board the viewer belongs to, it
   skips the picker entirely rather than making the viewer guess which board the link lives on.

Both call one shared resolver and one shared active-board switcher. The resolver is the phase's only
new read across the tenancy boundary: a closed `(controller, action)` registry, membership-pinned,
returning a board id and nothing else.

**Not in this phase:** any change to what a non-member sees (byte-identical to today); writes —
POST and every other non-idempotent verb behave exactly as they do now; board-qualified routes;
a rendered 404/error page; restoring `URL` to the calendar feed; board-qualifying the URLs emitted
by email jobs.

</domain>

<decisions>
## Implementation Decisions

### Landing behaviour

- **D-01: Auto-switch and land on the page — no confirm step.** The viewer arrives on the requested
  page with the board already switched. This is a deliberate revision of Phase 82 D-11, which
  rejected silent switching for the Agenda's row actions. The operator's complaint is the friction
  of working across boards, and a confirm click on every cross-board link is that friction.

  The Agenda's own confirm-then-switch modal (`Views/Agenda/Index.cshtml:192-250`) stays as it is.
  It is a *list* where the viewer is choosing among boards; a deep link is a viewer who has already
  chosen. Revising D-11 for deep links does not revise it for the Agenda.

  Rejected: confirm-then-switch (the click being complained about); reusing the group picker for the
  wrong-board case (the roadmap warns against blurring it into the no-board path).

- **D-02: Auto-switch fires only on a real top-level navigation, gated on request headers.**
  Require `Sec-Fetch-Dest: document` **and** `Sec-Fetch-Mode: navigate`, and refuse when any
  prefetch hint is present (`Sec-Purpose`, `Purpose`, `X-Moz`). A request that fails the gate is
  **not** answered differently — it falls through to exactly today's behaviour, the bare 404.

  This closes the roadmap's "repointing a session as a side effect of a GET" risk directly: a
  browser prefetch, a prerender, a link unfurl or a crawler cannot move the viewer's board. It also
  closes a vector the roadmap did not name — without the gate, `<img src="…/Quest/Details/42">` on
  any page the viewer visits would silently repoint their board cross-site; that request arrives as
  `Sec-Fetch-Dest: image` and is refused.

  **The fallback is today's behaviour, so no client regresses.** A browser old enough to send no
  `Sec-Fetch-*` headers at all (pre-2019 Chrome, pre-2021 Firefox, pre-2023 Safari) gets the 404 it
  already gets. This was chosen over a confirm-interstitial fallback specifically to avoid building
  a second user-facing surface plus its `.Mobile.cshtml` twin for a near-nonexistent client — twin
  misses are a recorded failure mode in this codebase (Phases 43, 54, 72).

  Side benefit worth keeping: integration tests must set these headers explicitly, so the gate is
  visible and directly testable rather than implicit.

  **Claude's discretion** — the operator deferred this one ("whatever is best").

- **D-03: A one-shot banner on the landed page, carrying a switch-back control.** Wording along the
  lines of "Switched to *Board X* to open this — switch back to *Board Y*". TempData-style: shown
  once, gone on the next navigation. No new session key to keep in sync across every join, leave and
  switch path.

  Rejected: informational-only (leaves the viewer to find Switch Group themselves); sticky-until-
  dismissed (needs a session flag, a dismiss endpoint, and clearing on every other switch path).

- **D-04: Switch-back posts to `SelectGroup` with no `returnUrl`.** `RedirectToLocal` then falls
  through to the app's normal post-switch destination — the same place Switch Group sends you today.

  **This is a correctness requirement, not a preference.** Switch-back must not return to the
  current URL: that page belongs to the board we just left, so auto-switch would immediately re-fire
  and bounce the viewer forward again. Dropping the `returnUrl` makes the loop structurally
  impossible rather than guarded against.

  Rejected: threading a stashed previous URL or `Referer` (absent for email links — the single most
  important case for this phase — and needs new session state).

- **D-05: When the picker is reached with a `returnUrl` that resolves, skip the picker.** If the
  `returnUrl` resolves to exactly one board the viewer belongs to, switch to it and go straight to
  the page. When it does not resolve (`/quests`, `/shop`, a non-member's entity, a nonexistent id),
  show today's picker unchanged.

  **Raised by the operator during discussion as a separate live pain point**, and it is a genuinely
  distinct path from D-01's: session expires, picker appears with `returnUrl=/Quest/Details/42`, the
  viewer picks the wrong board, and `SelectGroup` redirects them to a page that lives on the other
  one. Today they are punished for not remembering which board they were on.

  This belongs in `GroupPickerController.Index`, not only in the middleware — that is where the
  `returnUrl` and the membership list already meet, and it already has an auto-select precedent in
  its `groups.Count == 1` branch.

### Where resolution lives

- **D-06: A new middleware registered directly after `GroupSessionMiddleware`.** It reads route
  values (populated by `UseRouting` at `Program.cs:320`), resolves the target board, repoints the
  session, sets the banner, and lets the **same request continue** — no redirect, no second round
  trip.

  This works because `ActiveGroupContextService.ActiveGroupId` reads Session on every access with no
  caching, and `QuestBoardContext`'s filters reference the *service* rather than a captured value —
  there is a `CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here`
  comment at `QuestBoardContext.cs:391-393` pinning exactly that. Repointing session before the
  action runs means the action's own queries return the row.

  **Placement before `UseAuthorization` (`Program.cs:325`) is required, not incidental.** See D-18.

  Rejected: intercepting `NotFoundResult` in a result filter (fires on every legitimate 404, costs a
  second round trip, and cannot distinguish a cross-board 404 from a role-check 404 — this codebase
  deliberately returns 404 for both, per Phase 49/55); an explicit helper at each `NotFound()` site
  (~84 sites across 13 controllers, and a missed one is an invisible gap); board-qualified routes
  (rewrites every URL the app emits and does nothing for links already in someone's mailbox, which
  is the case this phase exists for).
  — **Reversibility:** costly — the registry and resolver are self-contained, but the pipeline slot
  and its ordering relative to `UseAuthorization` become load-bearing for every board-scoped route.

- **D-07: Two middlewares, one shared resolver.** The wrong-board middleware is its own class with
  its own tests; `GroupSessionMiddleware`'s existing null-board branch and
  `GroupPickerController.Index` call the same resolver service.

  This is what satisfies the roadmap's "a wrong-board path added beside it must stay distinguishable
  from a missing-board one" — separate classes, separate tests — while still keeping exactly one
  cross-board lookup seam. Rejected: a third branch inside `GroupSessionMiddleware` (merges the two
  paths the roadmap wants kept apart); running the new middleware first (reorders an already-tested
  gate so every existing `GroupSessionMiddleware` test now runs behind new behaviour).

- **D-08: Extract a shared active-board switcher.** The three session writes
  (`ActiveGroupId`, `ActiveGroupName`, `ActiveGroupValidatedAtUtc`) move out of the controllers into
  one small service.

  Phase 82 D-11 locked in "reuse `SelectGroup` rather than inventing a second way to set the active
  board", and middleware cannot POST to itself. Extraction keeps that invariant literally true.
  **It has three callers, not two** — the discussion found that `GroupPickerController.Index:33-35`
  already writes the three keys directly in its single-group auto-select branch, alongside
  `SelectGroup:57-59`. The new middleware is the third.

  Getting this wrong has a specific consequence: forgetting `ActiveGroupValidatedAtUtc` on the new
  path would make `GroupSessionMiddleware` re-validate membership on the very next request.

  Rejected: duplicating the writes in the middleware (exactly the second mechanism D-11 exists to
  prevent); redirecting through a GET-accepting `SelectGroup` (a plain GET changing state — the
  thing D-02's gate exists to stop).

### The cross-board lookup seam

- **D-09: A closed `(controller, action)` registry.** `(Quest, Details)` → quest lookup,
  `(Events, Details)` → event lookup, and so on. A route absent from the registry never resolves.

  Opt-in by construction: no action added later silently gains cross-board powers. It also handles
  actions whose `id` is not the controller's own entity — `QuestController` has several where `id`
  is a signup or a vote, and a controller-level mapping would resolve those to a board silently
  rather than failing.

  Rejected: controller-level mapping (wrong for those actions, and wrong quietly); an attribute on
  each action (scatters the list across 13 controllers with no single place to audit what the escape
  hatch covers).

- **D-10: The lookup returns a board id and nothing else.** Projected in SQL:
  `.IgnoreQueryFilters().Where(x => x.Id == id && memberGroupIds.Contains(x.GroupId)).Select(x => (int?)x.GroupId)`.

  The bypass can only ever return a board the viewer already belongs to. It structurally cannot leak
  a title, a name, or any row data, even if misused later — "what can this leak" has the answer
  "nothing", permanently. Shape follows Phase 82 D-14 (one query, membership set pinned in the
  predicate), explicitly **not** `QuestRepository.GetQuestsForTomorrowAllGroupsAsync`
  (`QuestRepository.cs:267-270`), a bare `IgnoreQueryFilters()` with no group predicate.

  Rejected: returning the entity for reuse (saves one round trip, destroys the leak-nothing
  property); returning the board name for the banner (the name is what Phase 82 D-15 treats as
  sensitive; read it through the existing membership query that already returns names).
  — **Reversibility:** costly — widening the return type later means re-auditing every call site
  against the leak-nothing guarantee this decision rests on.

- **D-11: Containment is an allowlist architecture test plus one dedicated repository class.**
  Mirror `AmbientClockSeamTests`: a closed list of files permitted to call `IgnoreQueryFilters()`,
  so a new call site fails the build until added deliberately. There are **7 call sites today**
  across `EventSignupRepository.cs:91`, `EventRepository.cs:172`, `GroupRepository.cs:135` and
  `:147`, and `QuestRepository.cs:270` and `:293`. Separately, every cross-board id→board lookup
  lives in one dedicated repository class so the whole escape hatch is auditable in one file.

  This answers the roadmap's top risk — "not an `IgnoreQueryFilters()` that spreads by copy-paste" —
  with a mechanism rather than an intention. Behavioural tests alone were rejected: nothing would
  stop a future bare `IgnoreQueryFilters()` elsewhere, which is how `QuestRepository.cs:270` came to
  exist.

- **D-12: No SuperAdmin branch — the resolver pins to the viewer's own `UserGroups` memberships,
  read fresh per request.** A SuperAdmin following a link to a board they are not a member of gets
  the same 404 as anyone else.

  `SelectGroup` and `GroupSessionMiddleware` both special-case SuperAdmin today, so this is a
  deliberate divergence. It follows the operator override recorded in `.planning/PROJECT.md` from
  Phase 55 — *"SuperAdmin's board-viewing experience must be structurally identical to a normal
  user's — no role-based 'sees everything' escape hatch on any group-scoped route"* — and matches
  Phase 82 D-10, which made the same call for the agenda's cross-board read.

### Oracle parity

- **D-13: Parity is structural, not maintained.** A non-member asking for an entity that exists and
  any viewer asking for an id that does not exist run the **identical** code path: same resolver,
  same query shape, both return null, both fall through to the same `NotFound()`. There is no
  branch, so there is no response difference and no timing difference to measure.

- **D-14: The bare 404 stays exactly as it is.** `Program.cs` registers no `UseStatusCodePages`, so
  an unresolvable link is an empty browser 404 today and remains one. After this phase a member
  following a cross-board link never reaches it; only a genuinely deleted entity does, unchanged
  from today. A rendered error page is a deferred idea, not a cut feature.

- **D-15: One paired equivalence test, not two separate 404 assertions.** A single test issues both
  requests and asserts the responses match on status, body and headers. Two independently passing
  404 tests can describe two *different* 404s and nothing notices; a paired test fails the moment
  anyone adds a distinguishing branch.

### Scope — which routes participate

- **D-16: All 18 board-scoped top-level GET routes that take an id.**

  **Read (8):** `Quest/Details`, `Events/Details`, `Characters/Details`, `Contacts/Details`,
  `QuestLog/Details`, `Shop/Details`, `Series/Details`, `DungeonMaster/Profile`.

  **Edit/manage (10):** `Quest/Edit`, `Quest/Manage`, `Quest/CreateFollowUp`, `Events/Edit`,
  `Characters/Edit`, `Contacts/Edit`, `QuestLog/EditRecap`, `ShopManagement/Edit`,
  `ContactCategoryManagement/Edit`, `DungeonMaster/EditProfile`.

  Edit and manage routes are included because a DM sharing an edit link with a co-DM on the other
  board hits the identical 404 — the same complaint in a different suit. The marginal cost per route
  is one registry line plus one projection per entity type, and several routes share a table.

  **The six image subresources are excluded for free** — `Characters/GetProfilePicture`,
  `Characters/GetCroppedPicture`, `Contacts/GetContactImage`, `Contacts/GetCroppedContactImage`,
  `DungeonMaster/GetDMProfilePicture`, `DungeonMaster/GetOriginalDMProfilePicture` arrive as
  `Sec-Fetch-Dest: image` and fail D-02's gate. They do not need to resolve: once the page has
  switched the board, the images that page then requests resolve normally. The same applies to
  `Shop/Details`'s `isModal` AJAX variant (`Sec-Fetch-Dest: empty`) while its full-page variant works.

- **D-17: The two `DungeonMaster` routes resolve only when the answer is unambiguous.** Switch when
  the target user is in exactly one of the viewer's boards and it is not the active one. If they
  share several boards, or the target is already in the active board, no switch and today's
  behaviour stands.

  `DungeonMasterController.Profile` (`DungeonMasterController.cs:20-49`) is a mixed page and needed
  checking: bio, name and picture come from `dmProfileService.GetProfileByUserIdAsync` and are
  board-independent by design, **but** `questService.GetQuestsByDungeonMasterAsync` runs under the
  query filter, so the quest list is board-scoped — and there is an explicit
  `if (!await IsTargetInActiveGroupAsync(id)) return NotFound();` gate. So the page does participate
  in the problem. The ambiguity is real only in the many-shared-boards case, which is a rule, not a
  reason to exclude the route. Note `EditProfile` takes `int? id` — a null id is self-edit and never
  resolves.

- **D-18: A resolved link that the page will not serve is still resolved — the viewer is switched
  and then sees whatever the page says.** A cancelled event, an archived shop item, or a
  `DungeonMasterOnly` page the viewer is only a Player on: the resolver answers "which board owns
  this id", and the page answers everything else.

  This keeps per-entity visibility rules and authorization policy out of the tenancy bypass, which
  is the whole reason it can be audited in one file. The alternatives were teaching each lookup the
  entity's visibility predicate (duplicating business logic into the bypass, needing permanent
  sync with every controller) and switching back on failure (a filter undoing session writes — new
  machinery for a rare case).

  **The role case is why D-06's placement before `UseAuthorization` is mandatory.** A viewer who is
  a Player on Board A and a DM on Board B following `/Quest/Edit/42` must be judged with Board B's
  role. Verified safe: `DungeonMasterHandler` reads `activeGroupContext.ActiveGroupId` live and does
  a fresh `userService.GetGroupRoleAsync` lookup — there is no cached role claim to go stale when
  the board changes mid-request.

### Writes

- **D-19: GET and HEAD only — an explicit method check, not an inherited one.** A cross-board POST
  behaves exactly as it does today; the action's own checks refuse it.

  Two corrections to the roadmap's framing that the planner needs: the 409 it refers to is
  `GroupSessionMiddleware`'s **null-board** branch, so there is no existing wrong-board 409 to
  inherit — today a wrong-board POST simply proceeds and the action 404s. And **D-02's header gate
  does not exclude POSTs on its own**: a form post is also `Sec-Fetch-Mode: navigate` +
  `Sec-Fetch-Dest: document`. Excluding writes must be an explicit verb check.

  Auto-switch on GET largely dissolves the stale-form problem anyway: to submit Board B's form you
  must first have loaded it, and loading it switched you. What remains is a tab left open across a
  manual switch. Rejected: switching on POST (a write repointing the session is strictly worse than
  a GET doing it, and the antiforgery token is user-bound rather than board-bound, so it would
  validate happily against the wrong board); resolving on POST for detection only and answering 409
  (honest feedback, but needs client-side handling that does not exist).

### Emails and the calendar feed

- **D-20: The email URLs are left exactly as they are.** Four Hangfire jobs emit
  `{AppUrl}/Quest/Details/{questId}` (`SessionReminderJob.cs:43` and its siblings
  `QuestFinalizedEmailJob`, `QuestDateChangedEmailJob`, `QuestWaitlistPromotedEmailJob`).

  The resolver handles them like any other link — **including the ones already sitting in people's
  mailboxes**, which board-qualifying cannot reach. A board hint parameter would be a second
  resolution route to keep honest, for a benefit only new mail sees.

  The full unauthenticated path was traced and works: `AccountController.Login` redirects to
  `GroupPicker.Index` carrying `returnUrl` (`AccountController.cs:146`), which threads it to
  `SelectGroup`, which redirects there. D-05 is what makes that path land correctly instead of
  dumping the viewer at a picker with no idea which board to choose.

- **D-21: The calendar feed is out of scope, and the reason is that it has no deep links at all.**
  `CalendarFeedWriter.cs:9` records that `DESCRIPTION` and `URL` were deliberately dropped. The
  roadmap's "whether emails and the calendar feed are in scope" question is therefore only half
  live. Restoring `URL` is adding a capability to a file the architecture notes flag as high-risk
  and test-pinned; deferred, not cut.

### Claude's Discretion

- **D-02's prefetch mechanism** — the operator deferred ("I don't know, leave it up to you. Whatever
  is best"). The header set, the exact prefetch hints honoured, and the decision to make the failed
  gate fall through to today's 404 rather than to a confirm interstitial are all Claude's call.
  Research and planning may refine *which* headers are checked; they may not weaken the property
  that a non-navigation request never changes the active board.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Prior decisions this phase revises or inherits
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` — D-11 (confirm-then-switch,
  **revised by D-01 for deep links only**), D-13 (`returnUrl` round trip), D-14 (one query with the
  membership set pinned), D-15 (membership read fresh per request), D-16 (second in-memory layer and
  its stated limits), D-10 (no SuperAdmin branch on a cross-board read).
- `.planning/PROJECT.md` — the Phase 55 operator override on SuperAdmin board-viewing parity, and
  the Phase 49/55 lesson that authorization must validate the target resource's group.
- `.planning/ROADMAP.md` §"Phase 87: Cross-Board Deep Link Recovery" — origin, the security property
  that must survive, and the five named risks. Note D-19 and D-21 correct two of its premises.

### Project rules that bind this phase
- `.claude/ui-guidelines.md` — **read before writing any Razor view.** Card pattern, button layout,
  `.Mobile.cshtml` twins, date-rendering helpers.
- `.claude/architecture.md` — layer rules (Service → Domain → Repository), EF package placement,
  migration commands.
- `CLAUDE.md` §"Code Comments" — no phase/requirement/review IDs in source comments.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns.
- `.planning/codebase/TESTING.md` — test project layout and the InMemory provider's limits.

### No external specs
No ADRs or external specification documents apply. Requirements are fully captured in the decisions
above; the phase has no REQ-IDs and runs on the D-NN decision ids in this file, as Phase 86 did.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — `SelectGroup:42-61` (membership-verified,
  antiforgery-protected, `returnUrl`-aware) and `Index:14-40` (already carries `returnUrl` and the
  membership list; its `groups.Count == 1` branch at `:33-35` is the auto-select precedent D-05
  generalises). `RedirectToLocal` handles the local-URL validation.
- `QuestBoard.Repository/GroupRepository.cs:29` — `GetGroupsForUserAsync` returns the viewer's boards
  with `Name` and `BoardType`, unfiltered. The membership source and the banner's board name.
- `QuestBoard.Service/Views/Agenda/Index.cshtml:192-250` — the confirm-then-switch modal and its JS.
  **Read for its warning copy**, which the banner can draw on. Not modified by this phase.
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` — the closed-allowlist architecture
  test D-11 mirrors for `IgnoreQueryFilters()`.
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — `ExemptPathPrefixes` and its
  `ControllerNameOf<T>()` helper; the `nameof`-derived route-prefix pattern is worth reusing.

### Established Patterns
- **Filters read the service live, never a captured value.** `QuestBoardContext.cs:391-393` carries a
  `CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here` comment.
  `ActiveGroupContextService.ActiveGroupId` reads Session on every access. This is what makes D-06's
  in-request switch work with no redirect.
- **18 `HasQueryFilter` entities** in `QuestBoardContext.OnModelCreating` (`:385-575`), all keyed off
  `ActiveGroupId`, all fail-closed (`!= null &&`). Proven by
  `QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs`.
- **404, not 403, for cross-tenant** (Phase 49/55) — which is why a 404-intercepting result filter
  cannot distinguish a cross-board miss from a role denial. One documented exception:
  `QuestLog/EditRecap` returns 403 for an intra-group role failure.
- **Filters constrain reads only.** The `260831-hz9` quick fix closed a cross-board IDOR in
  `ContactsController.AddNote` where a caller-supplied id reached an unconditional `Add()`. Relevant
  background for D-19.
- **`.Mobile.cshtml` twins are user-agent-selected, not viewport-selected** — devtools emulation never
  exercises them. Desktop and mobile edits belong in the **same task**, not merely the same wave
  (the Phase 61 lesson recorded in PROJECT.md).

### Integration Points
- `QuestBoard.Service/Program.cs:320-325` — `UseRouting` → `UseSession` → `UseAuthentication` →
  `GroupSessionMiddleware` → `UseRateLimiter` → `UseAuthorization`. The new middleware goes between
  `GroupSessionMiddleware` and `UseRateLimiter`; **before `UseAuthorization` is mandatory** (D-18).
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` **and** `_Layout.Mobile.cshtml` — the banner
  renders once, in the layout, so it covers all 18 routes and any added later. Both twins in one task.
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:115-128` — the null-board branch that
  calls the shared resolver for D-05's server side.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — the filters the resolver deliberately steps
  around; no filter is changed.
- Existing `IgnoreQueryFilters()` call sites for D-11's initial allowlist: `EventSignupRepository.cs:91`,
  `EventRepository.cs:172`, `GroupRepository.cs:135`, `GroupRepository.cs:147`, `QuestRepository.cs:270`,
  `QuestRepository.cs:293`.

</code_context>

<specifics>
## Specific Ideas

- **"They have access to the board, so why restrict it?"** — the operator's framing. The 404 is a
  correct consequence of the filter; what is wrong is the response to a legitimate member.
- **Fewer clicks is part of the goal, not a nice-to-have.** This is what drove D-01 to revise Phase
  82 D-11 and D-05 to skip the picker. A solution that is correct but adds a confirm step to every
  cross-board link has not solved the stated problem.
- **The operator's own scenario, raised mid-discussion:** "when a groupid session is expired, the
  user gets redirected to the grouppicker. However, selecting a board which is not part of the link
  the user was on previously, currently gets the 404. Meaning the user has to select the board it
  was looking on last, or be shown a 404. This is not user friendly at all." → D-05.
- **The operator's reading of the DM profile,** confirmed against the code: the bio and style are
  board-independent by design; only the quest list is board-scoped. → D-17.

</specifics>

<deferred>
## Deferred Ideas

- **A rendered 404 / error page.** `UseStatusCodePagesWithReExecute` plus a generic not-found view
  and its mobile twin, replacing the bare browser 404. Deliberately out of this phase (D-14): it is a
  new surface in a phase whose named failure mode is missing a mobile twin, and after this phase a
  member following a cross-board link never reaches it. The remaining case — a member following a
  link to something genuinely deleted — is unchanged from today.
- **Restoring `URL` to the calendar feed** so `.ics` entries link to the event (D-21). Adding a
  capability, and `CalendarFeedWriter.cs` is test-pinned and flagged high-risk. With deep-link
  recovery in place the argument for feed links gets *stronger*, so this is worth a roadmap entry.
- **A board hint parameter on newly-sent email links** (`?b=3`) as a resolution fast path (D-20).
  Declined because the lookup it saves is one indexed read and it creates a second resolution route.
- **Board-qualified routes** (`/b/{board}/quest/42`) as the long-term URL shape (D-06). Removes the
  inference permanently, but rewrites every URL the app emits and does nothing for links already
  sent — so it can only ever be an addition to this phase's work, never a replacement for it.
- **Answering cross-board POSTs with a 409 plus client-side "your session moved boards, reload"
  handling** (D-19). Real feedback instead of a silent 404, but needs UI that does not exist.

</deferred>

---

*Phase: 87-cross-board-deep-link-recovery*
*Context gathered: 2026-09-21*
