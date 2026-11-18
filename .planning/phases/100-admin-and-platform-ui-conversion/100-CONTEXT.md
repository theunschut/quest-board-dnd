# Phase 100: Admin and Platform UI conversion - Context

**Gathered:** 2026-11-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert the Admin pages (`Views/Admin/{Users,CreateUser,EditUser,ResetPassword,Quests}` each with its
`.Mobile` twin, plus the twin-less `EmailStats`) and the Platform area
(`Areas/Platform/Views/Group/{Index,Create,Edit,Delete,Members}`, `Users/Index`,
`LinkedBoards/Index`, each with its `.Mobile` twin) to the Phase 94 foundation (Direction A
"refined glass"), and verify the two Platform layouts the foundation built. The scope, counts,
stylesheet list, `@section Styles` rule, guard tests, deferred items and the six-point "Definition of
converted" are fixed by `.planning/ROADMAP.md` § "Phase 100" and are not repeated here.

Unlike Phases 98 and 99, this phase **also audits and fixes the server rules behind the converted
pages** (D-01 to D-05). Those pages act on other people's accounts and on whole boards. Phase 99
found such gaps late, during validation and security, which added three plans. Here they are found
first, on purpose.

Every content question for this area was answered at the Phase 94 G1 sign-off (2026-11-01) and is
inherited as recorded in `94-AUDIT.md` § "Twin content: Admin and Platform (Q-85 to Q-94)":

- **"Add it to the other view"** (the phone gains it):
  - Q-85: the Resend Welcome Email form and button on phone Admin/Users (unconfirmed users with an
    address);
  - Q-87: the Campaign board status logic (Open or Closed from `IsClosed`) on phone Admin/Quests,
    as one shared status mapping for both views (A-admin-016);
  - Q-88: the "Current Members" section heading on phone Platform Group/Members;
  - Q-92: the "Password Management" heading and its sentence on phone Admin/EditUser;
  - Q-93: the user's name in the phone Admin/ResetPassword card heading.
- **Deliberate, recorded, not changed**:
  - Q-86: no email on the phone Admin/Users card;
  - Q-89 and Q-90: no Description column and no DM email on phone Admin/Quests;
  - Q-91: the desktop duplicate `h1` is not copied to the phone (Q-124 removes it);
  - Q-94: the empty-state sentences stay as they are on each layout.
- **No merges.** Q-108 to Q-110 kept the three Platform Group twins (Create, Delete, Edit).
- **Relayouts approved in principle, each re-confirmed with its AFTER at Gate 1** (no AFTER exists
  yet for any of them):
  - Q-124: Admin/CreateUser and Admin/EditUser (desktop) get one page title in the card header
    instead of the bare-board `h1` plus the card `h2`;
  - Q-125: Platform Group/Members (desktop) stacks "Current Members" and "Available Users" full
    width;
  - Q-126: Admin/Users (desktop) puts the Email under the Name so the Actions cell gets the room.

No capability is added. User management, role changes, password resets, welcome resends, quest
deletion, email stats, board creation, editing, deletion and membership, platform-wide
enable/disable and linked board sets keep working. The server-rule fixes (D-02, D-03) narrow who
may do some of these to whom; they do not add actions.

</domain>

<decisions>
## Implementation Decisions

### Server-rule audit (new for this phase)
- **D-01: Audit early, fix in this phase.** Before any UI slice, research (or plan 1) audits every
  POST, DELETE and state-changing GET reachable from the 25 views, in `AdminController` and the
  Platform `GroupController`, `UsersController` and `LinkedBoardsController`:
  - who may act on whom;
  - cross-board effects (the action is board-scoped but changes a global account or another
    board);
  - self-actions and last-Admin cases;
  - board scoping of ids (for example `DeleteQuest` has no board check in the controller and relies
    on whatever the service and query filters do; the audit confirms or refutes it);
  - anti-forgery, rate limits and the response a crafted request gets.
  The report lists each gap with a proposed rule and lands in a `100-SECURITY.md`-style document.
- **D-02: Account edits stay within the acting Admin's reach.** A board Admin may reset the password
  of, or edit the name and email of, only an account whose highest role on any board is no higher
  than theirs. They are refused for an account that is a SuperAdmin or an Admin of another board.
  The refusal is a clear Error toast and the controls are disabled with a visible reason. A
  SuperAdmin may act on anyone. Today `EditUser` and `ResetPassword` check only that the target is a
  member of the active board, yet they change the global account. The same reach rule applies to
  `SendConfirmationEmail` if the audit shows the same shape.
