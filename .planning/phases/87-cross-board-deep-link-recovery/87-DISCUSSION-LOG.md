# Phase 87: Cross-Board Deep Link Recovery - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-21
**Phase:** 87-cross-board-deep-link-recovery
**Areas discussed:** Landing behaviour, Where resolution lives, The cross-board lookup seam, Oracle parity, Which surfaces are in scope, Writes and the 409, Email links, Role mismatch after switching

All eight offered areas were selected.

---

## Landing behaviour

### Arrival behaviour

| Option | Description | Selected |
|--------|-------------|----------|
| Confirm-then-switch | Generalise Phase 82's modal into an interstitial. One click, keeps the warning, honours the "a GET must not repoint session" risk. | |
| Auto-switch silently | Zero clicks. Repoints everything as a side effect of a GET; contradicts Phase 82 D-11. | |
| Auto-switch + banner | Land immediately, banner explains the move after the fact. | ✓ |
| Reuse the group picker | Redirect to /groups/pick with the resolved board highlighted. Roadmap warns against blurring into the no-board path. | |

**User's choice:** Auto-switch + banner
**Notes:** Presented with the correction that today's *no active board* case already works (middleware redirects to the picker with a returnUrl); the genuinely broken case is narrower — a *different* board is active — so the click budget being compared against is "404, open Switch Group, pick, re-find the page", not "one click".

### Prefetch / crawler safety

| Option | Description | Selected |
|--------|-------------|----------|
| Gate on navigation headers | Sec-Fetch-Dest: document + Sec-Fetch-Mode: navigate + no prefetch hint; anything else falls through to today's 404. | ✓ (Claude's call) |
| Headers, but confirm on the fallback | Same gate, with a confirm interstitial for header-less clients. | |
| Accept the risk | Auto-switch on any GET. | |

**User's choice:** "I don't know, leave it up to you. Whatever is best"
**Notes:** Claude chose the header gate with a fall-through to today's exact 404 — never a regression for any client, no second surface plus mobile twin to build, and it additionally closes a vector neither party had named: a cross-site `<img src=".../Quest/Details/42">` silently repointing the viewer's board.

### Banner lifetime

| Option | Description | Selected |
|--------|-------------|----------|
| One-shot, with switch-back | TempData-style, gone on next navigation, carries a POST to SelectGroup. | ✓ |
| One-shot, informational only | No switch-back control. | |
| Sticky until dismissed | Needs a session flag, a dismiss endpoint, and clearing on every other switch path. | |

**User's choice:** One-shot, with switch-back

### Switch-back destination

| Option | Description | Selected |
|--------|-------------|----------|
| Board Y's default landing | POST SelectGroup with no returnUrl; RedirectToLocal falls through to the normal destination. | ✓ |
| Back to where you came from | Thread Referer or a stashed URL. Absent for email links; needs new session state. | |
| The group picker | Loop-proof but makes switch-back two steps. | |

**User's choice:** Board Y's default landing
**Notes:** Framed around a loop hazard — switch-back must not return to the current URL, or auto-switch re-fires and bounces the viewer forward. Dropping the returnUrl makes the loop structurally impossible.

### The expired-session picker (raised by the operator)

| Option | Description | Selected |
|--------|-------------|----------|
| Skip the picker entirely | If the returnUrl resolves to one of the viewer's boards, switch and go straight through. | ✓ |
| Pre-select the right board | Show the picker but mark/pre-check the board that owns the link. | |
| Let auto-switch absorb it | Change nothing; pick any board and the resolver corrects it on the next request. | |

**User's choice:** Skip the picker entirely
**Notes:** The operator raised this unprompted: *"when a groupid session is expired, the user gets redirected to the grouppicker. However, selecting a board which is not part of the link the user was on previously, currently gets the 404. Meaning the user has to select the board it was looking on last, or be shown a 404. This is not user friendly at all."* Confirmed as a distinct path from the wrong-board case and folded into scope.

---

## Where resolution lives

### Mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Middleware beside GroupSessionMiddleware | Resolve in-request after UseRouting; no redirect, no controller edits. | ✓ |
| Intercept the 404 after the action runs | Result filter on NotFoundResult. Fires on every legitimate 404; cannot tell a cross-board 404 from a role-check 404. | |
| Explicit helper at each NotFound site | ~84 sites across 13 controllers; a missed one is invisible. | |
| Board-qualified routes | Rewrites every URL and does nothing for links already sent. | |

