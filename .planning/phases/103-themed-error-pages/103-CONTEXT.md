# Phase 103: Themed Error Pages - Context

**Gathered:** 2026-11-18
**Status:** Ready for planning

<domain>
## Phase Boundary

A failed browser request shows an error page styled in the v9 theme instead of a bare status or an
empty body. The page offers three actions: go back, go to the home page (`/`), and log out and
return to the login page.

The trigger was a member following a link to a record on a board other than their active one and
landing on a plain 404. The board-scoped global query filters make the other board's record
invisible.

The phase also closes the existence leak that phase 99 left as known debt (see D-04 to D-06).
Today the cross-board deep-link switch fires for a record the viewer could not open on the target
board, and so tells them the id exists there.

Not in scope: widening the switch to more routes or to boards the viewer does not belong to, and
any new alerting on errors.

</domain>

<decisions>
## Implementation Decisions

### Cross-board 404s and the deep-link switch
- **D-01: The switch's reach does not change.** `CrossBoardDeepLinkMiddleware` and
  `CrossBoardLinkRegistry` keep the same closed route list and the same membership requirement. No
  routes are added, and nothing switches to a board the viewer does not belong to. The error page,
  not a wider switch, answers the operator's case.
- **D-02: The 404 page carries one fixed sentence for every viewer and every 404.** An example:
  "It may have been removed, or it may belong to a board you're not viewing right now." The wording
  never depends on the requested id, the record, or whether the record exists anywhere. Nothing on
  the page reveals anything about another board's records. That includes the page itself: the
  same body for a missing id and for an id on a board the viewer cannot see.
- **D-03: The 404 page shows a "Switch board" button for viewers with two or more boards.** A button to the board picker (`/groups/pick`) appears on the 404 page
  only for an authenticated viewer who belongs to two or more boards. It depends on the viewer's
  own memberships, never on the record. It is a secondary action next to the three required
  buttons. It is the manual way out when the switch did not fire.
- **D-04: The phase 99 Draft/scheduled deep-link leak is fixed in this phase.** The resolver
  returns a board only when the viewer could open the record there. For a shop item, that means
  the viewer would pass `Shop/Details` on the target board: they are a Dungeon Master or Admin of
  that board or a SuperAdmin, or the item is visible to members (Published or Archived, and its
  sale has started). Otherwise the resolver answers "not found", exactly the path a nonexistent id
  takes. The request then gets the normal themed 404 with no switch and no one-shot banner. —
  **Reversibility:** costly — the resolver becomes visibility-aware; reverting reopens the leak
  and touches the resolver, its repository query and the guard facts.
- **D-05: Every lookup kind in the registry is audited, not only shop items.** Research lists each
  kind whose target action can answer 404, or refuse, for a member on a record that exists. The
  kinds are Quest, Event, EventSeries, Character, Contact, ContactCategory, ShopItem and
  BoardMember. The list covers Details, Edit, Manage and the other registered actions. The D-04
  rule ("switch only if the viewer could open it there") applies to each kind found. Kinds with
  no such rule are recorded as checked and left as they are.
- **D-06: The membership-pinned lookup stays the tenancy boundary.** The fix never adds a branch
  that answers differently for "exists but hidden" and "does not exist": same response, same
  headers, no banner, and no session change. Guard facts pin both
  paths as indistinguishable.

### Which responses get the page
- **D-07: Every bodiless 4xx/5xx gets the themed page.** One status-code re-execute path covers
  all error statuses, with specific copy for 400, 404 and 500 and one generic fallback for the
  rest. A response that already has a body keeps it, except 429 (D-10).
- **D-08: 403 joins the new page.** `Account/AccessDenied` becomes the 403 variant of the shared
  error page: the same look and buttons, with today's two cases for authenticated and anonymous
  viewers. The cookie `AccessDeniedPath` (`/Account/AccessDenied`) and its URL stay the same.
- **D-09: Only browser document navigations get the HTML page.** That means a request whose
  `Accept` includes `text/html` and that is not `X-Requested-With: XMLHttpRequest`; research
  refines the rule. Fetch callers keep exactly what they get today. That covers the shop item
  modal and its `isModal` Details form, the markdown preview, event details, the series form and
  the shop preview. No existing JS changes behaviour.
