---
phase: quick-261019-gff
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/mobile.css
  - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
autonomous: true
requirements: [quick-261019-gff]

estimate:
  tokens: 40000
  raw_tokens: 40000
  tasks: 2
  confidence: low

must_haves:
  truths:
    - "A super admin on a board that shares a linked set with at least one other board sees 'Move to Linked Board' on Quest Manage, desktop and mobile, in every quest state the page renders: open with proposed dates (Finalize row), open with no proposed dates (No Proposed Dates alert), finalized (Open Quest row) and campaign (Close/Reopen row)."
    - "When the quest can move, the control is an enabled link to /Move/Quest/{id}, rendered exactly once on the page."
    - "When the quest cannot move (closed, or finalized for a night already played), the control is a button type=button with aria-disabled=\"true\" and aria-describedby=\"move-reason\", followed by p.move-control-reason#move-reason carrying the one-line reason from MoveCopy.ControlReason, and no /Move/Quest link renders."
    - "A board Admin or a DM who belongs to both linked boards never sees the control on Manage, in either layout, even on a quest that DM runs."
    - "On a board in no linked set, or in a set of one, Manage renders nothing of the control: no label, no reason paragraph, no /Move/Quest link."
    - "Quest Details (desktop and mobile) and Event Details are byte-for-byte unchanged, and every pre-existing BoardMoveControlTests fact stays green."
    - "QuestController.Manage gains no clock read; the board's today reaches the control only through the move control service."
    - "No source line added by this work carries a requirement id, phase or plan number, review id or planning document name."
  artifacts:
    - path: "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs"
      provides: "Manage sets ViewBag.MoveControl for a SuperAdmin only, via IMoveControlService"
      contains: "ViewBag.MoveControl = User.IsInRole(\"SuperAdmin\")"
    - path: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      provides: "Desktop move control beneath the state's action row"
      contains: "MoveCopy.ControlReason(moveControl.BlockReason)"
    - path: "QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml"
      provides: "Mobile move control beneath the state's action stack"
      contains: "MoveCopy.ControlReason(moveControl.BlockReason)"
    - path: "QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs"
      provides: "Manage facts for shown, disabled-with-reason, hidden-off-set and hidden-for-board-roles, desktop and mobile"
      contains: "/Quest/Manage/"
    - path: "QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs"
      provides: "Planning-reference scan now covers both Manage views"
      contains: "QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml"
  key_links:
    - from: "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs"
      to: "QuestBoard.Domain/Services/MoveControlService.cs"
      via: "Manage action calls moveControlService.GetForQuestAsync(quest, currentUser.Id, token) behind User.IsInRole(\"SuperAdmin\")"
      pattern: "moveControlService\\.GetForQuestAsync\\(quest, currentUser\\.Id, token\\)"
    - from: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      to: "QuestBoard.Service/Controllers/MoveController.cs"
      via: "enabled link built with Url.Action(\"Quest\", \"Move\", new { id = Model.Id })"
      pattern: "Url\\.Action\\(\"Quest\", \"Move\""
    - from: "QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml"
      to: "QuestBoard.Service/Controllers/MoveController.cs"
      via: "enabled link built with Url.Action(\"Quest\", \"Move\", new { id = Model.Id })"
      pattern: "Url\\.Action\\(\"Quest\", \"Move\""
    - from: "QuestBoard.Service/Views/Quest/Manage*.cshtml"
      to: "QuestBoard.Service/Helpers/MoveCopy.cs"
      via: "disabled branch renders the reason via MoveCopy.ControlReason"
      pattern: "MoveCopy\\.ControlReason\\(moveControl\\.BlockReason\\)"
---

<objective>
Put the "Move to Linked Board" control on the Quest Manage page, desktop and mobile, with the
exact behaviour it already has on Quest Details: shown only to a platform super admin on a board
that shares a linked set with another board; an enabled link to `/Move/Quest/{id}` when the quest
can move; a disabled, focusable, described button plus a one-line reason when it cannot; nothing at
all when the move control state is null.

Purpose: a super admin managing a quest should not have to leave Manage for Details to start a
move. Quest Details and Event Details stay exactly as they are.

Output: controller wiring in `QuestController.Manage`, the control in `Manage.cshtml` and
`Manage.Mobile.cshtml`, a mobile.css comment correction, Manage facts in
`BoardMoveControlTests.cs` (desktop and mobile user agents), and the two Manage views added to the
board-move planning-reference guard.

