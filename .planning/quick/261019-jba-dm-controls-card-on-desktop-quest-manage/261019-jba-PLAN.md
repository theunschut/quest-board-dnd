---
phase: quick-261019-jba
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
autonomous: true
requirements: [quick-261019-jba]

estimate:
  tokens: 35000
  raw_tokens: 35000
  tasks: 2
  confidence: low

must_haves:
  truths:
    - "On desktop Quest Manage the first sidebar card is headed 'DM Controls', rendered exactly once, and the old card heading <h5>View Public Page</h5> is gone; the 'View Public Page' button (btn btn-secondary w-100, link to /Quest/Details/{id}) is still the card's first button."
    - "For a finalized one-shot quest with no follow-up, the DM Controls card holds, in this order: View Public Page, Create Follow-Up Quest (link to /Quest/CreateFollowUp/{id}, btn btn-primary w-100 mt-2), and the Send Reminder POST form to /Quest/SendReminder/{id} carrying an anti-forgery token and a full-width btn btn-info w-100 button."
    - "The finalized one-shot action row in the main card keeps Edit Quest, Open Quest and Refresh Data, and no longer contains Create Follow-Up Quest or Send Reminder; no other action row changes."
    - "Create Follow-Up Quest does not render once the quest has a follow-up, while Send Reminder still does."
    - "On a campaign board and on an unfinalized quest, neither Create Follow-Up Quest nor the Send Reminder form renders anywhere on desktop Manage; the DM Controls card still renders with View Public Page."
    - "For a super admin on a linked board, Move to Linked Board renders exactly once on desktop Manage, inside the DM Controls card after View Public Page, as btn btn-info w-100 mt-2 (enabled link to /Move/Quest/{id}, or disabled button plus p.move-control-reason#move-reason); visibility semantics are unchanged and nothing of it remains in the main card."
    - "The 'Send again (bypasses duplicate check)' form stays at the top of the main card, untouched."
    - "Manage.Mobile.cshtml is byte-for-byte unchanged: the phone layout deliberately keeps Create Follow-Up in its finalized row and the Move control under its action stack, has no Send Reminder and no DM Controls card. Quest Details, Event Details, controllers, services, repositories and CSS are unchanged."
    - "No source or test line added by this work carries a quick-task id, decision id, phase number or planning document name, and no clock read is added."
  artifacts:
    - path: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      provides: "Desktop DM Controls sidebar card holding View Public Page, Create Follow-Up Quest, Send Reminder and Move to Linked Board"
      contains: "<h5>DM Controls</h5>"
    - path: "QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs"
      provides: "Desktop facts for the DM Controls card contents, order and visibility conditions, plus one phone-layout pin"
      contains: "DM Controls"
    - path: "QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs"
      provides: "Desktop Manage move-control facts now pin the control inside the DM Controls card, once, full width"
      contains: "DM Controls"
  key_links:
    - from: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      to: "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs"
      via: "Send Reminder form in the DM Controls card posts to SendReminder with an anti-forgery token"
      pattern: "asp-action=\"SendReminder\" asp-route-id=\"@Model.Id\" method=\"post\" class=\"mt-2\""
    - from: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      to: "QuestBoard.Service/Controllers/MoveController.cs"
      via: "enabled Move link in the DM Controls card built with Url.Action(\"Quest\", \"Move\", new { id = Model.Id })"
      pattern: "Url\\.Action\\(\"Quest\", \"Move\", new \\{ id = Model\\.Id \\}\\)\" class=\"btn btn-info w-100 mt-2\""
    - from: "QuestBoard.Service/Views/Quest/Manage.cshtml"
      to: "QuestBoard.Service/Helpers/MoveCopy.cs"
      via: "disabled Move branch renders the reason via MoveCopy.ControlReason"
      pattern: "MoveCopy\\.ControlReason\\(moveControl\\.BlockReason\\)"
---

<objective>
On the DESKTOP Quest Manage page only, turn the right-hand "View Public Page" sidebar card into a
"DM Controls" card, mirroring the Quest Details DM Controls card, and move three controls out of
the main card into it: Create Follow-Up Quest, Send Reminder, and Move to Linked Board. Each keeps
its exact current visibility condition. The phone layout stays exactly as it is.

Purpose: DM-only actions sit in one predictable place on desktop, the same place Quest Details puts
them, instead of crowding the finalized action row (which today holds four buttons plus Refresh
Data) and trailing the Move control under whichever row rendered.

