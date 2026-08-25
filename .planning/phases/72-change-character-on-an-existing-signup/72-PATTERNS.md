# Phase 72: Change Character on an Existing Signup - Pattern Map

**Mapped:** 2026-08-25
**Files analyzed:** 6 (2 new, 4 modified)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml` | component (Razor partial) | request-response (form POST) | `QuestBoard.Service/Views/ShopManagement/Index.cshtml` (`#denyModal`) | role+dataflow exact for the modal-priming shape; `_ShopItemDetailsContent.cshtml` used for the "partial takes a model" house style |
| `QuestBoard.Service/Views/Quest/Details.cshtml` | component (Razor view, host) | request-response | itself (`#addCharacterModal` block, two character cells) | exact — modifying in place |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` | component (Razor view, host) | request-response | `Details.cshtml` (desktop counterpart) + `ShopManagement/Index.Mobile.cshtml` for the mobile modal-trigger idiom | role-match |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (`UpdateSignupCharacter`, `ViewBag.UserCharacters`) | controller | CRUD (single-field update) | `QuestController.RemovePlayerSignup` (`GroupId` check idiom) and `QuestController.UpdateSignup` (existing-signup lookup + `BadRequest` idiom) | exact — same controller, adjacent actions |
| `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` | test | request-response | `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` | exact — same controller, adjacent action, same fixture usage |
| (cross-group case within the above test file) | test | request-response / tenant-isolation | `QuestBoard.IntegrationTests/.../TenantIsolationTests.cs` | role-match — only pattern in repo for the query-filter boundary |

## Pattern Assignments

### `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml` (new partial, request-response)

**Primary analog — modal priming shape:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml`

**Trigger button** (`ShopManagement/Index.cshtml:93-97`):
```cshtml
<button type="button" class="btn btn-danger btn-sm btn-action" title="Deny"
        data-bs-toggle="modal" data-bs-target="#denyModal"
        data-item-id="@item.Id"
        data-item-name="@item.Name">
    <i class="fas fa-times"></i>
</button>
```

**Modal + form** (`ShopManagement/Index.cshtml:455-492`):
```cshtml
<div class="modal fade" id="denyModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <form id="denyForm" method="post">
                @Html.AntiForgeryToken()
                <div class="modal-header border-secondary">
                    <h5 class="modal-title">Deny Item</h5>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    <p>You are about to deny: <strong id="denyItemName"></strong></p>
                    ...
                </div>
                <div class="modal-footer border-secondary">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    <button type="submit" class="btn btn-danger">Deny Item</button>
                </div>
            </form>
        </div>
    </div>
</div>
```

**Priming script** (`ShopManagement/Index.cshtml:501-517`):
```javascript
document.addEventListener('DOMContentLoaded', function() {
    const denyModal = document.getElementById('denyModal');
    if (denyModal) {
        denyModal.addEventListener('show.bs.modal', function(event) {
            const button = event.relatedTarget;
            const itemId = button.getAttribute('data-item-id');
            const itemName = button.getAttribute('data-item-name');

            const form = document.getElementById('denyForm');
            form.action = '/ShopManagement/Deny/' + itemId;

            document.getElementById('denyItemName').textContent = itemName;
            document.getElementById('denialReason').value = '';
        });
    }
});
```

**Copy exactly:** the `id="denyModal"`/`show.bs.modal`/`event.relatedTarget`/`data-*` attribute shape. Rename `data-item-id`/`data-item-name` → `data-quest-id`/`data-current-character-id`. The Remove-button toggle (show/hide based on whether `data-current-character-id` is empty) has no existing analog for the "toggle visibility of a footer button" part — this is new wiring but attaches to the same `show.bs.modal` listener.