- **D-10: 429 becomes a themed page for browser navigations.** It keeps the same sentence, "Too
  many requests. Please try again later.", and the 429 status. Non-HTML callers keep the plain
  text body. This is done in the rate limiter's `OnRejected`.

### 500 in production and development
- **D-11: Development keeps the developer exception page for unhandled exceptions.** Production
  and Testing use the themed 500, so integration tests can assert it. Status-code pages (404 and
  the rest) are themed in every environment, Development included.
- **D-12: The 500 page shows a reference.** It reads "Reference: <TraceIdentifier>". That is the same id the exception
  handler logs, so the operator can find it in the CT 102 logs. Nothing else from the exception
  reaches the page: no message, type or path.
- **D-13: No new logging or alerting.** The built-in exception handler logging, which carries the
  trace id, is enough.
- **D-14: The broken `/Error` handler is fixed.** `UseExceptionHandler("/Error")` points at a route
  with no action today, so a production 500 ends bare. The new error endpoint serves that path,
  and the `/Error` exemption in `GroupSessionMiddleware` stays valid for it.

### Buttons, anonymous viewers and the phone layout
- **D-15: "Go back" is `history.back()`, wired by a script file in `wwwroot/js`.** No inline
  script is used (CSP). With no history, or without JS, it falls back to a same-site `Referer`
  link, and to hiding the button when there is none. An off-site `Referer` is never followed.
- **D-16: "Home" goes to `/`.**
- **D-17: An authenticated viewer gets "Log out", which ends on the login page.** An anonymous
  viewer gets "Log in" instead. It goes to the login page with a `returnUrl` only when that URL
  is local. Button order and roles follow `.claude/ui-guidelines.md`: filled buttons with icons,
  `btn-secondary` for back, and the commit-style fill reserved as the guidelines say.
- **D-18: There is a phone twin with its own phone sheet.** It is a `.Mobile.cshtml` twin with its own `*.mobile.css`, as in the converted
  areas: no `@media` in the twin, and 44px targets.
- **D-19: A Gate 1 look check comes before the build.** It covers mockups of the 404 (with and
  without the Switch board button), 500 (with the reference), 403 and anonymous variants, on
  desktop and phone. They are compared against the live converted phase 98 to 100 pages, as in
  the phase 98 to 100 practice. Nothing visual is built before the operator approves Gate 1.

### Claude's Discretion
- **Log out mechanism (operator said "you decide"):** prefer reusing the existing
  `Account/Logout` POST form with an anti-forgery token rendered on the error page. Any mechanism
  is acceptable as long as logout can never be triggered by a cross-site GET or link.
- The exact copy per status code, within D-02's no-leak rule and the guidelines' tone.
- Which layout the error page uses for a failure under `/platform` (Platform layout versus the main
  one). Also a minimal fallback when the layout itself throws (for example the database is down
  or the active-board lookup fails), so the error page never errors into a bare response.
- Excluding `/hangfire`, `/health` and static-file paths from the HTML error page if needed.
- How the page renders with no active board, for example a 404 reached from the group picker
  path.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition and prior debt
- `.planning/ROADMAP.md` § "Phase 103: Themed Error Pages": goal and trigger.
- `.planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md` row "A deep link
  to a Draft, or to an item whose sale has not started, on another of the member's boards
  switches the board before the 404": the known debt that D-04 closes.
- `.planning/phases/99-shop-and-shop-management-ui-conversion/99-23-PLAN.md`,
  `99-22-SUMMARY.md`: how the Draft and availability rules on `Shop/Details` were defined.

### UI conventions
- `.claude/ui-guidelines.md`: card pattern, buttons by role, tokens and layers, no inline code,
  mobile twins, the accessibility checklist.
- `.planning/phases/100-admin-and-platform-ui-conversion/100-CONTEXT.md`: the latest conversion
  conventions (Gate 1 practice, confirm-submit, toasts).
- `.planning/phases/99-shop-and-shop-management-ui-conversion/99-UI-SPEC.md`: the most recent
  shipped UI spec, a reference for how a page's look is specified.

### Architecture
- `.claude/architecture.md`: layer rules (no EF in Service), board clock.
- `CLAUDE.md`: no GSD IDs in source comments, branch rules, local DB contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/Views/Shared/AccessDenied.cshtml`: the current themed-ish 403 page,
  served from `AccountController.AccessDenied`. It becomes the 403 variant (D-08).
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` holds the logout POST form, around line 256,
  which D-17 can reuse. `_Layout.Mobile.cshtml` and the Platform layouts are in
  `Areas/Platform/Views/Shared/`.