## Design decisions (planner's discretion, justified)

**Placement: one block per layout, directly beneath whichever action row the quest's state
renders, not inside each row.** Manage has four mutually exclusive action areas per layout, and
exactly one renders for a given quest:
(a) open with no proposed dates: Edit + Delete inside the "No Proposed Dates" alert;
(b) open with proposed dates: the Finalize form row (Finalize, Edit, Delete | Refresh Data);
(c) finalized: Edit, Open Quest, Create Follow-Up, Send Reminder | Refresh Data;
(d) campaign board: Edit, Close/Reopen, Delete | Refresh Data.
In every state that area is the last content of the main card body (desktop) or of
`.dm-manage-section-card` (mobile). One insertion point after all four, at the end of that
container, therefore lands immediately under the Edit/Delete/Finalize/Open row in every state,
which is where a super admin's eye ends after scanning the page's actions. Reasons for not going
inside the rows:
1. In-row placement needs four copies per layout (eight total) to cover every state; this needs two.
2. The desktop rows in (c) and (d) are non-wrapping `d-flex gap-2` groups in the `col-md-8` column;
   state (c) already holds four buttons plus Refresh Data, so a fifth overflows at `md`/`lg` widths.
3. The disabled state needs a line of its own for the reason paragraph; a horizontal row cannot give
   it one without splitting the button from its reason.
4. In state (b) an in-row control would sit inside the Finalize `<form>`; beneath the row it does not.
On mobile the rows are `flex-wrap gap-2` stacks of full-width buttons, so a block with `mt-2`
(8px, the same as `gap-2`) right after the stack reads as one more button at the bottom of it.

**No shared partial: duplicate consistently.** Recommendation is to keep the markup inline in each
view, mirroring Details. Every site uses different spacing on all three elements (Details desktop
link and button `w-100 mt-2`, reason `mb-0 mt-2`; Details mobile link `w-100 mb-2`, button `w-100`,
reason `mt-2 mb-2`; Manage desktop and mobile as specified below), so a partial's parameter list
would be three free-form class strings plus a URL, which is the whole variable surface of the
markup. Extracting it would also mean editing the two Details views, which this task must leave
unchanged. The parts that carry meaning are already single-sourced: eligibility in
`MoveControlState`, the reason text in `MoveCopy.ControlReason`. The remaining contract (label,
icon, `aria-disabled`, `aria-describedby` to `#move-reason`) is pinned at every quest site by the
same regex shape in `BoardMoveControlTests`, so a drifting copy fails a test.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.claude/ui-guidelines.md
@.planning/STATE.md
@.planning/phases/92-move-a-quest-or-event-to-a-linked-board/92-08-SUMMARY.md

Source to read before editing (all already exist):
@QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
@QuestBoard.Service/Views/Quest/Manage.cshtml
@QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
@QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs

<interfaces>
From QuestBoard.Domain/Interfaces/IMoveControlService.cs (already injected into QuestController as `moveControlService`):
- `Task<MoveControlState?> GetForQuestAsync(Quest quest, int actorUserId, CancellationToken token = default)`
  returns null when there is no active board or the active board's linked set has no other board.
  It reads the board clock itself, so the controller needs none.

From QuestBoard.Domain/Models/BoardMove/MoveControlState.cs:
- `record MoveControlState(MoveBlockReason BlockReason)` with `bool CanMove` (BlockReason == None).

From QuestBoard.Service/Helpers/MoveCopy.cs:
- `static string MoveCopy.ControlReason(MoveBlockReason reason)` returns "Closed in the Quest Log." for a
  closed quest and "Already played." for a played one.

QuestController.Details already does (lines ~353-358), with a plain-language comment above it:
`ViewBag.MoveControl = currentUser != null && User.IsInRole("SuperAdmin") ? await moveControlService.GetForQuestAsync(quest, currentUser.Id, token) : null;`

QuestController.Manage today: `public async Task<IActionResult> Manage(int id)`, loads
`questService.GetQuestWithManageViewDetailsAsync(id)` (accepts a CancellationToken), returns
`Challenge()` when `userService.GetUserAsync(User)` is null, so `currentUser` is non-null after that.
It already sets `ViewBag.BoardToday = boardClock.Today` (existing, permitted; do not add another clock read).