Output: restructured `Manage.cshtml`, a new `QuestManageDmControlsTests.cs`, and tightened desktop
Manage facts in `BoardMoveControlTests.cs`.

## Decisions taken in this plan (planner's discretion, justified)

- **Decomposition: two `type="auto"` tasks, no tracer task and no checkpoint.** The operator asked
  for an autonomous plan with no human-verify gate. This is a markup move inside an already-proven
  page with no new layer, so a tracer slice would add no information.
- **Card markup mirrors Quest Details** (`Details.cshtml` lines 755-786): bare `<h5>DM Controls</h5>`
  heading in `card-header modern-card-header`, first button full width with no top margin, every
  later button `w-100 mt-2`, disabled Move button followed by `p.move-control-reason mb-0 mt-2#move-reason`.
  No `<hr>` inside the card, matching the existing sidebar cards on Details and Manage rather than
  the full-page form guideline.
- **Order inside the card:** View Public Page, Create Follow-Up Quest, Send Reminder, Move to Linked
  Board. Navigation first, then the quest-lifecycle actions, then the super-admin-only move last,
  which is where Quest Details also puts it.
- **Send Reminder keeps its `btn-info` colour and envelope icon**; only the width changes, and the
  icon spacing becomes `me-2` like every other icon in the card. The spacing comes from `class="mt-2"`
  on the `<form>` so the button itself is a plain `btn btn-info w-100`.
- **Intentional desktop/mobile divergence:** `Manage.Mobile.cshtml` is NOT touched. The operator
  decided the phone layout stays as is: it keeps Create Follow-Up in its finalized row, keeps the
  Move control under its action stack, and has neither Send Reminder nor a View Public Page / DM
  Controls section. This must be recorded in the SUMMARY so nobody "fixes" the twin later. No
  comment about it goes into source.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.claude/ui-guidelines.md
@.planning/STATE.md
@.planning/quick/261019-gff-add-move-to-linked-board-control-to-the-/261019-gff-SUMMARY.md
@QuestBoard.Service/Views/Quest/Manage.cshtml
@QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs

## Interfaces and facts the executor needs (already verified against the tree)

Desktop `Manage.cshtml` today (line numbers at base commit f7b619cf):
- Lines 9-15: view locals `boardType` (`BoardType`), `boardToday`, `moveControl` (`MoveControlState?`
  from `ViewBag.MoveControl`). Everything from line 27 to 822 is inside the `else` of
  `@if (!(bool)ViewBag.IsAuthorized)`, so the whole sidebar is already DM/Admin-only.
- Lines 77-85: the "Send again (bypasses duplicate check)" form (renders only when
  `TempData["Success"] != null`). Leave it untouched.
- Lines 86-87: `@if (boardType != BoardType.Campaign)` then `@if (Model.IsFinalized)` wraps the
  finalized block; the finalized action row is lines 623-647. Inside its inner `d-flex gap-2`:
  Edit Quest link, Open Quest form, then `@if (Model.FollowUpQuest == null)` Create Follow-Up Quest
  link (`btn btn-primary`, `fa-scroll me-2`), then the Send Reminder form (`asp-action="SendReminder"
  asp-route-id="@Model.Id" method="post"`, `@Html.AntiForgeryToken()`, `btn btn-info`,
  `fa-envelope me-1`). Refresh Data sits on the right of the outer flex.
- Lines 650-680: the campaign action row (Edit, Close/Reopen, Delete | Refresh Data). Unchanged.
- Lines 681-698: `@if (moveControl != null)` block in a `div.mt-2` at the end of the main card body:
  enabled `a.btn.btn-info` to `Url.Action("Quest", "Move", new { id = Model.Id })` with
  `fa-right-left me-2` + `aria-hidden="true"`, or disabled `button type="button" class="btn btn-info
  disabled" aria-disabled="true" aria-describedby="move-reason"` followed by
  `<p class="move-control-reason mb-0 mt-2" id="move-reason">@MoveCopy.ControlReason(moveControl.BlockReason)</p>`.
- Lines 703-715: `div.col-md-4` sidebar; first card `card modern-card mb-3` whose header holds the
  heading text "View Public Page" in an `<h5>`, body holds the `btn btn-secondary w-100` link to
  `Url.Action("Details", "Quest", new { id = Model.Id })` with `fa-eye me-2`.