- `wwwroot/css/components.css` holds `.empty-state`, `.refusal-line`, the cards and the buttons.
  `tokens.css` holds the tokens.

### Established Patterns
- `CrossBoardDeepLinkMiddleware` (`QuestBoard.Service/Middleware/`) runs only for GET/HEAD
  top-level navigations to registry routes and calls `ICrossBoardLinkResolver`. Its "not found"
  path is deliberately identical to a nonexistent id.
- `CrossBoardLinkRegistry` and `CrossBoardRouteTarget` (`QuestBoard.Service/Helpers/`) hold the
  closed route list: 18 entries across 8 lookup kinds (corrected from 19 by research).
- `CrossBoardLinkResolverService` (`QuestBoard.Domain/Services/`) and `CrossBoardLinkRepository`
  (`QuestBoard.Repository/`) do the membership-pinned lookup. They never consult roles or
  visibility today. D-04 changes this.
- `ShopItemAvailability.IsOnSale` and the `Shop/Details` Draft/Denied/scheduled rule (phase 99)
  are the visibility rules D-04 must mirror.
- `GroupSessionMiddleware.ExemptPathPrefixes` already lists `/Error`.
- The rate limiter's `OnRejected` writes a plain-text 429 (`Program.cs` around line 163).

### Integration Points
- `Program.cs` around line 319: `UseExceptionHandler("/Error")` is inside the non-Development
  branch. A status-code re-execute (`UseStatusCodePagesWithReExecute` or similar) is added once,
  for all environments.
- About 80 `NotFound()`, 33 `BadRequest()` (13 bodiless), 21 `Forbid()` and 2 `StatusCode()` (410 and 304 in the calendar feed) returns in the
  controllers produce bodiless errors that the page will now cover.
- JS fetch callers expect today's bodiless statuses (D-09): `shop-item-modal.js`,
  `shop-item-preview.js`, `markdown-editor.js`, `event-details.js` and `event-series-form.js`.

</code_context>

<specifics>
## Specific Ideas

- The 404 sentence is the same for everyone, for example: "It may have been removed, or it may
  belong to a board you're not viewing right now."
- The 500 page shows "Reference: <trace id>" so a member can quote it to the operator.

</specifics>

<deferred>
## Deferred Ideas

- **Widening the automatic board switch.** This means more routes in the registry, such as index
  pages or the quest log, or switching to boards the viewer is not a member of. The operator kept
  the switch's reach as it is (D-01). It would be a backlog item if 404s keep coming from
  unregistered routes.
- **Alerting on 500s** (email or notification): a new capability, not requested.

</deferred>

---

*Phase: 103-themed-error-pages*
*Context gathered: 2026-11-18*

## Operator answers at execution

- logScopes: env (operator, 2026-11-19), amends D-12 and D-13: the 500 Reference is found in the journal only where `Logging__Console__IncludeScopes=true` is set (CT 102 through a systemd drop-in the operator adds, the Docker self-host path only if its operator sets it too), with no code change and no new log line.
  Applied by the operator on 2026-11-19 as the systemd drop-in `20-log-scopes.conf` on CT 102 and verified read-only afterwards (the setting shows in `systemctl show`, the service restarted on 6.5.0 and `/` answered 200).
- accessDeniedStatus: 403 (operator, 2026-11-19): the AccessDenied page answers status 403; the URL and the cookie path stay.
- preGate1Structural: yes (operator, 2026-11-19), the operator's reading of D-19: the server slices may ship the structural error page with shared classes only (no error.css, error.mobile.css, error-actions.js, own styling or chrome) before Gate 1.
- pageGateCounts: yes (operator, 2026-11-19): a deep link to a board that does not offer that page counts as a refusal and does not switch the board.
- superAdminAsMember: yes (operator, 2026-11-19): the reused SuperAdmin test account is added as a Player of the two P103 One-Shot boards so the SuperAdmin-member switch is proven live.
- contactToggle: dm-tier (settled, 2026-11-19): a Dungeon Master or Admin of the target board is entitled to know hidden contacts exist; the per-session show-hidden toggle only filters their own list, and modelling it would put session state into the Domain resolver.