**User's choice:** Middleware beside GroupSessionMiddleware
**Notes:** Enabled by a verified property — `ActiveGroupContextService.ActiveGroupId` reads Session live with no caching, and `QuestBoardContext`'s filters reference the service rather than a captured value (`QuestBoardContext.cs:391-393` carries a `CRITICAL: Do NOT capture...` comment). Repointing session before the action runs makes the action's own queries correct.

### Structure of the two paths

| Option | Description | Selected |
|--------|-------------|----------|
| Two middlewares, one shared resolver | Separate classes and tests; one lookup seam. | ✓ |
| One middleware, third branch | Fewest moving parts, but merges the two paths the roadmap wants kept apart. | |
| New middleware runs first | Reorders an already-tested gate. | |

**User's choice:** Two middlewares, one shared resolver

### Writing the switch

| Option | Description | Selected |
|--------|-------------|----------|
| Extract a shared switcher | Pull the three session writes out of SelectGroup into one service. | ✓ |
| Middleware writes the keys directly | The second mechanism Phase 82 D-11 exists to prevent. | |
| Redirect through SelectGroup | Adds a round trip and makes a plain GET change state. | |

**User's choice:** Extract a shared switcher
**Notes:** Discovered during Area 7 that `GroupPickerController.Index:33-35` also writes the three keys in its single-group auto-select branch, so the extracted switcher has three callers rather than two.

---

## The cross-board lookup seam

### Registry shape

| Option | Description | Selected |
|--------|-------------|----------|
| Closed (controller, action) registry | Opt-in by construction; handles actions whose id is not the controller's own entity. | ✓ |
| Controller-level registry | Fewer entries, but wrong — silently — for signup/vote ids on QuestController. | |
| Attribute on the action | Scatters the list across 13 controllers with no single audit point. | |

**User's choice:** Closed (controller, action) registry

### Return value

| Option | Description | Selected |
|--------|-------------|----------|
| A board id and nothing else | SQL projection; structurally cannot leak row data. | ✓ |
| The entity, for reuse downstream | Saves a round trip; destroys the leak-nothing property. | |
| Board id plus the board name | Convenient for the banner; the name is what Phase 82 D-15 treats as sensitive. | |

**User's choice:** A board id and nothing else

### Containment

| Option | Description | Selected |
|--------|-------------|----------|
| Allowlist test + one dedicated class | Mirror AmbientClockSeamTests; 7 IgnoreQueryFilters call sites exist today. | ✓ |
| Allowlist test only | Catches new call sites but scatters the lookups. | |
| Behavioural tests only | Nothing stops a future bare IgnoreQueryFilters — which is how QuestRepository:270 exists. | |

**User's choice:** Allowlist test + one dedicated class

### SuperAdmin

| Option | Description | Selected |
|--------|-------------|----------|
| Same rule as everyone | Membership-pinned, SuperAdmin included. Matches Phase 55's operator override and Phase 82 D-10. | ✓ |
| SuperAdmin resolves anywhere | Mirrors SelectGroup's existing branch; reintroduces the privileged path Phase 55 removed. | |

**User's choice:** Same rule as everyone

---

## Oracle parity

### The 404 itself

| Option | Description | Selected |
|--------|-------------|----------|
| Leave it bare | No UseStatusCodePages today; parity stays free, no new view or mobile twin. | ✓ |
| Add a rendered 404 page | Better for genuinely-deleted entities; a new surface in a phase whose named failure mode is a missing twin. | |

**User's choice:** Leave it bare
**Notes:** Presented alongside the observation that parity here is structural — non-member and nonexistent run one identical code path with no branch, so there is nothing to keep in sync and no timing difference to measure.

### Proof

| Option | Description | Selected |
|--------|-------------|----------|
| Paired equivalence test | One test issuing both requests, asserting status/body/headers match. | ✓ |
| Separate 404 assertions | Two passing tests can describe two different 404s. | |
| Equivalence test plus a logging check | Guards a channel the viewer cannot observe. | |

**User's choice:** Paired equivalence test

---

## Which surfaces are in scope

### Route list

| Option | Description | Selected |
|--------|-------------|----------|
| All 18, DM Profile on an unambiguous-only rule | Every board-scoped top-level GET with an id. | ✓ |
| All 17 entity routes, skip DM Profile | Simpler rule; knowingly leaves the DM 404 in place. | |
| Read surfaces only | 8 Details routes; leaves co-DM edit links broken. | |