- Lines 717-718: the next card is headed `<h5>Quest Summary</h5>` — tests use it as the end marker
  of the DM Controls card. The finalized vote columns earlier on the page also use `col-md-4`, so
  never use `col-md-4` as a sidebar marker in tests.

Quest Details DM Controls card to mirror: `QuestBoard.Service/Views/Quest/Details.cshtml` lines 755-786.

Test fixtures:
- `WebApplicationFactoryBase` with `IClassFixture<WebApplicationFactoryBase>`; xunit runs
  non-parallel (`xunit.runner.json`). `factory.TestGroupContext` (`MutableGroupContext`) has settable
  `ActiveGroupId` (default 1) and `BoardType` (default OneShot) — reset both in `DisposeAsync` via
  `IAsyncLifetime`, exactly like `BoardMoveControlTests` lines 39-45.
- `QuestController.GetActiveBoardTypeAsync` reads the active group's `BoardType` from the DATABASE,
  so a campaign test needs a campaign group row: `TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2)`,
  a DM membership on group 2 (`BoardMoveSeed.SeedMembershipAsync(factory, userId, 2, GroupRole.DungeonMaster)`),
  the quest on group 2, and `factory.TestGroupContext.ActiveGroupId = 2` (pattern:
  `QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs` lines 19-70).
- `TestDataHelper.ClearDatabaseAsync(factory.Services)` recreates the DB and seeds group 1.
- `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, userName, email, roles: ["DungeonMaster"])`
  returns `(HttpClient client, UserEntity user)` with a group-1 DungeonMaster membership and
  `AllowAutoRedirect = false`.
- `BoardMoveSeed.SeedQuestAsync(factory, groupId, title, dmUserId, DateTime[] proposedDates, DateTime? finalizedDate = null, bool isClosed = false, int? originalQuestId = null)`
  returns the quest id; a non-null `finalizedDate` sets `IsFinalized`. A follow-up exists when a
  second quest is seeded with `originalQuestId:` pointing at the first. The Manage query includes
  `FollowUpQuest`, so the view sees it.
- Desktop and mobile user-agent strings and the `GetAsync(client, url, userAgent)` helper pattern:
  `BoardMoveControlTests.cs` lines 24-28 and 78-85. Use fixed far-future dates such as
  `new DateTime(2031, 5, 10, 19, 0, 0)`, never `DateTime.Now`/`Today`.
- Rendered URLs: `/Quest/Details/{id}`, `/Quest/CreateFollowUp/{id}`, form `action="/Quest/SendReminder/{id}"`,
  `/Move/Quest/{id}`; the anti-forgery hidden input renders as `<input name="__RequestVerificationToken" type="hidden" value="...">`.