**Source block being extracted (existing `#addCharacterModal`):** `Details.cshtml:819-863` — read verbatim below; this is the literal starting point for the new partial before generalizing add/change/clear:
```cshtml
<!-- Add Character Modal -->
<div class="modal fade" id="addCharacterModal" tabindex="-1" aria-labelledby="addCharacterModalLabel" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <div class="modal-header border-secondary">
                <h5 class="modal-title" id="addCharacterModalLabel">
                    <i class="fas fa-user-plus me-2"></i>Add Character to Signup
                </h5>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <form asp-action="UpdateSignupCharacter" method="post" id="addCharacterForm">
                <div class="modal-body">
                    <input type="hidden" name="questId" value="@Model.Quest?.Id" />
                    <div class="alert alert-info">
                        <i class="fas fa-info-circle me-2"></i>
                        Select a character to add to your quest signup. This character will be saved to the database.
                    </div>
                    <div class="mb-3">
                        <label for="characterSelect" class="form-label">Select Character <span class="text-danger">*</span></label>
                        <select name="characterId" id="characterSelect" class="form-select" required>
                            <option value="">-- Select a character --</option>
                            @foreach (var character in ViewBag.UserCharacters as List<Character> ?? new List<Character>())
                            {
                                var classList = string.Join(", ", character.Classes.Select(c => $"{c.Class} {c.ClassLevel}"));
                                <option value="@character.Id">
                                    @character.Name - Level @character.Level (@classList)
                                </option>
                            }
                        </select>
                    </div>
                </div>
                <div class="modal-footer border-secondary">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                        <i class="fas fa-times me-2"></i>Cancel
                    </button>
                    <button type="submit" class="btn btn-success">
                        <i class="fas fa-check me-2"></i>Add Character
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>
```
Note the existing option text pattern (`"{Name} - Level {Level} ({classList})"`) must survive into the new partial with the D-11 status suffix appended (`" (Retired)"` etc.) only for non-Active characters.

**House-style reference for a shared partial receiving inputs — `_ShopItemDetailsContent.cshtml:1-4`:**
```cshtml
@using QuestBoard.Service.ViewModels.ShopViewModels
@using QuestBoard.Domain.Enums
@using QuestBoard.Service.Extensions
@model ShopItemViewModel
```
This is the codebase's only `Views/Shared/` partial that takes a typed `@model`. All other `Views/Shared/` partials (`_Toasts.cshtml`, `_Calendar.cshtml`, `_MarkdownEditor.cshtml`) are model-less and read `ViewBag`/`TempData` directly — matching this phase's data source (`ViewBag.UserCharacters`, already the established idiom per D-12). **Recommendation for the planner:** follow the model-less convention (no strongly-typed `@model`) since `_CharacterSelectModal.cshtml` reads `ViewBag.UserCharacters` exactly like the block it's extracted from, and every other `Views/Shared/` partial in this codebase that reads `ViewBag`/`TempData` does so without a `@model`. Only `_ShopItemDetailsContent.cshtml` breaks that pattern, and it does so because it's rendered via AJAX/`PartialAsync` with an explicit item — not this phase's shape.

**Toast wiring — no changes needed.** `_Toasts.cshtml` (`Views/Shared/_Toasts.cshtml:1-15`) already reads `TempData["Success"]`/`["Error"]` and is rendered by both `_Layout.cshtml` and `_Layout.Mobile.cshtml`. D-14/D-15 only require the controller to set `TempData["Success"]`/`["Error"]` before redirecting — zero view work.

---

### `QuestBoard.Service/Views/Quest/Details.cshtml` (host view, modified)

**Diff between the two existing character cells (finalized vs. waitlist) — what the extracted trigger markup must parameterize:**

Finalized cell (`Details.cshtml:116-145`, iteration var `participant`):
```cshtml
@if (participant.Character != null)
{
    <div class="d-flex align-items-center">
        <img src="@Url.Action("GetCroppedPicture", "Characters", new { id = participant.Character.Id })"
             alt="@participant.Character.Name" class="character-mini-avatar me-2" .../>
        <span class="character-mini-avatar-placeholder me-2" ...><i class="fas fa-user"></i></span>
        <span>@participant.Character.Name</span>
    </div>
}
else
{
    <div class="d-flex align-items-center gap-2">
        <span class="text-muted fst-italic">No character</span>
        @if (isCurrentUser && ViewBag.UserCharacters != null && ((List<Character>)ViewBag.UserCharacters).Any())
        {
            <button type="button" class="btn btn-sm btn-success"
                    data-bs-toggle="modal" data-bs-target="#addCharacterModal" title="Add character">
                <i class="fas fa-plus"></i>
            </button>
        }
    </div>
}
```