**User's choice:** All 18, DM Profile on an unambiguous-only rule
**Notes:** The operator interrupted before answering to supply domain knowledge: *"I believe the dungeonmaster profile is to show who the DM is and what his/her dm style is. Which is the same over all boards/campaigns... Only on this page, is a list of quests the DM has ran specific to the board you're viewing? (might need some confirmation)"* — verified correct against `DungeonMasterController.cs:20-49`: the profile is board-independent, the quest list runs under the query filter, and an explicit `IsTargetInActiveGroupAsync` gate 404s a DM absent from the active board. That confirmation turned an exclusion into a rule.

Also established here: the six image subresources are excluded for free by the header gate (`Sec-Fetch-Dest: image`) and do not need to resolve, since the page's own switch precedes the image requests.

### Links that resolve but will not render

| Option | Description | Selected |
|--------|-------------|----------|
| Accept it | Resolver answers "which board", the page answers the rest. | ✓ |
| Resolve only if the page would render | Duplicates business logic into the tenancy bypass. | |
| Switch, then switch back on failure | New machinery for a rare case. | |

**User's choice:** Accept it

---

## Writes and the 409

| Option | Description | Selected |
|--------|-------------|----------|
| GET/HEAD only — writes unchanged | Explicit verb check; cross-board POST behaves as today. | ✓ |
| Resolve on POST for detection only, return 409 | Real feedback instead of a silent 404; needs client handling that does not exist. | |
| Switch on POST too | A write repointing the session; antiforgery token is user-bound, not board-bound. | |

**User's choice:** GET/HEAD only
**Notes:** Two roadmap premises corrected before the question — the 409 it refers to is `GroupSessionMiddleware`'s null-board branch, so there is no wrong-board 409 to inherit (today a wrong-board POST proceeds and the action 404s); and the header gate does *not* exclude POSTs on its own, since a form post is also `Sec-Fetch-Mode: navigate` + `Sec-Fetch-Dest: document`.

---

## Email links

### The four email jobs

| Option | Description | Selected |
|--------|-------------|----------|
| Leave them as they are | The resolver handles them, including links already in mailboxes. | ✓ |
| Add a board hint to new links | Saves one indexed lookup; a second resolution route to keep honest. | |

**User's choice:** Leave them as they are
**Notes:** The unauthenticated path was traced first and holds — `AccountController.cs:146` redirects to `GroupPicker.Index` carrying `returnUrl`, which threads it to `SelectGroup`. This also relocated the "skip the picker" decision into `GroupPickerController.Index`, where the returnUrl and the membership list already meet.

### The calendar feed

| Option | Description | Selected |
|--------|-------------|----------|
| Out of scope — defer it | CalendarFeedWriter deliberately dropped URL; restoring it is a new capability on a test-pinned file. | ✓ |
| Add URL to the feed in this phase | Makes calendar entries clickable. | |

**User's choice:** Out of scope — defer it
**Notes:** Found during scouting that `CalendarFeedWriter.cs:9` records `DESCRIPTION` and `URL` as deliberately dropped, so the feed has no deep links at all — which makes half of the roadmap's "emails and the calendar feed" question moot rather than merely deferred.

---

## Role mismatch after switching

| Option | Description | Selected |
|--------|-------------|----------|
| Accept it — same rule as dead links | Resolver answers "which board", authorization answers "may you". | ✓ |
| Check the policy before switching | A second, divergable copy of the app's authorization rules. | |
| Switch, then switch back on denial | Machinery already declined for dead links. | |

**User's choice:** Accept it
**Notes:** Established here that placement before `UseAuthorization` (`Program.cs:325`) is *required*, not merely convenient — a Player on Board A who is a DM on Board B must be judged with Board B's role. Verified safe afterwards: `DungeonMasterHandler` reads `ActiveGroupId` live and does a fresh `GetGroupRoleAsync` lookup, so there is no cached role claim to go stale mid-request.

---

## Claude's Discretion

- **The prefetch-safety mechanism (D-02).** Operator deferred with "whatever is best". Claude chose the `Sec-Fetch-*` navigation-header gate with a fall-through to today's exact 404. Research and planning may refine which headers are checked; they may not weaken the property that a non-navigation request never changes the active board.

## Deferred Ideas

- A rendered 404 / error page (`UseStatusCodePagesWithReExecute` plus a generic view and its mobile twin).
- Restoring `URL` to the calendar feed so `.ics` entries link to the event.
- A board hint parameter (`?b=3`) on newly-sent email links as a resolution fast path.
- Board-qualified routes (`/b/{board}/quest/42`) as the long-term URL shape — an addition to this phase's work, never a replacement, since it cannot reach links already sent.
- Answering cross-board POSTs with a 409 plus client-side "your session moved boards, reload" handling.