- `BoardMoveStaticGuardTests` already scans both Manage views for planning references; it must stay green.
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Build the desktop DM Controls card on Quest Manage, test first</name>
  <files>QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs, QuestBoard.Service/Views/Quest/Manage.cshtml</files>
  <read_first>
    - .claude/ui-guidelines.md (card pattern, button layout, mobile twins note)
    - QuestBoard.Service/Views/Quest/Manage.cshtml lines 1-16, 75-90, 615-720
    - QuestBoard.Service/Views/Quest/Details.cshtml lines 754-787 (the card to mirror)
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml lines 195-220 and 480-505 (read only, to write the phone-layout pin; do not edit)
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs lines 20-100 (UA constants, GetAsync, IAsyncLifetime reset)
    - QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs lines 14-70 (campaign group 2 pattern)
    - QuestBoard.IntegrationTests/Helpers/BoardMoveSeed.cs lines 39-95 (SeedMembershipAsync, SeedQuestAsync)
  </read_first>
  <behavior>
    All facts request `/Quest/Manage/{id}` as the quest's own DM (group role DungeonMaster) with the
    desktop user agent unless stated. "Card segment" = the HTML between `<h5>DM Controls</h5>` and
    `<h5>Quest Summary</h5>`; "main segment" = the HTML before `<h5>DM Controls</h5>`.
    - Finalized one-shot quest, no follow-up: 200; `<h5>DM Controls</h5>` occurs exactly once; the
      page has no `<h5>View Public Page</h5>` but still has the "View Public Page" button text; the
      card segment contains `href="/Quest/Details/{id}"`, `href="/Quest/CreateFollowUp/{id}"` with
      "Create Follow-Up Quest", and `action="/Quest/SendReminder/{id}"` with "Send Reminder" and
      `__RequestVerificationToken`, in that order (IndexOf comparisons); the View Public Page link
      carries `btn btn-secondary w-100`, the follow-up link `btn btn-primary w-100 mt-2`, the reminder
      button `btn btn-info w-100`.
    - Same quest, main segment: contains "Edit Quest", "Open Quest" and "Refresh Data"; does not
      contain `/Quest/CreateFollowUp/`, `/Quest/SendReminder/`, "Create Follow-Up Quest" or "Send Reminder".
    - Finalized one-shot quest that already has a follow-up: page does not contain
      `/Quest/CreateFollowUp/{id}`; card segment still contains `action="/Quest/SendReminder/{id}"`.
    - Campaign board, finalized campaign quest on group 2 (marker "Close Quest" present): DM Controls
      card with `href="/Quest/Details/{id}"` present; page contains neither `/Quest/CreateFollowUp/`
      nor `/Quest/SendReminder/`.
    - Unfinalized one-shot quest with proposed dates (marker "Finalize Quest" present): DM Controls
      card with View Public Page present; page contains neither `/Quest/CreateFollowUp/` nor `/Quest/SendReminder/`.
    - Phone layout (mobile user agent), finalized one-shot quest, no follow-up: page still contains
      `href="/Quest/CreateFollowUp/{id}"` and contains no `<h5>DM Controls</h5>` — pins that the phone
      layout deliberately keeps its own arrangement.
  </behavior>
  <action>
    RED first. Create `QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs`,
    namespace `QuestBoard.IntegrationTests.Controllers`, class `QuestManageDmControlsTests(WebApplicationFactoryBase factory)`
    implementing `IClassFixture<WebApplicationFactoryBase>` and `IAsyncLifetime` (DisposeAsync resets
    `factory.TestGroupContext.ActiveGroupId = 1` and `BoardType = BoardType.OneShot`). Usings:
    `System.Net`, `System.Text.RegularExpressions`, `QuestBoard.Domain.Enums`, `QuestBoard.IntegrationTests.Helpers`.
    Copy the desktop and mobile user-agent constants and a private `GetAsync(client, url, userAgent)`
    helper in the `BoardMoveControlTests` style, plus small private helpers `CardSegment(html)` and
    `MainSegment(html)` that slice on the two `<h5>` markers described in the behavior block (assert
    both markers are found before slicing so a missing card fails loudly, not with an index error).
    Each fact starts with `TestDataHelper.ClearDatabaseAsync`, creates its own DM via
    `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(..., roles: ["DungeonMaster"])` with
    a unique user name per fact, and seeds quests via `BoardMoveSeed.SeedQuestAsync` with fixed 2031
    dates. The campaign fact follows the `QuestCampaignUiParityTests` group-2 pattern and also sets
    `factory.TestGroupContext.BoardType = BoardType.Campaign`. Write one fact per bullet in the
    behavior block, named in the repo's descriptive style (for example
    `FinalizedOneShotQuest_ShowsViewPublicPageCreateFollowUpAndSendReminderInTheDmControlsCard`).
    Give the class a plain-language XML summary in the style of `BoardMoveControlTests` lines 10-20:
    what is seeded, what the facts pin (DM Controls card contents, order and visibility conditions on
    desktop; the phone layout keeping its own arrangement on purpose), and that they check markup,
    not rendering. No requirement ids, quick-task ids, phase numbers or planning document names
    anywhere in the file. Build and run the new class; the desktop facts must fail against the
    current view (the phone-layout fact passes already — that is expected, it is a regression pin).

    GREEN. Edit `QuestBoard.Service/Views/Quest/Manage.cshtml` only:
    1. Finalized one-shot action row (inside `@if (boardType != BoardType.Campaign)` / `@if (Model.IsFinalized)`):
       delete the `@if (Model.FollowUpQuest == null)` Create Follow-Up Quest link and the Send
       Reminder form from the inner `d-flex gap-2`, leaving Edit Quest and the Open Quest form there
       and Refresh Data on the right. Change nothing else in that row or any other row.
    2. Delete the whole `@if (moveControl != null)` block at the end of the main card body (the
       `div.mt-2` after the campaign row).
    3. Sidebar first card: change the heading text inside its `<h5>` from View Public Page to
       `DM Controls` (bare `<h5>`, like Details). In its body keep the existing View Public Page link
       exactly as the first element, then append, in this order:
       a. `@if (boardType != BoardType.Campaign && Model.IsFinalized)` wrapping: an inner
          `@if (Model.FollowUpQuest == null)` Create Follow-Up Quest link to
          `Url.Action("CreateFollowUp", "Quest", new { id = Model.Id })` with class
          `btn btn-primary w-100 mt-2` and icon `fas fa-scroll me-2`; then the Send Reminder form
          with `asp-action="SendReminder" asp-route-id="@Model.Id" method="post" class="mt-2"`,
          containing `@Html.AntiForgeryToken()` and `button type="submit" class="btn btn-info w-100"`
          with icon `fas fa-envelope me-2` and text Send Reminder. These are the same visibility
          conditions the controls had in the finalized row (campaign boards and unfinalized quests
          never show them; Create Follow-Up additionally hides once a follow-up exists).
       b. `@if (moveControl != null)`: the enabled branch is the `Url.Action("Quest", "Move", new { id = Model.Id })`
          link with class `btn btn-info w-100 mt-2`; the disabled branch is the
          `button type="button" class="btn btn-info w-100 mt-2 disabled" aria-disabled="true" aria-describedby="move-reason"`
          followed by `<p class="move-control-reason mb-0 mt-2" id="move-reason">` rendering
          `MoveCopy.ControlReason(moveControl.BlockReason)`. Keep the `fa-right-left me-2` icon with
          `aria-hidden="true"` and the Move to Linked Board text. Same markup as Details lines 766-781,
          with `Model.Id` instead of `Model.Quest?.Id`. No wrapper div.
    4. Do not touch the "Send again" resend form near the top of the main card, the Quest Summary
       card, the `<script>` block, the `@using` list (MoveCopy and MoveControlState are already
       imported) or the view locals. Add no comments; if one is truly needed, write plain-language
       why-text with no planning references. Add no clock reads.
    5. Do NOT edit `Manage.Mobile.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`,
       `Events/Details.cshtml`, any controller, service, repository or CSS file. The phone layout's
       different arrangement is an operator decision, not drift.
    Re-run the new class and `BoardMoveControlTests` (its existing Manage facts must still pass
    unchanged at this point). Linux host: never set `DOTNET_GCHeapHardLimit`; if a run dies with
    `Fatal error. Internal CLR error. (0x80131506)`, re-run the same command once before treating it as real.
  </action>
  <verify>
    <automated>cd /mnt/Data/repos/quest-board-dnd && dotnet build && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~QuestManageDmControlsTests|FullyQualifiedName~BoardMoveControlTests"</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c '<h5>DM Controls</h5>' QuestBoard.Service/Views/Quest/Manage.cshtml` prints 1.
    - `grep -c 'class="btn btn-info w-100 mt-2' QuestBoard.Service/Views/Quest/Manage.cshtml` prints 2 (enabled Move link and disabled Move button).
    - `grep -c 'class="btn btn-primary w-100 mt-2"' QuestBoard.Service/Views/Quest/Manage.cshtml` prints 1.
    - `grep -c 'asp-action="SendReminder"' QuestBoard.Service/Views/Quest/Manage.cshtml` prints 2 (the untouched top form plus the card form).
    - `git diff --quiet f7b619cf -- QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml QuestBoard.Service/Views/Quest/Details.cshtml QuestBoard.Service/Views/Quest/Details.Mobile.cshtml QuestBoard.Service/Views/Events/Details.cshtml QuestBoard.Service/Controllers QuestBoard.Service/wwwroot QuestBoard.Domain QuestBoard.Repository` exits 0.
    - All `QuestManageDmControlsTests` facts and all `BoardMoveControlTests` facts pass.
  </acceptance_criteria>
  <done>
    Desktop Manage shows a single DM Controls sidebar card with View Public Page, then (finalized
    one-shot only) Create Follow-Up Quest (only without a follow-up) and Send Reminder, then Move to
    Linked Board (super admin on a linked board only), all full width; the finalized row keeps Edit
    Quest, Open Quest and Refresh Data; the phone layout and every other file are unchanged; the new
    test class is green and committed.
  </done>