- **D-03: Self and last-Admin guards on the role buttons and Remove from Group.** The server refuses
  an Admin who demotes or removes themselves. It also refuses any change that would leave the board
  with no Admin. Each refusal is an Error toast, and the matching buttons are hidden or disabled
  with a visible reason (not a `title`, as in A-admin-042).
- **D-04: What counts as in scope.** A gap is fixed here when an action on one of the 25 converted
  views reaches it, including in Domain or Repository code that action calls. A gap not reachable
  from these views goes to `deferred-items.md` with a severity. Layer rules hold: no EF Core in the
  Service project.
- **D-05: An operator checkpoint follows the audit, before any fix slice.** The operator approves,
  adjusts or accepts as a residual each gap and its proposed rule. That record is the security
  document's basis (the Phase 99 pattern). D-02 and D-03 are already decided and are not re-asked
  there.

### Fixtures, real data and live actions
- **D-06: The existing Phase 94 SuperAdmin `qb-audit-platform@example.invalid` is reused** for the
  Platform pages. Its credentials stay where Phase 94 put them and are never printed. No new
  SuperAdmin is created; no app form can grant one.
- **D-07: New p100 fixtures are created through the app's own forms** with the reused admin and
  SuperAdmin logins (Phase 94 D-25):
  - accounts `qb-audit-p100admin`, `p100dm`, `p100player`, `p100unconfirmed` and others as the guard
    tests need (for example an account that is an Admin on a second p100 board, for D-02), all
    `@example.invalid`, with passwords only in `~/qb-ui-review/phase-100/credentials.json` (mode
    600);
  - boards "P100 Audit ..." (One-Shot and Campaign, so Q-87 shows both status branches; an empty
    one; one with no members, so its Delete appears);
  - a p100 linked set with p100 boards;
  - every empty state listed in `94-AUDIT-admin.md` § "Empty-state copy".
  No existing account, board, membership or linked set is changed. Fixtures are kept afterwards.
- **D-08: All state-changing actions may run, on p100 fixtures only.** That covers role changes,
  reset password, welcome resend (to Mailpit), add, remove and create member, Disable and Enable,
  and linked set create, rename, add, remove and delete. They are used both to capture states and
  to prove the D-02 and D-03 guards live. They are never run on a real account, board or linked set.
- **D-09: Captures of pages listing real members and boards are taken as they are and stay local.**
  This covers Platform/Users, Group/Index, Members and Admin/Users. Screenshots stay under
  `~/qb-ui-review/phase-100/`. Reports describe them structurally and never quote a real name,
  email or board. These gate packages are not published to a review branch unless real rows are
  masked first.
- **D-10: The EmailStats statistics branch is exercised through a test-only stub of
  `ResendStatsClient`.** Canned figures feed the integration tests and a dev-only capture, so the
  stat tiles' contrast is measured (A-admin-036). No real key is used, and `appsettings.json` is not
  edited.
- **D-11: Any email the phase triggers goes to the Mailpit sink only** (`qb-mailpit`; starting it is
  pre-approved).

### Destructive actions
- **D-12: Remove from Group (Admin/Users) and Delete (Admin/Quests) become ordinary POST forms.**
  They are the same as every other action in the area: anti-forgery, a PRG redirect, and Success or
  Error toasts. The `[HttpDelete]` endpoints `DeleteUser` and `DeleteQuest` become POST, and their
  tests are updated to the same intent. The four inline copies of the fetch script and their
  `alert()` go (A-admin-033).
- **D-13: Confirmations stay native `confirm()` with today's wording**, through
  `wwwroot/js/confirm-submit.js` `data-confirm-message` (the Phase 95, 98 and 99 precedent). This
  covers Remove Member, Remove Board, Delete Set, Disable, Remove from Group and Delete Quest. The
  LinkedBoards handler strings built in Razor become data attributes.

### Gate 1, look and copy
- **D-14: The Gate 1 look check compares against the `quest-form` Direction A render and the live
  converted Phase 98 and 99 pages.** No Direction A admin render exists. There is one mockup per
  page type, each side by side with its reference:
  - a table list (Admin/Users or Group/Index);
  - a form (EditUser);
  - a confirmation page (Group/Delete);
  - the Members Create New User modal on the opaque modal surface (A-admin-006).
  The glass, scrim, badges, buttons and table cells must match what shipped in v6.3 and v6.4.