Waitlist cell (`Details.cshtml:232-260`, iteration var `player`) — **byte-for-byte identical structure**, only the loop variable name differs (`participant` → `player`) and `isCurrentUser` is computed slightly earlier in scope. No other markup, class, or conditional differs between the two cells today. This confirms the extraction target: a single partial/local helper parameterized on `(Character? character, bool isCurrentUser, int? questId)` collapses both blocks with zero behavioral difference to preserve.

**Where the pencil/`+` trigger buttons must be added:** inside the `if (participant.Character != null)` branch (pencil, new — no `.Any()` gate per D-03) and the existing `else` branch's `+` button (keep, but replace its visibility gate per D-03's OR rule).

**Signup-time selects that widen per D-12** — two sites in this file:
- `:333` (`finalizedQuestCharacter`, "Join This Quest" panel) — verbatim shown in RESEARCH.md Code Examples
- `:419` (`CharacterId` select in the plain `Details` POST form)

Both currently iterate `ViewBag.UserCharacters as List<QuestBoard.Domain.Models.Character>` — no local filtering, so widening the single source (`QuestController.cs:337`) is sufficient; no view-side change needed at these two sites beyond the option-text status suffix if D-11's suffix is added centrally.

**Remove-guard idiom to mirror** — `revokeSignup()` (`Details.cshtml:866-878`):
```javascript
function revokeSignup(questId) {
    if (confirm("Are you sure you want to revoke your signup for this quest? This action cannot be undone.")) {
        const formData = new FormData();
        formData.append('__RequestVerificationToken', '@tokens.RequestToken');
        fetch(`/Quest/RevokeSignup/${questId}`, {
            method: "DELETE",
            body: formData
        }).then(res => {
            if (res.ok) { location.reload(); } else { res.text().then(text => { /* ... */ }); }
        });
    }
}
```
D-07 mirrors the `confirm()` guard only — the Remove button in `_CharacterSelectModal.cshtml` submits the existing form (see RESEARCH.md's `characterRemoveBtn` JS), not a separate `fetch`.

---

### `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (host view, modified)

**Analog:** `Details.cshtml` (desktop counterpart, same data) for the character-display data shape; `ShopManagement/Index.cshtml` for the trigger idiom (identical priming approach applies regardless of platform).

**Current bare rendering — participant row** (`Details.Mobile.cshtml:207-218`):
```cshtml
<div class="participant-row d-flex justify-content-between align-items-center py-2 border-bottom @(isCurrentUser ? "bg-dark rounded px-2" : "")">
    <div>
        <span class="fw-bold">@participant.Player.Name</span>
        @if (isCurrentUser) { <span class="badge bg-info ms-1">You</span> }
        <br>
        <small class="text-muted">@(participant.Character?.Name ?? "No character")</small>
    </div>
    <span class="badge @roleBadge">@roleText</span>
</div>
```

**Waitlist row** (`Details.Mobile.cshtml:230-241`) — same `<small class="text-muted">@(player.Character?.Name ?? "No character")</small>` shape, second column is the vote badge instead of role badge.

Per D-02, the pencil must render **inline on the same `<small>` line**, immediately after the character name, subordinate in size to the `<small>` text (e.g. a tiny icon-only button, no `btn-sm` padding that grows the line). No existing mobile analog for an inline icon inside a `<small>` — this is new composition, but the `data-bs-toggle="modal"` / `data-quest-id` / `data-current-character-id` attributes are identical to the desktop trigger, just placed inside the `<small>` rather than the flex row.

**Signup-time select at `:295`** (`finalizedQuestCharacterMobile`) — same widen-at-source treatment as desktop's `:333`/`:419`, no local view change needed beyond the shared option-text status suffix.

---

### `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (controller, CRUD)

**Analog for the existing-signup lookup + `BadRequest` idiom — `UpdateSignup`** (`QuestController.cs:496-518`, sibling action, has an `IsFinalized` guard that `UpdateSignupCharacter` correctly lacks):
```csharp
[Authorize]
public async Task<IActionResult> UpdateSignup(int questId, List<PlayerDateVote> dateVotes)
{
    var quest = await questService.GetQuestWithDetailsAsync(questId);
    if (quest == null || quest.IsFinalized)
        return NotFound();

    var user = await userService.GetUserAsync(User);
    if (user == null)
        return Challenge();

    var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
    if (playerSignup == null)
    {
        return BadRequest("You are not signed up for this quest.");
    }

    await playerSignupService.UpdatePlayerDateVotesAsync(playerSignup.Id, dateVotes);
    return RedirectToAction("Details", new { id = questId });
}
```

**Current `UpdateSignupCharacter` in full** (`QuestController.cs:520-555`) — this is the exact block to modify per D-10/D-13/D-14/D-15:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize]
public async Task<IActionResult> UpdateSignupCharacter(int questId, int? characterId)
{
    var quest = await questService.GetQuestWithDetailsAsync(questId);
    if (quest == null)
        return NotFound();

    var user = await userService.GetUserAsync(User);
    if (user == null)
        return Challenge();

    var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
    if (playerSignup == null)
    {
        return BadRequest("You are not signed up for this quest.");
    }

    // Validate character if provided
    if (characterId.HasValue)
    {
        var character = await characterService.GetCharacterWithDetailsAsync(characterId.Value);
        if (character == null || character.OwnerId != user.Id || character.Status != CharacterStatus.Active)
        {
            return BadRequest("Invalid character selection.");
        }
    }

    // Update the character
    await playerSignupService.UpdateSignupCharacterAsync(playerSignup.Id, characterId);

    return RedirectToAction("Details", new { id = questId });
}
```
Changes needed: drop `character.Status != CharacterStatus.Active` (D-10); add the `GroupId` check below (D-13); the "not signed up" `BadRequest` becomes `TempData["Error"]` + redirect (D-15, reachable-without-tampering path); the cross-group case stays `BadRequest` (D-15).

**`GroupId` check idiom to mirror — `RemovePlayerSignup`** (`QuestController.cs:640-646`):
```csharp
// An Admin caller only has authority over their own active group's signups. Without
// this check, an Admin in one group could delete a signup on another group's quest by
// guessing its id, since the AdminOnly policy alone only confirms the caller's role.
if (activeGroupContext.ActiveGroupId is not { } groupId || signup.Quest.GroupId != groupId)
{
    return NotFound();
}
```
For `UpdateSignupCharacter`, mirror this shape but check `character.GroupId` and return `BadRequest` (not `NotFound`) per D-15, with a plain-language comment (no phase/requirement IDs per CLAUDE.md) explaining the filter is the primary control and this is defense-in-depth insurance against a future `.IgnoreQueryFilters()` regression — see RESEARCH.md's Code Examples section for the exact recommended comment framing.

**`ViewBag.UserCharacters` population — current single source** (`QuestController.cs:326-337`):
```csharp
if (currentUser != null)
{
    var allCharacters = await characterService.GetCharactersByOwnerIdAsync(currentUser.Id, token);
    userCharacters = allCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
}
...
ViewBag.UserCharacters = userCharacters ?? new List<Character>();
```
D-12 widens this at the `.Where(c => c.Status == CharacterStatus.Active)` line — remove the filter (list all owned, active-group characters; `GetCharactersByOwnerIdAsync` is already group-scoped via the EF Core query filter). No other read site needs modification since all six already consume `ViewBag.UserCharacters` directly.

---

### `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` (new test file)

**Primary analog — `QuestJoinFinalizedQuestTests.cs`** (full fixture pattern, `:1-46`):
```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestJoinFinalizedQuestTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsPlayer_CreatesWaitlistedSignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm1", "joindm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Full Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 4; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"seated{i}", $"seated{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, seatedPlayer.Id, isSelected: true);
        }

        var (playerClient, newJoiner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "newjoiner1", "newjoiner1@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "0" // Player
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == newJoiner.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.IsSelected.Should().BeFalse();
    }
    // ... more [Fact] methods, same shape: Arrange (ClearDatabase → CreateTestQuestAsync → CreatePlayerSignupAsync)
    //     → authenticated client → FormUrlEncodedContent POST → Act → Assert via a fresh scoped DbContext query
}
```
Copy verbatim: constructor-injected `factory`, `CreateNonRedirectingClient()`, `ClearDatabaseAsync` at the top of every test, `CreateTestQuestAsync`/`CreatePlayerSignupAsync` for fixtures, `CreateAuthenticatedClientWithUserAsync` for the acting player, `FormUrlEncodedContent` for the POST body, and asserting via a fresh `factory.Services.CreateScope()` + `QuestBoardContext` query rather than trusting the response body.

**Cross-group case — `TenantIsolationTests.cs`'s mutable-singleton pattern** (`:13-21`, `:40-58`):
```csharp
// IAsyncLifetime — reset singleton group context after each test class run so that
// test state does not bleed into subsequently-executed test classes.
public ValueTask InitializeAsync() => ValueTask.CompletedTask;

