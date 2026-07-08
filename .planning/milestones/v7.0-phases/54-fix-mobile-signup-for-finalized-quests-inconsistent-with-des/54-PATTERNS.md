# Phase 54: Fix mobile signup for finalized quests - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 6 (3 modified, 3 test files new/extended)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (add Join card, D-01/D-02) | component (Razor view) | request-response (form POST) | `QuestBoard.Service/Views/Quest/Details.cshtml` lines 293-382 (desktop's `userCanJoin` card) | exact — this phase is a verbatim structural port, same ViewBag/Model surface, only container CSS class differs |
| `QuestBoard.Service/Views/Quest/Details.cshtml` (D-06 copy update) | component (Razor view) | request-response | same file, line 314 (the line being edited) | exact — in-place edit, not a new pattern |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `JoinFinalizedQuest` capacity branch (D-03/D-05) | controller (action method) | CRUD (create `PlayerSignup`) | same file — `Signup` action's identical recompute-then-decide pattern is the internal analog; no external controller needed | exact — editing the action in place, following its own established shape |
| `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (new `[Fact]`s for D-01/D-02/D-06) | test (integration) | request-response (HTTP GET + HTML assertion) | Same file — `MobileQuestDetails_MobileUserAgent_RendersVoteButtons` (lines 172-189) and `MobileQuestDetails_MobileUserAgent_ParticipantListIsStacked` (lines 194-213) | exact — same class, same User-Agent-switching harness, same `/Quest/Details/{id}` route |
| `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` (new file, D-03/D-05) | test (integration) | request-response (HTTP POST + DB-state assertion) | `QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs` — `Signup_Post_WhenAuthenticated_ShouldAddPlayerToQuest` (lines 121-152) and `Finalize_Post_WhenQuestOwner_ShouldFinalizeQuest` (lines 180-219) | role-match — same controller, same POST-then-assert-DB-state shape; no existing test hits `JoinFinalizedQuest` itself |
| `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` (extend, D-03 + waitlist integration) | test (unit) | CRUD / ordering (waitlist query) | same file — `GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime` (lines 270-298) | exact — same fixture/helpers (`SeedQuestAndUserAsync`, `MakeSignupEntity`), same `LastVoteChangeTime`/`SignupTime` fallback shape a `JoinFinalizedQuest`-created row will have |

## Pattern Assignments

### `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/Quest/Details.cshtml` lines 293-382 (desktop `userCanJoin` card), container styling ported from mobile's own sibling cards (lines 105-141, 146-179 in `Details.Mobile.cshtml` itself)

**Insertion point** (after mobile's waitlist section, before the DM manage link — `Details.Mobile.cshtml` lines 254/257):
```csharp
    @* Waitlist (finalized quests) *@
    @if (Model.Quest?.IsFinalized == true && waitlistPlayers.Count > 0)
    {
        ...
    }
    @* <-- NEW "Join This Quest" card inserted here, per D-02 --> *@

    @* DM controls link *@
    @if (canManage)
    {
```

**Variables needed before the new block** (mirror desktop's computation, `Details.cshtml` lines 37-40 — mobile already computes `isPlayerSignedUp`/`canManage`/`boardType` at lines 12-14, but `selectedPlayersCount`/`hasSpace`/`userCanJoin` are new to the mobile file):
```csharp
var selectedPlayersCount = Model.Quest?.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count() ?? 0;
var hasSpace = selectedPlayersCount < Model.Quest?.TotalPlayerCount;
var userCanJoin = User.Identity?.IsAuthenticated == true && !isPlayerSignedUp;
```

**Card container pattern** — copy mobile's own sibling-card convention (`Details.Mobile.cshtml` lines 105-108), NOT desktop's `card modern-card` (Pitfall 3 in RESEARCH.md):
```html
<div class="quest-section-card-mobile mb-3">
    <h6 class="quest-section-heading mb-3">
        <i class="fas fa-user-plus me-1"></i>Join This Quest
    </h6>
    ...
</div>
```

**3-button + character-select form structure** — ported verbatim from desktop, `Details.cshtml` lines 320-368 (character select) and lines 336-368 (three forms):
```html
<form asp-action="JoinFinalizedQuest" method="post" class="d-inline" id="joinPlayerForm">
    <input type="hidden" name="questId" value="@Model.Quest?.Id" />
    <input type="hidden" name="selectedRole" value="0" />
    <input type="hidden" name="characterId" id="playerCharacterId" value="" />

    <button type="submit" class="btn btn-primary w-100">
        <i class="fas fa-users me-2"></i>Join as Player
        <small class="d-block mt-1" style="font-size: 0.8em;">Participate in the quest (counts toward player limit)</small>
    </button>
</form>
```
Per D-01, use the `...Mobile` ID suffix (`playerCharacterIdMobile`, etc. — see 54-UI-SPEC.md Implementation Notes) to avoid collision with desktop's unqualified IDs, even though the two views never render simultaneously.

**Inline JS character-sync script** — ported verbatim in shape, IDs suffixed, from desktop `Details.cshtml` lines 370-378:
```html
<script>
    document.getElementById('finalizedQuestCharacter').addEventListener('change', function() {
        const characterId = this.value;
        document.getElementById('playerCharacterId').value = characterId;
        document.getElementById('assistantCharacterId').value = characterId;
        document.getElementById('spectatorCharacterId').value = characterId;
    });
</script>
```

**Antiforgery:** No explicit `@Html.AntiForgeryToken()` needed — `asp-action` tag helper auto-injects it, exactly like desktop's existing forms (confirmed pattern, both `Details.cshtml` lines 337/348/359 and `Details.Mobile.cshtml`'s own existing forms at lines 109-111/150-151 which DO call `@Html.AntiForgeryToken()` explicitly for non-tag-helper forms — but the `JoinFinalizedQuest` forms use `asp-action`, matching desktop's approach exactly, so omit the explicit call to stay consistent with the analog being ported).

**Full markup block to use (from 54-UI-SPEC.md, already verified against desktop's structure and mobile's CSS conventions):** see `54-UI-SPEC.md` "Implementation Notes" section 1 (lines 117-197) — this is the authoritative, ready-to-use markup block; do not re-derive it from scratch.

---

### `QuestBoard.Service/Views/Quest/Details.cshtml` (component, request-response) — D-06 copy only

**Analog:** the exact line being edited, `Details.cshtml` line 314

**Current (to be replaced):**
```html
<p class="mb-0"><i class="fas fa-info-circle text-warning me-1"></i>Player slots full, but you can join as Assistant DM or Spectator!</p>
```

**New (locked wording from 54-UI-SPEC.md Copywriting Contract, must match mobile's copy word-for-word per D-06):**
```html
<p class="mb-0"><i class="fas fa-info-circle text-warning me-1"></i>Player slots full &mdash; joining as a Player will place you on the waitlist. You can also join as Assistant DM or Spectator.</p>
```
Icon (`fa-info-circle text-warning`) stays unchanged — only the text changes.

---

### `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `JoinFinalizedQuest` (controller, CRUD)

**Analog:** itself — the existing recompute-then-decide shape (lines 438-449) and the existing `IsSelected` assignment (line 479) are the two halves that must be joined into one conditional. No external controller pattern needed; this is a self-contained action-method edit.

**Current capacity-reject block (lines 438-449, to be changed per D-03):**
```csharp
// Check if quest has space - only count Player roles
if (role == SignupRole.Player)
{
    var selectedPlayersCount = quest.PlayerSignups
        .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
        .Count();

    if (selectedPlayersCount >= quest.TotalPlayerCount)
    {
        ModelState.AddModelError("", $"This quest is full ({selectedPlayersCount}/{quest.TotalPlayerCount} players).");
        return RedirectToAction("Details", new { id = questId });
    }
}
```

**Current signup creation (lines 472-486, `IsSelected` currently hardcoded `true`):**
```csharp
var signup = new PlayerSignup
{
    Player = user,
    Quest = quest,
    CharacterId = characterId,
    Role = role,
    IsSelected = true, // Auto-approve all roles when joining finalized quest
    DateVotes = role == SignupRole.Spectator ? [] :
        [new PlayerDateVote
        {
            ProposedDateId = finalizedProposedDate.Id,
            Vote = VoteType.Yes
        }]
};
```

**Required change shape:** compute a `bool` (e.g. `hasSpace` or inline) from the existing recompute in the capacity-check block — keep the block, drop only the `ModelState.AddModelError` + early `return`, and thread the boolean into the `IsSelected` assignment:
```csharp
// Recompute space server-side — never trust client state (existing pattern, unchanged)
var isPlayerRoleWithSpace = role != SignupRole.Player
    || quest.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count() < quest.TotalPlayerCount;

// ... later, in the signup creation:
IsSelected = isPlayerRoleWithSpace, // Player joins waitlist if full; AssistantDM/Spectator always auto-approved (D-05)
```
D-05 requirement: only the `Player`-role branch changes — `AssistantDM`/`Spectator` remain always `IsSelected = true`, which the above expression preserves (`role != SignupRole.Player` short-circuits to `true` for those roles).

**Do NOT call** `PromoteNextWaitlistedPlayerIfSeatFreedAsync` from this action (Pitfall 1) — a new join never frees a seat.

---

### `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (test, integration, request-response)

**Analog:** same file — `MobileQuestDetails_MobileUserAgent_RendersVoteButtons` (lines 172-189) for the GET+User-Agent+assert-HTML shape; `MobileQuestDetails_MobileUserAgent_ParticipantListIsStacked` (lines 194-213) for the finalized-quest-with-seeded-signups seeding shape.

**Imports pattern** (file-level, lines 1-5):
```csharp
using System.Net;
using System.Net.Http.Headers;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
```

**Core pattern to copy (seeded finalized quest + mobile UA + authenticated non-signed-up player, lines 194-213):**
```csharp
[Fact]
public async Task MobileQuestDetails_MobileUserAgent_ParticipantListIsStacked()
{
    var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_qview02", "dm_qview02@test.com", name: "DM Qview02");
    var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Stacked Quest", isFinalized: true);
    var player = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "player_qview02a", "player_qview02a@test.com", name: "Player Alpha");
    await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, isSelected: true);
    var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_qview02b", "player_qview02b@test.com");

    var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
    request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
    request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("participant-row");
}
```

**New test(s) to add — model on the above, asserting new-card presence/absence/content:**
- Positive: authenticated + not-signed-up + finalized quest -> `html.Should().Contain("Join This Quest")` and form action markers (e.g. `joinPlayerFormMobile` id or `JoinFinalizedQuest` action attribute), positioned after waitlist markup.
- Negative (already signed up): seed `CreatePlayerSignupAsync(..., isSelected: true)` for the requesting user itself -> card must NOT render.
- Negative (unauthenticated): no `Authorization` header -> card must NOT render (or redirects to login, matching desktop's `userCanJoin` gate).
- D-06 copy: seed quest at capacity (`TotalPlayerCount` selected Player signups) -> `html.Should().Contain("joining as a Player will place you on the waitlist")` (exact locked D-06 string).

**Seeding helper reused unchanged:** `TestDataHelper.CreateTestQuestAsync(..., isFinalized: true, finalizedDate: ...)`, `CreatePlayerSignupAsync(..., isSelected: true)` × `TotalPlayerCount` (default `TotalPlayerCount = 4`, see `TestDataHelper.cs` line 33) to reach capacity.

---

### `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` (new file, test, integration, request-response)

**Analog:** `QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs` — class shape (lines 1-9), `Signup_Post_WhenAuthenticated_ShouldAddPlayerToQuest` (lines 121-152) for POST-form + DB-assert shape, `Finalize_Post_WhenQuestOwner_ShouldFinalizeQuest` (lines 180-219) for the finalized-quest arrange pattern.

**Imports pattern** (lines 1-2 of the analog file):
```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;
```

**Class declaration pattern:**
```csharp
public class QuestJoinFinalizedQuestTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();
    ...
}
```

**Core POST + DB-assert pattern (from `Signup_Post_WhenAuthenticated_ShouldAddPlayerToQuest`, lines 121-152 — adapt route/fields to `JoinFinalizedQuest`):**
```csharp
[Fact]
public async Task JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsPlayer_CreatesWaitlistedSignup()
{
    // Arrange
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm1", "joindm1@example.com");
    var quest = await TestDataHelper.CreateTestQuestAsync(
        factory.Services, dm.Id, "Full Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
    var proposedDate = await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

    // Fill quest to TotalPlayerCount (4, TestDataHelper default) with selected Player signups
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
    signup!.IsSelected.Should().BeFalse("D-03: a full quest waitlists a new Player join instead of rejecting it");
}
```

**Companion tests to add in the same file (same shape, vary seeded capacity/role):**
- `JoinFinalizedQuest_Post_WhenQuestHasSpaceAndRoleIsPlayer_CreatesSeatedSignup` — regression guard, `IsSelected.Should().BeTrue()` when under capacity (D-03's "unchanged behavior" half).
- `JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsAssistantDM_CreatesSeatedSignup` / `...Spectator...` — D-05 regression guard, always `IsSelected.Should().BeTrue()` regardless of Player-slot fullness.

---

### `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` (extend, test, unit, CRUD/ordering)

**Analog:** same file — `GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime` (lines 270-298)

**Imports pattern** (file-level, lines 1-7 — already present, no new imports needed):
```csharp
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
```

**Helpers already available, reuse unchanged:** `CreateContext(databaseName)` (lines 13-20), `CreateMapper()` (lines 31-35), `SeedQuestAndUserAsync(context, questId, playerId, groupId)` (lines 39-64), `MakeSignupEntity(id, questId, isSelected, role, signupTime)` (lines 66-78).

**Core pattern to copy and adapt (lines 270-298) — the new test should model a `JoinFinalizedQuest`-created signup (`LastVoteChangeTime == null`, fresh `SignupTime = DateTime.UtcNow`) competing fairly against a pre-existing waitlisted signup:**
```csharp
[Fact]
public async Task GetTopWaitlistedCandidateAsync_NewJoinerFromJoinFinalizedQuest_OrdersCorrectlyAmongExistingWaitlist()
{
    // Arrange: a pre-existing waitlisted Yes-voter (signed up 2 days ago, never changed vote)
    // vs. a brand-new JoinFinalizedQuest-created joiner (signed up just now, also Yes, never changed vote)
    await using var context = CreateContext(nameof(GetTopWaitlistedCandidateAsync_NewJoinerFromJoinFinalizedQuest_OrdersCorrectlyAmongExistingWaitlist));
    await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
    await SeedQuestAndUserAsync(context, questId: 1, playerId: 102);

    var existingWaitlisted = MakeSignupEntity(1, questId: 1, isSelected: false, signupTime: DateTime.UtcNow.AddDays(-2));
    existingWaitlisted.LastVoteChangeTime = null;
    existingWaitlisted.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 1, Vote = (int)VoteType.Yes });

    // Shape of a signup JoinFinalizedQuest creates when waitlisted: IsSelected = false,
    // LastVoteChangeTime never set, SignupTime = entity default (DateTime.UtcNow at creation)
    var newJoiner = MakeSignupEntity(2, questId: 1, isSelected: false, signupTime: DateTime.UtcNow);
    newJoiner.LastVoteChangeTime = null;
    newJoiner.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 2, Vote = (int)VoteType.Yes });

    context.PlayerSignups.AddRange(existingWaitlisted, newJoiner);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var repository = new PlayerSignupRepository(context, CreateMapper());

    // Act
    var candidate = await repository.GetTopWaitlistedCandidateAsync(1, finalizedProposedDateId: 5, TestContext.Current.CancellationToken);

    // Assert: existingWaitlisted (earlier SignupTime) wins the same-vote tiebreak — the new
    // joiner participates correctly in the existing ordering, no special-casing needed
    candidate.Should().NotBeNull();
    candidate!.Id.Should().Be(1);
}
```

## Shared Patterns

### Server-side recompute-then-decide (never trust client state)
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` lines 440-444 (existing, `JoinFinalizedQuest`)
**Apply to:** The D-03 controller edit only — preserve this shape exactly, just change the outcome of the decision from reject to `IsSelected = false`.
```csharp
var selectedPlayersCount = quest.PlayerSignups
    .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
    .Count();
```

### Antiforgery via `asp-action` tag helper (no explicit token call)
**Source:** `QuestBoard.Service/Views/Quest/Details.cshtml` lines 337, 348, 359 (existing desktop forms)
**Apply to:** All 3 new mobile join forms — do not add `@Html.AntiForgeryToken()`, the tag helper already injects it. Confirmed safe against `AntiForgeryTokenCoverageTests.cs` reflection checks (Pitfall 2) since no new controller action is introduced.

### Mobile section-card container convention
**Source:** `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` lines 105-108, 146-149 (existing "Update Your Vote"/"Choose a Date" cards)
**Apply to:** The new "Join This Quest" card container — use `quest-section-card-mobile` + `quest-section-heading` + icon, never desktop's `card modern-card` (Pitfall 3).
```html
<div class="quest-section-card-mobile mb-3">
    <h6 class="quest-section-heading mb-3">
        <i class="fas fa-user-plus me-1"></i>Join This Quest
    </h6>
```

### Integration test User-Agent switching harness
**Source:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` lines 16-56 (`MobileUserAgent`/`DesktopUserAgent` constants, `GetWithUserAgentAsync` overloads)
**Apply to:** All new `[Fact]`s asserting the mobile join card's presence/absence — reuse the class's existing constants and helper methods, do not redeclare them.

### Test data seeding via `TestDataHelper`
**Source:** `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestQuestAsync` (lines 8-44), `CreatePlayerSignupAsync` (lines 46-69), `CreateProposedDateAsync` (lines 71-89)
**Apply to:** All new integration tests (both `MobileViewsTests.cs` additions and the new `QuestJoinFinalizedQuestTests.cs`) — `TotalPlayerCount` defaults to `4` (line 33), so filling to capacity means 4 `isSelected: true` Player signups.

## No Analog Found

None — every file in scope has at least a role-match analog in the existing codebase; no new architectural pattern is being introduced by this phase (RESEARCH.md confirms zero new packages, zero new components, zero new controller actions).

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/Quest/`, `QuestBoard.Service/Controllers/QuestBoard/`, `QuestBoard.IntegrationTests/Mobile/`, `QuestBoard.IntegrationTests/Controllers/`, `QuestBoard.IntegrationTests/Helpers/`, `QuestBoard.UnitTests/Repository/`
**Files scanned:** `Details.cshtml`, `Details.Mobile.cshtml`, `QuestController.cs` (JoinFinalizedQuest + Signup actions), `MobileViewsTests.cs`, `QuestFinalizeTests.cs`, `QuestControllerIntegrationTests_Comprehensive.cs`, `TestDataHelper.cs`, `PlayerSignupRepositoryTests.cs`
**Pattern extraction date:** 2026-07-06