Details desktop markup to mirror (QuestBoard.Service/Views/Quest/Details.cshtml ~765-781): enabled
`<a href="@Url.Action("Quest", "Move", new { id = ... })" class="btn btn-info w-100 mt-2">` with
`<i class="fas fa-right-left me-2" aria-hidden="true"></i>Move to Linked Board`; disabled
`<button type="button" class="btn btn-info w-100 mt-2 disabled" aria-disabled="true" aria-describedby="move-reason">`
(same icon and label) then `<p class="move-control-reason mb-0 mt-2" id="move-reason">@MoveCopy.ControlReason(moveControl.BlockReason)</p>`.

Test helpers in BoardMoveControlTests (private, same class): `SeedScenarioAsync(Linking linking = Linking.LinkedPair)`
returns `Scenario(SuperAdminClient, SuperAdminId, DmId)`; `GetAsync(client, url, userAgent)`;
constants `DesktopUserAgent`, `MobileUserAgent`, `BoardAlpha`, `BoardBravo`, `ControlLabel`,
`UpcomingDates`, `PlayedDate`; `ShouldShowDisabledControlWithReason` (requires `w-100`, Details-only).
`BoardMoveSeed.SeedQuestAsync(factory, groupId, title, dmUserId, proposedDates, finalizedDate: null, isClosed: false)`;
`BoardMoveSeed.SeedBoardAsync(factory, id, name, BoardType)`; `BoardMoveSeed.LinkAsync(factory, BoardType, params int[] boardIds)`
(also sets every listed board's BoardType); `BoardMoveSeed.SeedMembershipAsync(factory, userId, groupId, GroupRole)`;
`AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, userName, email, roles: [role])`.
`TestDataHelper.ClearDatabaseAsync` drops and recreates the InMemory database, so a campaign board
type set by one fact never leaks into the next.
</interfaces>
</context>

<tasks>

<task type="tracer">
  <name>Task 1: Super admin starts a move from desktop Quest Manage (controller to view to test)</name>
  <files>QuestBoard.Service/Controllers/QuestBoard/QuestController.cs, QuestBoard.Service/Views/Quest/Manage.cshtml, QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs</files>
  <read_first>
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs (Details ~313-360 and Manage ~941-966)
    - QuestBoard.Service/Views/Quest/Details.cshtml lines 755-785 (markup being mirrored; read only, do not edit)
    - QuestBoard.Service/Views/Quest/Manage.cshtml lines 1-12, 136-160, 374-396, 615-680
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs (whole file)
    - .claude/ui-guidelines.md
  </read_first>
  <action>
Controller, `QuestController.Manage`:
- Change the signature to `Manage(int id, CancellationToken token = default)` (MVC binds the request-aborted token; no route or caller changes) and pass `token` into `questService.GetQuestWithManageViewDetailsAsync(id, token)`.
- After the existing `ViewBag.BoardToday` line, add `ViewBag.MoveControl = User.IsInRole("SuperAdmin") ? await moveControlService.GetForQuestAsync(quest, currentUser.Id, token) : null;` mirroring Details. `currentUser` is already non-null here because of the `Challenge()` guard, so no null check is needed. Key on the SuperAdmin role itself, never on `isAdmin` / `IsAuthorized`, because a board Admin or DM is authorized for Manage and must not see the control.
- Precede it with a short plain-language comment (for example: the move control belongs to the platform super admin alone, so it keys on that role rather than on the board-level authorization above; a null state means the board has nothing to move to and the view renders nothing). No requirement ids, phase or plan numbers, or planning document names.
- Add no clock read: no `boardClock`, no `DateTime` in the added lines. The service reads the board clock itself. Change nothing else in the action.

Desktop view, `QuestBoard.Service/Views/Quest/Manage.cshtml`:
- Add `@using QuestBoard.Domain.Models.BoardMove` and `@using QuestBoard.Service.Helpers` to the usings, and in the top `@{ }` block add `var moveControl = ViewBag.MoveControl as MoveControlState;` after the `boardToday` line.
- Insert the control once, inside the main card's `card-body modern-card-body`, immediately after the closing brace of the `@if (boardType == BoardType.Campaign)` block and before that card body's closing `</div>` (around line 676). This is after all four mutually exclusive action areas, so in every state it sits directly beneath the row that rendered (see the objective's placement decision). It is inside the authorized `else` branch, never in the Access Denied branch.
- Markup, all inside `@if (moveControl != null)` so a null state renders nothing, not even a wrapper: a `<div class="mt-2">` containing, when `moveControl.CanMove`, `<a href="@Url.Action("Quest", "Move", new { id = Model.Id })" class="btn btn-info">` with `<i class="fas fa-right-left me-2" aria-hidden="true"></i>Move to Linked Board`; otherwise `<button type="button" class="btn btn-info disabled" aria-disabled="true" aria-describedby="move-reason">` with the same icon and label (attributes in exactly that order: type, class, aria-disabled, aria-describedby), followed by `<p class="move-control-reason mb-0 mt-2" id="move-reason">@MoveCopy.ControlReason(moveControl.BlockReason)</p>` (class before id). No `w-100` on desktop: this is the wide `col-md-8` column, and the button should match the auto-width buttons in the row above it. Filled `btn-info` with an `me-2` icon per the UI guidelines; no outline variant; no `<span>`, `small` or `.text-muted` (the `.modern-card` cascade forces those to parchment). The reason picks up `.move-control-reason` from `move-entry.css`, already linked globally in `_Layout.cshtml`. Do not touch any existing action row.
- Do NOT edit `Details.cshtml`, `Details.Mobile.cshtml` or `Events/Details.cshtml`, and do not extract a partial (see the objective's decision).

Tests, `QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs` (add to this class to reuse its private seed helpers and fixture; add desktop rows only in this task, Task 2 adds the mobile rows):
- Give `SeedScenarioAsync` a second optional parameter `BoardType boardType = BoardType.OneShot` and use it for `factory.TestGroupContext.BoardType`, for the `SeedBoardAsync` call that seeds Board Bravo, and for both `LinkAsync` calls. Existing callers pass nothing and behave exactly as before.
- Add a private `ManageAsync(HttpClient client, int questId, string userAgent)` that calls `GetAsync(client, $"/Quest/Manage/{questId}", userAgent)`.
- Add a private helper `ShouldShowDisabledManageControlWithReason(string html, string reason)`: contains `ControlLabel`; matches regex `<button type="button" class="btn btn-info[^"]*disabled" aria-disabled="true" aria-describedby="move-reason"` (no `w-100` requirement, because desktop Manage has none); matches `<p class="move-control-reason[^"]*" id="move-reason">\s*{Regex.Escape(reason)}\s*</p>`; and `Regex.Matches(html, "id=\"move-reason\"").Count` is exactly 1. Leave the existing `ShouldShowDisabledControlWithReason` untouched.
- Theory `SuperAdminOnALinkedBoard_SeesTheEnabledControlOnManage_InEveryQuestState(string state, string marker, string userAgent)`, desktop rows: ("open", "Finalize Quest"), ("no-dates", "No Proposed Dates"), ("finalized", "Open Quest"), ("campaign", "Close Quest"). Seeding: "campaign" uses `SeedScenarioAsync(Linking.LinkedPair, BoardType.Campaign)`, the rest the default; "open" and "campaign" seed with `UpcomingDates`; "no-dates" seeds an empty proposed-date array; "finalized" seeds `UpcomingDates` with `finalizedDate: UpcomingDates[0]` (still upcoming, so it can move). Assert 200; html contains `marker` (proves which action area rendered); contains `ControlLabel` and `fa-right-left`; `href="/Move/Quest/{questId}"` occurs exactly once (count with `Regex.Matches` on the escaped string); does not contain `move-control-reason`.
- Theory `AQuestThatCannotMove_ShowsADisabledControlWithItsReasonOnManage(string state, string reason, string marker, string userAgent)`, desktop rows: ("closed", "Closed in the Quest Log.", "Finalize Quest") on a one-shot board with `UpcomingDates` and `isClosed: true`; ("closed-campaign", "Closed in the Quest Log.", "Reopen Quest") on a campaign scenario with `isClosed: true`; ("played", "Already played.", "Open Quest") seeded with `[PlayedDate]` and `finalizedDate: PlayedDate`. Assert 200, contains `marker`, does not contain `href="/Move/Quest/{questId}"`, and `ShouldShowDisabledManageControlWithReason(html, reason)`.
- Theory `SuperAdminOnABoardOutsideAnySetOrInASetOfOne_SeesNoControlOnManage(Linking linking, string userAgent)`, desktop rows for `Linking.NoSet` and `Linking.SetOfOne`: seed an open quest and a closed quest (both with `UpcomingDates`); for each, GET Manage as the super admin and assert 200, contains "Finalize Quest" (the management view really rendered, so the absence means something), and does not contain `ControlLabel`, `move-control-reason` or `/Move/Quest`.
- Theory `BoardAdminOrDmOfBothBoards_NeverSeesTheControlOnManage(string role, string userAgent)`, desktop rows for "Admin" and "DungeonMaster": create the role client with `CreateAuthenticatedClientWithUserAsync(factory, key, $"{key}@example.com", roles: [role])` using a key distinct from the Details fact's (for example `move_manage_{role.ToLowerInvariant()}`), seed its membership on both boards with the matching `GroupRole`, then seed the open quest with the DM set to that user for "DungeonMaster" (so the page is that DM's own management view, not Access Denied) and to `scenario.DmId` for "Admin". Assert 200, does not contain "Access Denied", contains "Finalize Quest", and does not contain `ControlLabel`, `move-control-reason` or `/Move/Quest`.
- Update the class summary's first sentence to say the control is checked on the Details pages and on Quest Manage. No planning references in any comment or string literal.
  </action>
  <verify>
    <automated>cd "$(git rev-parse --show-toplevel)" && dotnet build && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~BoardMoveControlTests" && dotnet test QuestBoard.UnitTests --no-build --filter "FullyQualifiedName~AmbientClockSeamTests"</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c 'ViewBag.MoveControl = ' QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` returns 2 (Details and Manage; Events has its own controller).
    - `grep -c 'moveControlService.GetForQuestAsync(quest, currentUser.Id, token)' QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` returns 2.
    - Before the task commit, `git diff -U0 -- QuestBoard.Service/Controllers/QuestBoard/QuestController.cs | grep '^+[^+]' | grep -cE 'DateTime\.|boardClock'` returns 0.
    - `grep -c 'MoveCopy.ControlReason(moveControl.BlockReason)' QuestBoard.Service/Views/Quest/Manage.cshtml` returns 1 and `grep -c 'Url.Action("Quest", "Move"' QuestBoard.Service/Views/Quest/Manage.cshtml` returns 1.
    - `git diff --quiet -- QuestBoard.Service/Views/Quest/Details.cshtml QuestBoard.Service/Views/Quest/Details.Mobile.cshtml QuestBoard.Service/Views/Events/Details.cshtml` exits 0.
    - All BoardMoveControlTests facts pass: the 22 pre-existing cases plus the new desktop rows (4 enabled, 3 disabled, 2 off-set, 2 role).
    - AmbientClockSeamTests passes.
  </acceptance_criteria>
  <done>A super admin on a linked board opening Quest Manage on desktop sees the enabled control beneath the action row in all four states, or the disabled control with its reason for a closed or played quest; board Admins, DMs and boards outside a linked set see nothing; Details is untouched; the desktop facts are green and the task is committed.</done>
</task>

<task type="auto">
  <name>Task 2: Mobile Quest Manage gets the same control, guarded and fully regression-tested</name>
  <files>QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml, QuestBoard.Service/wwwroot/css/mobile.css, QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs, QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs</files>
  <read_first>
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml lines 1-30, 190-240, 424-484
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml lines 440-465 (read only, do not edit)
    - QuestBoard.Service/wwwroot/css/mobile.css lines 55-66
    - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs lines 44-100
  </read_first>
  <action>
Mobile view, `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml`:
- Add `@using QuestBoard.Domain.Models.BoardMove` and `@using QuestBoard.Service.Helpers`, and `var moveControl = ViewBag.MoveControl as MoveControlState;` in the top `@{ }` block after the `boardType` line.
- Insert the control once, inside `.dm-manage-section-card`, immediately after the closing brace of the `@if (boardType == BoardType.Campaign)` block and before the section card's closing `</div>` (around line 481). As on desktop this is after all four mutually exclusive action stacks, so it sits at the bottom of whichever stack rendered.
- Markup inside `@if (moveControl != null)` (null renders nothing): a `<div class="mt-2">` containing, when `moveControl.CanMove`, `<a href="@Url.Action("Quest", "Move", new { id = Model.Id })" class="btn btn-info w-100">` with `<i class="fas fa-right-left me-2" aria-hidden="true"></i>Move to Linked Board`; otherwise `<button type="button" class="btn btn-info w-100 disabled" aria-disabled="true" aria-describedby="move-reason">` with the same icon and label (same attribute order as desktop), followed by `<p class="move-control-reason mt-2 mb-0" id="move-reason">@MoveCopy.ControlReason(moveControl.BlockReason)</p>`. `w-100` matches the full-width buttons in the mobile stacks, and the wrapper's `mt-2` equals the stacks' `gap-2`, so it reads as one more button in the stack. Use `Url.Action` (as the rest of this file does) rather than tag helpers. Do not modify any existing action stack.
- Do NOT edit `Details.Mobile.cshtml`.

`QuestBoard.Service/wwwroot/css/mobile.css`: the comment above `.move-control-reason` says the reason sits under a disabled move control "on a Details page"; reword it to say a Details or Manage page. Change no rule values: the existing 14px and `#D9CDA8` rule already serves the dark glass `.dm-manage-section-card` (`dm-manage.mobile.css` sets no paragraph colour that would override it).

Tests, `BoardMoveControlTests.cs`: add a `MobileUserAgent` `InlineData` row for every desktop row Task 1 added to the four Manage theories (4 enabled, 3 disabled, 2 off-set, 2 role), so each Manage theory runs under both user agents. The mobile markers are the same strings ("Finalize Quest", "No Proposed Dates", "Open Quest", "Close Quest", "Reopen Quest"), all present in the mobile view. No assertion changes are needed: the mobile disabled button also satisfies the relaxed regex.

Guard, `BoardMoveStaticGuardTests.cs`: add `"QuestBoard.Service/Views/Quest/Manage.cshtml"` and `"QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml"` to `FeatureSourceFiles`, keeping the list's alphabetical order (directly after `Views/Quest/Details.Mobile.cshtml`). Both files are free of the guarded shapes today, so the scan stays green and now holds the line on these views.

Then run the wider regression and the full suite. On Linux never set `DOTNET_GCHeapHardLimit` or any GC-constraining variable. A `Fatal error. Internal CLR error. (0x80131506)` is a transient flake: re-run the same command once, or run the two test projects separately with `--no-build`, before treating it as real.
  </action>
  <verify>
    <automated>cd "$(git rev-parse --show-toplevel)" && dotnet build && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~BoardMove|FullyQualifiedName~QuestDetails|FullyQualifiedName~QuestFinalizedEdit|FullyQualifiedName~QuestCampaignUiParity|FullyQualifiedName~QuestStatusBoardClock|FullyQualifiedName~WallClockUnmoved|FullyQualifiedName~MobileViews|FullyQualifiedName~MobileCss" && dotnet test QuestBoard.UnitTests --no-build --filter "FullyQualifiedName~AmbientClockSeamTests|FullyQualifiedName~Architecture|FullyQualifiedName~BoardMove" && dotnet test QuestBoard.UnitTests --no-build && dotnet test QuestBoard.IntegrationTests --no-build</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c 'MoveCopy.ControlReason(moveControl.BlockReason)' QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` returns 1 and `grep -c 'Url.Action("Quest", "Move"' QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` returns 1.
    - `grep -c 'Views/Quest/Manage' QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs` returns 2.
    - `git diff --quiet -- QuestBoard.Service/Views/Quest/Details.cshtml QuestBoard.Service/Views/Quest/Details.Mobile.cshtml QuestBoard.Service/Views/Events/Details.cshtml` exits 0.
    - `grep -nE '\bD-[0-9]{2}\b|[Pp]hase [0-9]+|\b9[0-9]-[0-9]{2}\b|UI-SPEC|CONTEXT\.md|RESEARCH\.md|PLAN\.md' QuestBoard.Service/Controllers/QuestBoard/QuestController.cs QuestBoard.Service/Views/Quest/Manage.cshtml QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml QuestBoard.Service/wwwroot/css/mobile.css QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs` prints nothing (exit 1; all five files are clean today).
    - BoardMoveControlTests passes with 44 cases (22 pre-existing plus 22 Manage cases: 8 enabled, 6 disabled, 4 off-set, 4 role).
    - Full unit and integration suites are green (the SQL Server-only skipped facts stay skipped).
  </acceptance_criteria>
  <done>The mobile Manage page shows the same control under the same rules; every Manage theory runs under desktop and mobile user agents; the guard scans both Manage views; Details is untouched; the full suite is green and the task is committed.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser to QuestController.Manage | Any DungeonMasterOnly-authorized user (board DM, board Admin, SuperAdmin) can GET Manage; only a SuperAdmin may be offered the move control |
| Manage page to MoveController | The control is only a link; MoveController re-checks role and eligibility on its own GET and POST |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-gff-01 | Elevation of Privilege | QuestController.Manage | medium | mitigate | `ViewBag.MoveControl` is set only under `User.IsInRole("SuperAdmin")`, never from `isAdmin`/`IsAuthorized`; the role facts prove a board Admin and a DM of both boards (on the DM's own quest) see no control in either layout |
| T-gff-02 | Information Disclosure | Manage.cshtml / Manage.Mobile.cshtml | low | mitigate | The control (which reveals that a board is in a linked set) renders only for a SuperAdmin and only when the service returns a non-null state; off-set and set-of-one facts assert no label, reason or link |
| T-gff-03 | Tampering | direct GET or POST to /Move/Quest/{id} for an ineligible quest | low | accept | The disabled control sends nothing (button type=button, no form, no handler); MoveController already refuses an ineligible quest server-side, pinned by the existing direct-GET fact |
| T-gff-04 | Tampering | reason paragraph / link markup | low | accept | Reason text comes from fixed `MoveCopy.ControlReason` strings and the link from `Url.Action` with an int id, both Razor-encoded; no user-controlled text is added to the page |
</threat_model>

<verification>
- `dotnet build` succeeds.
- `dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~BoardMoveControlTests"`: 44 cases pass.
- Wider integration filter (BoardMove, QuestDetails, QuestFinalizedEdit, QuestCampaignUiParity, QuestStatusBoardClock, WallClockUnmoved, MobileViews, MobileCss) passes.
- Unit filter (AmbientClockSeamTests, Architecture, BoardMove) passes.
- Full `dotnet test` (or both projects separately with `--no-build` if the CLR flake appears) is green.
- Details views and Event Details unchanged (`git diff --quiet` on the three files).
- Not established by these checks: visual rendering. The 8px spacing, the desktop auto-width button under the row, and the reason's 14px parchment and `#D9CDA8` colours are asserted by markup and existing CSS rules only; nothing is viewed in a browser.
</verification>

<success_criteria>
- A super admin on a linked board can start a move from Quest Manage in both layouts and in every quest state, or sees why the quest cannot move.
- No one else sees the control on Manage, and boards outside a linked set (or in a set of one) show nothing.
- Quest Details and Event Details behave and render exactly as before; all pre-existing tests stay green.
- No new clock read in the controller; no planning references in source.

## Source coverage audit (quick task: the task description is the only source)

| Source item | Covered by |
|-------------|------------|
| Manage sets ViewBag.MoveControl for SuperAdmin via GetForQuestAsync, mirroring Details, no clock read | Task 1 (controller) |
| Button in the Manage bottom action area next to Edit/Delete/Finalize/Open, justified placement | Task 1 (desktop), Task 2 (mobile); justification in objective |
| Enabled link to /Move/Quest/{id}; disabled button with aria-disabled + aria-describedby + reason; nothing when null | Task 1 and Task 2 markup |
| Keep the Details button unchanged | Both tasks: no Details edits, `git diff --quiet` criterion |
| Both layouts updated; ui-guidelines followed; reuse `.move-control-reason` | Task 1 (desktop, move-entry.css), Task 2 (mobile, mobile.css) |
| Tests: shown for super admin on a linked board | Task 1/2 enabled theory (4 states x 2 user agents) |
| Tests: hidden for board Admin/DM of both boards | Task 1/2 role theory |
| Tests: hidden on a board in no set / set of one | Task 1/2 off-set theory |
| Tests: disabled with the reason for a closed quest | Task 1/2 disabled theory (closed one-shot, closed campaign, plus played) |
| Reuse BoardMoveSeed and the BoardMoveControlTests fixture | Facts added to the same class, `SeedScenarioAsync` extended with a defaulted board type |
| Consider a shared partial | Objective: not extracted, duplicate consistently, with reasons |
</success_criteria>

<output>
Create `.planning/quick/261019-gff-add-move-to-linked-board-control-to-the-/261019-gff-SUMMARY.md` when done.
</output>