public ValueTask DisposeAsync()
{
    factory.TestGroupContext.ActiveGroupId = 1;
    return ValueTask.CompletedTask;
}

// ... in a [Fact]:
await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all for seeding)
ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
// ... seed an entity with GroupId = 2 directly via this bypass-filter context

factory.TestGroupContext.ActiveGroupId = 1;
var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
    factory, "isolationviewer1", "isolationviewer1@example.com");
var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);
```
For SIGNCHAR-07: use `factory.Database.CreateContext()` (bypasses the query filter) to seed a character with `GroupId = 2` via `TestDataHelper.CreateTestCharacterAsync(..., groupId: 2)` (already accepts `groupId` per RESEARCH.md), set `factory.TestGroupContext.ActiveGroupId = 1` for the acting client, then POST `UpdateSignupCharacter` with that character's id and assert `BadRequest`. Implement `IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime` and reset `ActiveGroupId = 1` in `DisposeAsync` if the new test class mutates it, matching this class's own hygiene convention.

**Real mobile User-Agent pattern — `MobileViewsTests.cs`** (`:15-36`):
```csharp
private const string MobileUserAgent =
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string url, string userAgent)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}
```
For SIGNCHAR-02: reuse this exact `HttpRequestMessage` + `TryAddWithoutValidation("User-Agent", ...)` + `SendAsync` shape (either copy the constant/helper locally or extend an existing shared test base if one exists) rather than relying on the default `HttpClient` (desktop UA) or any devtools-style emulation.

## Shared Patterns

### `show.bs.modal` + `event.relatedTarget` modal priming
**Source:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml:93-97, 455-492, 501-517`
**Apply to:** `_CharacterSelectModal.cshtml` (the modal itself) and both `Details.cshtml`/`Details.Mobile.cshtml` (the trigger buttons that carry `data-quest-id`/`data-current-character-id`)