- **D-15: Gate 1 also carries:**
  - the AFTERs of Q-124, Q-125 and Q-126, desktop and phone;
  - the D-16 wording changes;
  - how the D-02 and D-03 refusals look (disabled buttons with their visible reason).
  Nothing in those is built before Gate 1 approves it.
- **D-16: The copy drift in A-admin-044 is fixed now** with one shared display mapping for board
  type ("One-Shot", "Campaign") and group role, used by both layouts. It is display text only;
  enums are untouched. Each wording change, for example "DungeonMaster" to "Dungeon Master" and
  "Admin" against "Administrator", is a Gate 1 item for the operator's yes.
- **D-17: The Platform layouts are verified on every page, and anything still failing is fixed
  here.** This covers A-admin-008 (the 320px bar), A-admin-014 (nav contrast), A-shell-037 and 038,
  the layout part of A-admin-029 (skip link, landmarks, toggler and offcanvas names) and A-admin-030
  (toggler size). Any remaining failure is fixed as a slice in this phase, not sent back to the
  foundation.

### Gates, fixtures and delivery (carried over from Phases 98 and 99)
- **D-18: Two operator gates, reviewed from a local package.**
  - Gate 1, early: `~/qb-ui-review/phase-100/gate1/`, with `index.html`, `README.md` and `img/`.
  - Gate 2, at the end: `~/qb-ui-review/phase-100/gate2/`, one before/after sheet, desktop and
    phone, with each contrast or target-size exception as an explicit accept or reject item.
  A rejected AFTER is redrawn until approved. The packages are published to a review branch only on
  request, and D-09 applies.
- **D-19: Run on the Linux machine that ran Phases 94 to 99**, reusing `~/qb-ui-review/phase-94`
  (Playwright, tools, renders, BEFORE paths). BEFORE is re-captured from the current branch into
  `~/qb-ui-review/phase-100/`. Q-126 has no populated BEFORE yet (G1 note), so this phase takes one.
  Front-end work invokes the `senior-frontend` skill.
- **D-20: The first plan opens with an operator checkpoint.** The operator confirms SQL Server at
  `localhost:1433`, which the agent never provisions. The local database holds real member data.
- **D-21: Work lands in slices on `milestone/v9-rolling-improvements` in this checkout**, with one
  release, v6.5.0, through a PR into `main` once both gates have passed. Each slice leaves every page
  usable and the suite green. Each plan's docs commit updates ROADMAP.md and STATE.md. Real-phone
  and screen-reader checks go in the phase UAT and may run after deploy.

### Claude's Discretion
- How the D-02 "highest role anywhere" check is expressed: a Domain service method or a query, kept
  in the right layer.
- Slice order and plan count. A sensible order:
  1. the audit, its checkpoint and the server fixes, with integration tests;
  2. the shared badge, button, table, link and muted-text fixes and the Platform layout
     verification (they clear A-admin-001 to 005 and 010 to 013 together);
  3. the forms (CreateUser, EditUser, ResetPassword, Group Create, Edit and Delete);
  4. the lists (Admin/Users, Admin/Quests, Group/Index, Members, Platform/Users, LinkedBoards),
     with the gated relayouts after Gate 1;
  5. EmailStats.
- How the six phone sheets and the admin ranges of `modern-card.css` and `site.css` consolidate into
  page sheets loaded through `@section Styles`. Dead rules go (A-admin-043, the nine duplicate glass
  cards of A-admin-038).
- The `autocomplete="new-password"` fix (A-admin-037), the Actions header alignment (A-admin-039)
  and the stable button positions (A-admin-040) follow the audit's proposed fixes.