</task>

<task type="auto">
  <name>Task 2: Pin the desktop Move control inside the DM Controls card and run the full sweep</name>
  <files>QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs</files>
  <read_first>
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs lines 100-115 and 258-361 (the Manage helpers and facts)
    - QuestBoard.Service/Views/Quest/Manage.cshtml (the DM Controls card as built in Task 1)
  </read_first>
  <action>
    In `BoardMoveControlTests.cs`, tighten the four desktop Manage theories without changing any
    mobile expectation (every existing assertion stays as it is for both user agents):
    1. Add a private static helper, for example `ShouldSitInsideTheDesktopDmControlsCard(string html, string controlMarkup)`,
       that asserts `<h5>DM Controls</h5>` occurs exactly once, finds it and the following
       `<h5>Quest Summary</h5>`, and asserts `controlMarkup` occurs exactly once in the page and lies
       between those two markers, after the card's "View Public Page" text.
    2. `SuperAdminOnALinkedBoard_SeesTheEnabledControlOnManage_InEveryQuestState`: when `userAgent`
       is the desktop constant, call the helper with the full opening tag
       `<a href="/Move/Quest/{questId}" class="btn btn-info w-100 mt-2">`. This covers all four quest
       states (open, no-dates, finalized, campaign), proving one placement regardless of state.
    3. `AQuestThatCannotMove_ShowsADisabledControlWithItsReasonOnManage`: when desktop, call the
       helper with `<button type="button" class="btn btn-info w-100 mt-2 disabled" aria-disabled="true" aria-describedby="move-reason">`
       and also assert the `id="move-reason"` paragraph lies inside the same card segment.
    4. `SuperAdminOnABoardOutsideAnySetOrInASetOfOne_SeesNoControlOnManage` and
       `BoardAdminOrDmOfBothBoards_NeverSeesTheControlOnManage`: when desktop, assert
       `<h5>DM Controls</h5>` is still present (the card does not depend on the move control) while
       the existing no-control assertions stay.
    Keep the class summary accurate (it already says the control is on the Details pages and on
    Quest Manage); add no planning references.

    Then run the sweep, in this order, from the repo root: `dotnet build`; the guard and neighbour
    filters below; then the full unit suite and the full integration suite, each with `--no-build`.
    Never set `DOTNET_GCHeapHardLimit`. A `Fatal error. Internal CLR error. (0x80131506)` is a
    transient flake: re-run that same command once. Integration tests use EF Core InMemory; no SQL
    Server is needed and none may be started.

    Finally confirm the scope and hygiene checks in the acceptance criteria. If any pre-existing test
    outside the two touched test files fails because of the move, fix the VIEW to restore the old
    behaviour it pins rather than editing that test, unless the test asserts the old desktop
    placement itself — then report it in the SUMMARY under Deviations with the reason.
  </action>
  <verify>
    <automated>cd /mnt/Data/repos/quest-board-dnd && dotnet build && dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~BoardMoveControlTests|FullyQualifiedName~QuestManageDmControlsTests|FullyQualifiedName~AntiForgeryTokenCoverageTests|FullyQualifiedName~QuestReminderTests|FullyQualifiedName~MobileViewsTests|FullyQualifiedName~BoardMoveStaticGuardTests|FullyQualifiedName~QuestCampaignUiParityTests|FullyQualifiedName~QuestControllerAuthorizationRegressionTests|FullyQualifiedName~QuestFinalizedEditTests|FullyQualifiedName~QuestStatusBoardClockTests|FullyQualifiedName~WallClockUnmovedTests" && dotnet test QuestBoard.UnitTests --no-build && dotnet test QuestBoard.IntegrationTests --no-build</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c 'DM Controls' QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs` prints at least 1.
    - The filtered run, the full unit suite and the full integration suite all report 0 failed (SQL Server-only skips are expected).
    - `git diff --quiet f7b619cf -- QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml QuestBoard.Service/Views/Quest/Details.cshtml QuestBoard.Service/Views/Quest/Details.Mobile.cshtml QuestBoard.Service/Views/Events/Details.cshtml QuestBoard.Service/Controllers QuestBoard.Service/wwwroot QuestBoard.Domain QuestBoard.Repository` exits 0.
    - `git diff --name-only f7b619cf -- QuestBoard.Service QuestBoard.Domain QuestBoard.Repository QuestBoard.UnitTests QuestBoard.IntegrationTests` lists exactly the three files in this plan's `files_modified`.
    - `git diff -U0 f7b619cf -- QuestBoard.Service/Views/Quest/Manage.cshtml QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs | grep '^+' | grep -nE '261019|\bD-[0-9]{2}\b|[Pp]hase [0-9]+|PLAN\.md|SUMMARY\.md|CONTEXT\.md|RESEARCH\.md'` prints nothing.
    - `git diff -U0 f7b619cf -- QuestBoard.Service/Views/Quest/Manage.cshtml | grep '^[-+]' | grep -c 'bypasses duplicate check'` prints 0 (the top "Send again" form is untouched).
  </acceptance_criteria>
  <done>
    Desktop Manage move-control facts prove the control sits once, full width, inside the DM
    Controls card in every quest state; mobile facts are unchanged and green; build, filtered
    guards, full unit and full integration suites are green; only the three planned files changed.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser -> QuestController.SendReminder | DM-triggered POST that enqueues reminder mail; must stay CSRF-protected after the form moves |