### Native `confirm()` destructive-action guard
**Source:** `QuestBoard.Service/Views/Quest/Details.cshtml:866-878` (`revokeSignup()`)
**Apply to:** the Remove-character button's click handler inside `_CharacterSelectModal.cshtml`

### `TempData["Success"]`/`["Error"]` → toast, zero view wiring
**Source:** `QuestBoard.Service/Views/Shared/_Toasts.cshtml:1-15`, wired into `_Layout.cshtml` and `_Layout.Mobile.cshtml`
**Apply to:** `QuestController.UpdateSignupCharacter` — set `TempData["Success"]` on swap/clear (D-14) and `TempData["Error"]` on the reachable-without-tampering "not signed up" case (D-15)

### Explicit `GroupId` check alongside an EF Core query filter (belt-and-suspenders)
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:640-646` (`RemovePlayerSignup`)
**Apply to:** `QuestController.UpdateSignupCharacter` — check `character.GroupId` against `activeGroupContext.ActiveGroupId`, return `BadRequest` (not `NotFound`, per D-15) on mismatch

### Integration test fixture shape (Arrange/Act/Assert via fresh scoped DbContext)
**Source:** `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs`
**Apply to:** `QuestUpdateSignupCharacterTests.cs` for all non-cross-group cases (SIGNCHAR-01, 03, 04, 05, 06, and the same-group/cross-user half of 07)

### Mutable `TestGroupContext.ActiveGroupId` singleton for query-filter boundary tests
**Source:** `QuestBoard.IntegrationTests/.../TenantIsolationTests.cs`
**Apply to:** the cross-group half of SIGNCHAR-07 in `QuestUpdateSignupCharacterTests.cs`

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| — | — | — | None — every file in this phase has a strong, verified analog in the existing codebase. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/{Shop,ShopManagement,Quest,Shared}`, `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.IntegrationTests/{Controllers,Mobile,Tests}`
**Files scanned:** 12 (read directly this session, in addition to RESEARCH.md's prior verified reads)
**Pattern extraction date:** 2026-08-25