- Ports: a free pair, checked before use.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 100: Admin and Platform UI conversion": scope, counts, stylesheet
  list, guard tests to rewrite (`PlatformAreaLayoutTests`, `LinkedBoardsPlatformIntegrationTests`,
  `BoardPageStaticGuardTests`, the Admin and Platform entries of `MobileViewsTests`), relayouts with
  BEFORE paths, Definition of converted.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-admin.md`: all 44 findings
  (A-admin-001 to 044), "Platform layout on phones", the twin comparison, merge and relayout
  proposals, empty-state copy (verbatim; no copy changes without the operator), stylesheet and
  inline-markup census.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md`:
  - § "Twin content: Admin and Platform (Q-85 to Q-94)";
  - Q-108 to Q-110 (merges kept);
  - Q-124 to Q-126 (relayouts) with their G1 answers;
  - the Relayout list.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-shell.md`: A-shell-037 and
  A-shell-038 (the Platform layouts).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`:
  - D-04: relayouts;
  - D-07 and D-08: release per phase, usable between slices;
  - D-12: inline code;
  - D-13 to D-15: twins, merges, mobile;
  - D-16 and D-17: the accessibility bar and colours;
  - D-25: fixture writes.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract
  (tables, badges, buttons, modal and offcanvas, page header).
- `~/qb-ui-review/phase-94/mockups/direction-a/png/quest-form-*.png` and their reports: the form
  look reference for D-14. `~/qb-ui-review/phase-94/before/` and
  `~/qb-ui-review/phase-94/audit/admin/` hold the audit's evidence and probes.

### Earlier area phases mirrored here
- `.planning/phases/99-shop-and-shop-management-ui-conversion/99-CONTEXT.md`: gates, fixtures and
  delivery (D-11 to D-16).
- `.planning/phases/99-shop-and-shop-management-ui-conversion/99-SECURITY.md` and `deferred-items.md`:
  the shape of the late server gaps (cross-keeper effects, ownership on delete, edit-review) that
  D-01 looks for up front.
- `.planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/98-CONTEXT.md`: the
  gate and fixture precedent, and the image-crop lesson (full pictures are never cropped with
  `aspect-ratio` plus `object-fit: cover`). No admin page shows a picture today, so it is not
  expected to trigger.
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-CONTEXT.md`: the `confirm-submit.js`
  pattern.

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, accessibility checklist, mobile twins,
  no inline code, dates.
- `.claude/architecture.md`: layers, authorization policies (`AdminOnly`, `SuperAdminOnly`), and
  `Html.LocalTime` and `Html.WallClock` for the Created dates.
- `CLAUDE.md`: branching, no planning IDs in source, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- The foundation sheets (`tokens.css`, `base.css`, `components.css`, `shell.css`,
  `shell.mobile.css`): badge, button, table, page header, opaque modal and empty-state components.
- `wwwroot/js/confirm-submit.js` for D-13; the toast helpers `RedirectWithSuccess` and
  `RedirectWithError` for D-03 and D-12.
- `emailResendLimiter`, a per-target-user rate limiter already on `SendConfirmationEmail` and
  email-changing `EditUser`.

### Established Patterns
- Every `AdminController` action resolves `activeGroupContext.ActiveGroupId` and checks
  `GetGroupRoleByIdAsync(target, group) != null` (membership only). `EditUser` and `ResetPassword`
  then change the global account, which is the D-02 gap. `ResetPasswordAsync(User, user, …)` takes
  the acting principal; research checks what it already enforces.
- `DeleteQuest` (`[HttpDelete]`, `[BoardPage(Quests)]`) loads by id with no board check in the
  controller. `DeleteUser` (`[HttpDelete]`) is really "remove from the active board".
- `EmailStats` carries `SuperAdminOnly` on top of the class-level `AdminOnly`.
- The Platform controllers are class-level `SuperAdminOnly`. `GetEffectiveGroupRoleAsync` maps a
  SuperAdmin to Admin.
- Platform phones render `_Layout.Platform.Mobile`, not dead code (`94-AUDIT-admin.md`).

### Integration Points
- Admin and Platform controller integration tests: the DELETE to POST change (D-12) and the new
  refusals (D-02, D-03) need tests.
- `ResendStatsClient` needs a seam the test stub can replace (D-10).
- The guard tests listed in the ROADMAP are rewritten to the same intent; none is deleted.

</code_context>

<specifics>
## Specific Ideas

- The operator wants the permission rules behind these pages checked before the UI work, not found
  late as in Phase 99.
- A board Admin must not be able to take over a more powerful account through password reset or
  email change (D-02).

</specifics>

<deferred>
## Deferred Ideas

- Server gaps found by the audit that no converted view reaches go to `deferred-items.md` (D-04).
- None raised as new capabilities during the discussion.

</deferred>

---

*Phase: 100-admin-and-platform-ui-conversion*
*Context gathered: 2026-11-18*