| Manage view -> viewer | DM-only controls rendered into HTML; must stay inside the authorized branch |
| browser -> MoveController / CreateFollowUp | links rendered by the card; server re-checks role and eligibility |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-jba-01 | Tampering (CSRF) | Send Reminder form in Manage.cshtml | medium | mitigate | Moved form keeps `@Html.AntiForgeryToken()` and a POST method; Task 1 fact asserts `__RequestVerificationToken` inside the card's form; `AntiForgeryTokenCoverageTests` (in the Task 2 filter) keeps `[ValidateAntiForgeryToken]` on every POST action |
| T-jba-02 | Information disclosure | DM Controls card | low | mitigate | The card lives in the existing `col-md-4` sidebar, which is inside the `else` of `!(bool)ViewBag.IsAuthorized`; Task 1 adds nothing outside that branch |
| T-jba-03 | Elevation of privilege | Move to Linked Board control | low | mitigate | Visibility still keyed on `moveControl != null`, which the controller sets only for the SuperAdmin role; `BoardMoveControlTests` board-Admin and DM facts stay green; MoveController re-checks server-side |
| T-jba-04 | Elevation of privilege | Create Follow-Up / Send Reminder visibility | low | accept | Visibility conditions are UX only and are preserved exactly; the CreateFollowUp and SendReminder actions enforce their own authorization, covered by `QuestControllerAuthorizationRegressionTests` |
</threat_model>

<verification>
- `dotnet build` succeeds with 0 errors.
- `QuestManageDmControlsTests` and `BoardMoveControlTests` green; `AntiForgeryTokenCoverageTests`,
  `QuestReminderTests`, `MobileViewsTests`, `BoardMoveStaticGuardTests` and the other Manage-touching
  classes in the Task 2 filter green.
- Full unit suite and full integration suite: 0 failed.
- Only `Manage.cshtml`, `QuestManageDmControlsTests.cs` and `BoardMoveControlTests.cs` changed under
  source and test projects; `Manage.Mobile.cshtml` byte-for-byte unchanged.
- No planning references in added lines; no clock reads added.
</verification>

<success_criteria>
- A DM on desktop Quest Manage finds View Public Page, Create Follow-Up Quest, Send Reminder and (for
  a super admin on a linked board) Move to Linked Board stacked full width in one "DM Controls"
  sidebar card, each under exactly its previous visibility condition.
- The finalized one-shot action row keeps Edit Quest, Open Quest and Refresh Data; the "Send again"
  form is where it was.
- The phone layout is unchanged, and the divergence is documented in the SUMMARY as deliberate.
</success_criteria>

<output>
Create `.planning/quick/261019-jba-dm-controls-card-on-desktop-quest-manage/261019-jba-SUMMARY.md` when done.
The SUMMARY MUST include a section titled "Desktop/mobile divergence (intentional)" stating: the
operator decided the phone layout of Quest Manage stays as is; `Manage.Mobile.cshtml` keeps Create
Follow-Up in its finalized row and the Move control under its action stack, and has no Send Reminder
and no View Public Page / DM Controls section; this is not drift and must not be "fixed" to match
desktop without a new operator decision. The `QuestManageDmControlsTests` phone-layout fact pins it.
Also list the commits, the test counts from the filtered and full runs, and state that visual
rendering was not checked in a browser.
</output>
