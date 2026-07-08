# Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 14
**Analogs found:** 12 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Repository/Entities/PlayerSignupEntity.cs` (+`LastVoteChangeTime`) | model (entity) | CRUD | same file, existing `SignupTime`/`IsSelected` props | exact (modify in place) |
| `QuestBoard.Domain/Models/QuestBoard/PlayerSignup.cs` (+`LastVoteChangeTime`) | model (domain) | CRUD | same file | exact (modify in place) |
| `QuestBoard.Repository/Migrations/{ts}_AddLastVoteChangeTimeToPlayerSignup.cs` | migration | batch | `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs` | exact |
| `QuestBoard.Repository/PlayerSignupRepository.cs` — rewrite `ChangeVoteToYesAndSelectAsync` → `ChangeVoteAsync`; add `GetTopWaitlistedCandidateAsync` | repository | CRUD | same file (existing `ChangeVoteToYesAndSelectAsync`, `UpdateAsync`) | exact (modify in place) |
| `QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs` — new method signatures | model (interface) | CRUD | same file, existing `ChangeVoteToYesAndSelectAsync` signature | exact (modify in place) |
| `QuestBoard.Domain/Interfaces/IPlayerSignupService.cs` — new method signatures | model (interface) | CRUD | same file | exact (modify in place) |
| `QuestBoard.Domain/Services/PlayerSignupService.cs` — generalized vote-change method | service | CRUD | same file (thin pass-through pattern already used for `UpdatePlayerDateVotesAsync`, `UpdateSignupCharacterAsync`) | exact (modify in place) |
| `QuestBoard.Domain/Services/QuestService.cs` — + `ChangeVoteAsync`/promotion orchestration + email dispatch call | service | event-driven | same file, `FinalizeQuestAsync` (lines 17-46) | exact (modify in place) |
| `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` — + `EnqueueWaitlistPromotedEmail` | model (interface) | event-driven | same file, `EnqueueFinalizedEmail`/`EnqueueDateChangedEmail` | exact (modify in place) |
| `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` — + implementation | service | event-driven | same file, `EnqueueFinalizedEmail` (lines 14-29) | exact (modify in place) |
| `QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs` | service (background job) | event-driven | `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` | exact |
| `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` | component | transform | `QuestBoard.Service/Components/Emails/QuestFinalized.razor` | exact |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — new `ChangeVote` action (replaces `ChangeVoteToYes`); wire `RevokeSignup` to promotion | controller | request-response | same file, `ChangeVoteToYes` (553-600) and `RevokeSignup` (605-627) | exact (modify in place) |
| `QuestBoard.Service/Views/Quest/Details.cshtml` — waitlist section rewritten (3-button UI, new ordering) | component (Razor view) | request-response | same file, existing waitlist table (189-303) + `revokeSignup()`/`changeVoteToYes()` JS (849-891) | exact (modify in place) |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` — NEW waitlist section + vote buttons | component (Razor view) | request-response | `Details.cshtml`'s waitlist section (cross-file analog, since mobile has none today); local analog for revoke button/JS at `Details.Mobile.cshtml:214-239` | role-match (new section, no direct mobile precedent) |
| `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` (new) | test | CRUD | `QuestBoard.UnitTests/Services/QuestServiceTests.cs` (structure/style) | role-match |
| `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — extend | test | CRUD | same file, `FinalizeQuestAsync_*` tests (55-80+) | exact (modify in place) |
| `QuestBoard.UnitTests/.../WaitlistOrderingTests.cs` (new) | test | transform | `QuestServiceTests.cs` (style only — no direct ordering-test precedent) | no analog for logic, style analog only |

## Pattern Assignments

### `QuestBoard.Repository/PlayerSignupRepository.cs` (repository, CRUD)

**Analog:** same file, `ChangeVoteToYesAndSelectAsync` (lines 21-50)

**Imports pattern** (lines 1-7):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;
```

**Core pattern to replace/generalize** (lines 21-50):
```csharp
public async Task ChangeVoteToYesAndSelectAsync(int playerSignupId, int proposedDateId, CancellationToken cancellationToken = default)
{
    var entity = await DbSet
        .Include(ps => ps.DateVotes)
        .FirstOrDefaultAsync(ps => ps.Id == playerSignupId, cancellationToken);

    if (entity == null)
    {
        throw new ArgumentException("Player signup not found", nameof(playerSignupId));
    }

    var existingVote = entity.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == proposedDateId);

    if (existingVote != null)
    {
        existingVote.Vote = 0; // VoteType.Yes = 0   <-- BUG: actual enum has Yes = 2, this stores No
    }
    else
    {
        entity.DateVotes.Add(new PlayerDateVoteEntity
        {
            ProposedDateId = proposedDateId,
            PlayerSignupId = playerSignupId,
            Vote = 0 // VoteType.Yes = 0   <-- same bug
        });
    }

    entity.IsSelected = true;
    await DbContext.SaveChangesAsync(cancellationToken);
}
```
**Required fix when rewriting into `ChangeVoteAsync`:** replace bare `0` literals with `(int)VoteType.Yes` (or the passed-in `vote` parameter cast), and set `entity.LastVoteChangeTime = DateTime.UtcNow;` on every mutation (VOTE-03). Do not hard-reject on capacity here — `IsSelected` is set conditionally by the caller/service based on a freshly recomputed seat count (VOTE-01), never throw/`BadRequest` from this layer.

**Error handling pattern** (lines 27-30): `ArgumentException` thrown when entity not found — keep this convention for the new method.

**New query needed — `GetTopWaitlistedCandidateAsync`:** no direct existing precedent; model it as an `IQueryable` filter + `OrderByDescending`/`ThenBy` similar in shape to `GetByIdWithDateVotesAsync` (lines 12-18) for the `Include(ps => ps.DateVotes)` + `FirstOrDefaultAsync` idiom, but selecting the top waitlisted (`!IsSelected && Role == SignupRole.Player`) row ordered by vote priority then `LastVoteChangeTime ?? SignupTime`.

---

### `QuestBoard.Domain/Services/QuestService.cs` (service, event-driven)

**Analog:** same file, `FinalizeQuestAsync` (lines 17-46)

**Imports pattern** (lines 1-6):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
```

**Core orchestration + email dispatch pattern** (lines 17-46):
```csharp
public async Task FinalizeQuestAsync(int questId, DateTime finalizedDate, IList<int> selectedPlayerSignupIds, CancellationToken token = default)
{
    await repository.FinalizeQuestAsync(questId, finalizedDate, selectedPlayerSignupIds, token);

    // Re-fetch post-save to avoid stale IsSelected state
    var quest = await repository.GetQuestWithDetailsAsync(questId, token);
    if (quest == null) return;

    var selectedSignups = quest.PlayerSignups
        .Where(ps => (selectedPlayerSignupIds.Contains(ps.Id) || ps.Role == SignupRole.Spectator)
                     && !string.IsNullOrEmpty(ps.Player.Email)
                     && ps.Player.EmailConfirmed)
        .ToList();

    if (selectedSignups.Count == 0) return;

    var recipientEmails = selectedSignups.Select(s => s.Player.Email!).ToArray();
    var playerNames     = selectedSignups.Select(s => s.Player.Name).ToArray();

    dispatcher.EnqueueFinalizedEmail(
        quest.Id,
        quest.GroupId,    // group context for Hangfire job filter
        finalizedDate,
        recipientEmails,
        playerNames,
        quest.Title,
        quest.DungeonMaster?.Name ?? "Unknown DM",
        quest.Description,
        quest.ChallengeRating);
}
```

**Apply this shape to the new `ChangeVoteAsync`/promotion orchestration:**
1. Call `playerSignupRepository.ChangeVoteAsync(...)` (returns whether a seat just freed — see Repository pattern above).
2. If a seat freed, re-fetch fresh state (mirrors "re-fetch post-save to avoid stale `IsSelected` state" comment above) and call `playerSignupRepository.GetTopWaitlistedCandidateAsync(questId)`.
3. If a candidate exists, flip `IsSelected = true` and persist, then call `dispatcher.EnqueueWaitlistPromotedEmail(...)` with a **singular** recipient (not an array) — mirrors the `recipientEmails`/`playerNames` array pattern above but deliberately narrowed to one player per VOTE-07.
4. Same guard style used in `RemoveAsync` (lines 109-124) for null/empty checks before touching related entities — reuse for `RevokeSignup` promotion wiring (`wasSelected` must gate the promotion check, per Pitfall 4 in RESEARCH.md).

---

### `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` + `HangfireQuestEmailDispatcher.cs` (event-driven, shared)

**Analog:** `EnqueueFinalizedEmail` (interface lines 12-21; implementation lines 14-29)

**Interface pattern:**
```csharp
void EnqueueFinalizedEmail(
    int questId,
    int groupId,
    DateTime finalizedDate,
    string[] recipientEmails,
    string[] playerNames,
    string questTitle,
    string dmName,
    string questDescription,
    int challengeRating);
```
New signature (singular, not array — structurally prevents accidental broadcast per VOTE-07):
```csharp
void EnqueueWaitlistPromotedEmail(
    int questId,
    int groupId,
    DateTime finalizedDate,
    string recipientEmail,
    string playerName,
    string questTitle,
    string dmName,
    string questDescription,
    int challengeRating);
```

**Implementation pattern** (`HangfireQuestEmailDispatcher.cs` lines 14-29):
```csharp
public void EnqueueFinalizedEmail(
    int questId, int groupId, DateTime finalizedDate,
    string[] recipientEmails, string[] playerNames,
    string questTitle, string dmName, string questDescription, int challengeRating)
{
    jobClient.Enqueue<QuestFinalizedEmailJob>(j => j.ExecuteAsync(
        questId, groupId, finalizedDate, recipientEmails, playerNames,
        questTitle, dmName, questDescription, challengeRating,
        CancellationToken.None));
}
```
Mirror exactly for `EnqueueWaitlistPromotedEmail` → `jobClient.Enqueue<QuestWaitlistPromotedEmailJob>(...)`.

---

### `QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs` (background job, event-driven)

**Analog:** `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` (entire file, 68 lines)

```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class QuestFinalizedEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestFinalizedEmailJob> logger)
{
    public async Task ExecuteAsync(
        int questId, int groupId, DateTime finalizedDate,
        string[] recipientEmails, string[] playerNames,
        string questTitle, string dmName, string questDescription, int challengeRating,
        CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp =>
        {
            var questRepository = sp.GetRequiredService<IQuestRepository>();
            var renderService   = sp.GetRequiredService<IEmailRenderService>();
            var emailService    = sp.GetRequiredService<IEmailService>();
            var emailSettings   = sp.GetRequiredService<IOptions<EmailSettings>>().Value;

            // Dedup guard: use .Date comparison — "same session date" intent, not same millisecond
            var quest = await questRepository.GetQuestWithDetailsAsync(questId, cancellationToken);
            if (quest?.FinalizedEmailSentForDate?.Date == finalizedDate.Date)
            {
                logger.LogInformation("Finalized email already sent for quest {QuestId} on {Date}. Skipping.", questId, finalizedDate);
                return;
            }

            var questUrl = $"{emailSettings.AppUrl}/Quest/Details/{questId}";

            for (var i = 0; i < recipientEmails.Length; i++)
            {
                var html = await renderService.RenderAsync<QuestFinalized>(new Dictionary<string, object?>
                {
                    { nameof(QuestFinalized.QuestTitle),           questTitle },
                    { nameof(QuestFinalized.DmName),               dmName },
                    { nameof(QuestFinalized.QuestDate),            finalizedDate },
                    { nameof(QuestFinalized.QuestDescription),     questDescription },
                    { nameof(QuestFinalized.ConfirmedPlayerNames), playerNames.ToList() },
                    { nameof(QuestFinalized.QuestUrl),             questUrl },
                    { nameof(QuestFinalized.ChallengeRating),      challengeRating },
                    { nameof(QuestFinalized.AppUrl),               emailSettings.AppUrl }
                });

                await emailService.SendAsync(recipientEmails[i], $"Your quest has been confirmed: {questTitle}", html);
            }

            await questRepository.SetFinalizedEmailSentForDateAsync(questId, finalizedDate, cancellationToken);
        });
    }
}
```
**For `QuestWaitlistPromotedEmailJob`:** drop the `for` loop entirely (singular recipient only — structural guard for VOTE-07), render `WaitlistPromoted` instead of `QuestFinalized`, and skip (or design a new) dedup-guard field — no existing "waitlist promotion already sent" tracking field exists on `Quest`; simplest approach is no dedup guard at all since promotion is a one-time state transition per player (each promotion is a distinct event, unlike the finalize email which can be re-triggered by date edits).

---

### `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` (component, transform)

**Analog:** `QuestBoard.Service/Components/Emails/QuestFinalized.razor` (entire file, 104 lines)

**Structure to copy:** `_EmailLayout` wrapper with `Subject`/`PreviewText`, same D&D-parchment visual styling (Poster background image, CR badge, Cinzel serif title, gold divider, wax-seal + CTA button row), same `@code` parameter block style:
```csharp
@code {
    [Parameter, EditorRequired] public string QuestTitle { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string DmName { get; set; } = string.Empty;
    [Parameter, EditorRequired] public DateTime QuestDate { get; set; }
    [Parameter, EditorRequired] public string QuestDescription { get; set; } = string.Empty;
    [Parameter, EditorRequired] public IList<string> ConfirmedPlayerNames { get; set; } = [];
    [Parameter, EditorRequired] public string QuestUrl { get; set; } = string.Empty;
    [Parameter, EditorRequired] public int ChallengeRating { get; set; }
    [Parameter, EditorRequired] public string AppUrl { get; set; } = string.Empty;
}
```
**For `WaitlistPromoted`:** same parameter shape works almost as-is (single `PlayerName` string instead of `ConfirmedPlayerNames` list, since only one recipient exists); change copy from "Quest Confirmed!" / "Your adventure awaits, brave adventurer." to a promoted-specific message (e.g. "A Seat Opened Up!" / "You've been promoted off the waitlist — get ready to adventure."). Reuse the CR badge, gold divider, metadata table, and wax-seal+CTA button rows verbatim (same visual system, per D-06).

---

### `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (controller, request-response)

**Analog:** same file, `ChangeVoteToYes` (lines 553-600) and `RevokeSignup` (lines 605-627)

**Imports/attributes pattern:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize]
public async Task<IActionResult> ChangeVoteToYes(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || !quest.IsFinalized || quest.FinalizedDate == null)
        return BadRequest("Quest not found or not finalized.");

    var user = await userService.GetUserAsync(User);
    if (user == null)
        return Challenge();

    var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
    if (playerSignup == null)
    {
        return BadRequest("You are not signed up for this quest.");
    }

    if (playerSignup.IsSelected)
    {
        return BadRequest("You are already selected for this quest.");
    }

    var selectedPlayersCount = quest.PlayerSignups
        .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
        .Count();

    if (selectedPlayersCount >= quest.TotalPlayerCount)
    {
        return BadRequest("No available spots in this quest.");   // <-- VOTE-01 removes this hard rejection
    }

    var finalizedProposedDate = quest.ProposedDates
        .FirstOrDefault(pd => pd.Date.Date == quest.FinalizedDate.Value.Date);

    if (finalizedProposedDate == null)
    {
        return BadRequest("Could not find the finalized date information.");
    }

    await playerSignupService.ChangeVoteToYesAndSelectAsync(playerSignup.Id, finalizedProposedDate.Id);

    return Ok();
}
```
**New `ChangeVote(int id, VoteType vote)` action:** keep the same authenticate → find-own-signup → find-finalized-proposed-date shape, but remove the "already selected" and "no available spots" hard-rejects (VOTE-01) and delegate the seat-check/promotion decision to `questService.ChangeVoteAsync(...)`. **Security requirement (V4/V5 from RESEARCH.md):** validate `vote` with `Enum.IsDefined(typeof(VoteType), vote)` before use, and always resolve the target signup via `quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id)` — never accept a raw `playerSignupId` from the client (IDOR guard, exactly as this existing action already does).

**`RevokeSignup` pattern to extend** (lines 605-627):
```csharp
[HttpDelete]
[ValidateAntiForgeryToken]
[Authorize]
public async Task<IActionResult> RevokeSignup(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
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

    await playerSignupService.RemoveAsync(playerSignup);

    return Ok();
}
```
**Change:** after `RemoveAsync`, if `playerSignup.IsSelected` was `true` before removal, call into the same `questService` promotion-check method used by `ChangeVote` (see QuestService pattern above) — do not duplicate the "find candidate + email" logic in the controller.

---

### `QuestBoard.Service/Views/Quest/Details.cshtml` (Razor view, request-response)

**Analog:** same file — existing waitlist table (lines 189-303) and JS fetch functions (lines 848-891)

**Waitlist ordering to replace** (line 67):
```csharp
var waitlistPlayers = Model.Quest?.PlayerSignups.Where(ps => !ps.IsSelected && ps.Role == SignupRole.Player).OrderBy(ps => ps.SignupTime).ToList() ?? [];
```
Replace with a call into the new centralized ordering method (e.g. `waitlistPlayers = Model.WaitlistPlayers` if moved to ViewModel, or a shared extension method call) — do not keep inline `OrderBy(SignupTime)` only, per VOTE-02/VOTE-03.

**Existing per-row action cell to replace** (lines 282-297) — currently a single conditional "Join Quest" button:
```csharp
<td>
    @if (canChangeVote)
    {
        <button type="button" class="btn btn-sm btn-success"
                onclick="changeVoteToYes(@Model.Quest?.Id)"
                title="Change vote to Yes and join quest">
            <i class="fas fa-check me-1"></i>Join Quest
        </button>
    }
    else if (isCurrentUser && !hasAvailableSpots)
    {
        <span class="text-muted small">
            <i class="fas fa-info-circle me-1"></i>Quest Full
        </span>
    }
</td>
```
Replace with the three Vote Yes/Maybe/No buttons (D-01), scoped to `isCurrentUser` rows only, in the same cell/row pattern.

**Revoke button row to extend** (lines 598-612) — D-02 requires Vote Yes/Maybe/No pushed right, Revoke stays left, same row:
```csharp
@if (boardType != BoardType.Campaign && User.Identity?.IsAuthenticated == true && (bool)ViewBag.IsPlayerSignedUp)
{
    <div class="d-flex gap-2">
        @if (Model.Quest?.IsFinalized != true)
        {
            <button type="button" class="btn btn-primary" onclick="showUpdateForm()">
                <i class="fas fa-edit me-2"></i>Update Signup
            </button>
        }
        <button type="button" class="btn btn-danger" onclick="revokeSignup(@ViewContext.RouteData.Values["id"])">
            <i class="fas fa-times me-2"></i>Revoke My Signup
        </button>
    </div>
}
```
Change `d-flex gap-2` to `d-flex justify-content-between` (per CLAUDE.md UI guidance: "secondary left, primary right") and add the three vote buttons in a nested `d-flex gap-2` on the right when `Model.Quest?.IsFinalized == true`.

**JS fetch pattern to copy exactly** (lines 871-891):
```javascript
function changeVoteToYes(questId) {
    if (confirm("Change your vote to Yes and join this quest?")) {
        const formData = new FormData();
        formData.append('__RequestVerificationToken', '@tokens.RequestToken');

        fetch(`/Quest/ChangeVoteToYes/${questId}`, {
            method: "POST",
            body: formData
        }).then(res => {
            if (res.ok) {
                location.reload();
            } else {
                res.text().then(text => {
                    alert(`Failed to change vote: ${text}`);
                });
            }
        }).catch(err => {
            alert("An error occurred while changing vote.");
        });
    }
}
```
New `changeVote(questId, vote)` function: same shape, append `formData.append('vote', vote)`, POST to `/Quest/ChangeVote/${questId}`. Reuse for all three buttons, passing `0`/`1`/`2` for No/Maybe/Yes.

---

### `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (Razor view, request-response — NEW section)

**Analog (cross-file):** desktop `Details.cshtml` waitlist table (189-303) for data/columns; local analog for revoke button/JS placement and mobile card idiom.

**Existing mobile revoke button + JS to extend** (lines 214-239):
```csharp
@* Revoke signup *@
@if (User.Identity?.IsAuthenticated == true && isPlayerSignedUp)
{
    <button type="button" class="btn btn-danger w-100 mt-2"
            onclick="revokeSignup(@Model.Quest?.Id)">
        <i class="fas fa-times me-2"></i>Revoke Signup
    </button>
}
```
```javascript
function revokeSignup(questId) {
    if (confirm("Are you sure you want to revoke your signup for this quest? This action cannot be undone.")) {
        const formData = new FormData();
        formData.append('__RequestVerificationToken', '@tokens.RequestToken');
        fetch(`/Quest/RevokeSignup/${questId}`, {
            method: "DELETE",
            body: formData
        }).then(res => {
            if (res.ok) { location.reload(); }
            else { res.text().then(text => { alert(`Failed to revoke signup: ${text}`); }); }
        }).catch(err => { alert("An error occurred while revoking signup."); });
    }
}
```
**Existing "participant list" card pattern to mirror for the new waitlist section** (lines 174-203):
```csharp
@if (Model.Quest?.IsFinalized == true && allSelectedParticipants.Count > 0)
{
    <div class="participant-list-mobile mb-3">
        <h6 class="text-muted text-uppercase mb-2">
            <i class="fas fa-users me-1"></i>Adventurers
        </h6>
        @foreach (var participant in allSelectedParticipants)
        {
            var isCurrentUser = participant.Player.Id == currentUserId;
            ...
            <div class="participant-row d-flex justify-content-between align-items-center py-2 border-bottom @(isCurrentUser ? "bg-dark rounded px-2" : "")">
                ...
            </div>
        }
    </div>
}
```
**Build the new mobile waitlist section on this exact card/row idiom** (`participant-list-mobile`-style container, `participant-row d-flex justify-content-between` rows) rather than importing desktop's `<table>` markup — mobile views in this codebase consistently use stacked-card rows, not tables (see `Manage.Mobile.cshtml` precedent per RESEARCH.md). Add the Vote Yes/Maybe/No buttons + Revoke button in a `d-flex justify-content-between` row per-item, matching D-02's layout instruction and this file's existing button styling (`btn-danger w-100`, `fas` icons with `me-2`/`me-1`).

**Data source:** both `waitlistPlayers` (desktop, line 67) and this new mobile section must consume the exact same server-side ordering method — do not write a second inline `OrderBy(SignupTime)` here (Pitfall 5 in RESEARCH.md).

---

### `QuestBoard.UnitTests/Services/QuestServiceTests.cs` (test, CRUD — extend)

**Analog:** same file, `FinalizeQuestAsync_*` tests (lines 55-80+)

**Imports pattern** (lines 1-9):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;
```

**Setup + helper pattern** (lines 11-49):
```csharp
public class QuestServiceTests
{
    private readonly IQuestRepository _repository;
    private readonly IPlayerSignupRepository _playerSignupRepository;
    private readonly IQuestEmailDispatcher _dispatcher;
    private readonly IMapper _mapper;
    private readonly QuestService _sut;

    public QuestServiceTests()
    {
        _repository = Substitute.For<IQuestRepository>();
        _playerSignupRepository = Substitute.For<IPlayerSignupRepository>();
        _dispatcher = Substitute.For<IQuestEmailDispatcher>();
        _mapper = Substitute.For<IMapper>();

        _sut = new QuestService(_repository, _playerSignupRepository, _dispatcher, _mapper);
    }

    private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) => new()
    {
        Id = id,
        Title = "Test Quest",
        Description = "A quest",
        DungeonMaster = new User { Id = 1, Name = "DM Dave", Email = "dm@example.com" },
        PlayerSignups = signups ?? [],
        ProposedDates = []
    };

    private static PlayerSignup MakeSignup(int id, string email, SignupRole role = SignupRole.Player, bool isSelected = true, bool emailConfirmed = true) => new()
    {
        Id = id,
        Role = role,
        IsSelected = isSelected,
        Player = new User { Id = id + 10, Name = $"Player {id}", Email = email, EmailConfirmed = emailConfirmed },
        Quest = new Quest { Id = 1, Title = "T", Description = "D" }
    };
}
```

**Test-assertion pattern for dispatcher calls** (lines 55-71):
```csharp
[Fact]
public async Task FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails()
{
    _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);
    _repository.GetQuestWithDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
        .Returns((Quest?)null);

    await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, [42], TestContext.Current.CancellationToken);

    _dispatcher.DidNotReceive().EnqueueFinalizedEmail(
        Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
}
```
**Use this exact style for VOTE-04/VOTE-07 tests:** substitute `_playerSignupRepository`/`_dispatcher`, assert `Received()`/`DidNotReceive()` on `EnqueueWaitlistPromotedEmail` with singular-recipient args, and add a dedicated test proving the promoted player is the ONLY recipient (never the freeing/voting player) — this is the closest existing precedent for asserting "who gets emailed."

## Shared Patterns

### Authentication / Ownership Resolution (IDOR guard)
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — repeated in `ChangeVoteToYes` (565), `RevokeSignup` (617), `UpdateSignup` (501), `UpdateSignupCharacter` (528)
**Apply to:** the new `ChangeVote` controller action
```csharp
var user = await userService.GetUserAsync(User);
if (user == null)
    return Challenge();

var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
if (playerSignup == null)
{
    return BadRequest("You are not signed up for this quest.");
}
```
Never accept a raw `playerSignupId` from the client as the mutation target — always resolve via the authenticated user's own signup row.

### CSRF Protection
**Source:** every mutating action in `QuestController.cs` (e.g. `[ValidateAntiForgeryToken]` on lines 407-409, 486-488, 550-552, 602-604)
**Apply to:** new `ChangeVote` action — must carry `[HttpPost]`, `[ValidateAntiForgeryToken]`, `[Authorize]` exactly like `ChangeVoteToYes` (lines 550-552).

### Fetch/AJAX JS Pattern
**Source:** `Details.cshtml:849-891`, mirrored in `Details.Mobile.cshtml:227-239`
**Apply to:** all three new vote buttons, both desktop and mobile — `FormData` + `__RequestVerificationToken`, `location.reload()` on success, `alert(text)` on failure, `.catch()` generic error alert. See full excerpts above.

### Email Dispatch Pipeline
**Source:** `QuestService.FinalizeQuestAsync` → `IQuestEmailDispatcher.EnqueueFinalizedEmail` → `HangfireQuestEmailDispatcher` → `QuestFinalizedEmailJob` → `IEmailRenderService`/`HtmlRenderer` → `IEmailService`
**Apply to:** the new promotion email path — same five-link chain, new names (`EnqueueWaitlistPromotedEmail` → `QuestWaitlistPromotedEmailJob` → `WaitlistPromoted.razor`), singular recipient instead of array.

### Migration Style (new nullable column)
**Source:** `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs`
**Apply to:** `AddLastVoteChangeTimeToPlayerSignup` migration
```csharp
migrationBuilder.AddColumn<DateTime>(
    name: "LastVoteChangeTime",
    table: "PlayerSignups",
    type: "datetime2",
    nullable: true);
```
No `defaultValue`/backfill needed — `null` naturally means "never changed vote since signup" and the ordering query's `?? SignupTime` fallback handles it.

### Enum-Cast Regression Test Pattern
**Source:** `QuestBoard.UnitTests/.../EntityProfileEnumCastTests.cs` (referenced in RESEARCH.md; not read in full here but named explicitly as the pattern to extend)
**Apply to:** a new assertion that `(int)VoteType.Yes == 2` and/or a repository-level test exercising `ChangeVoteAsync(..., VoteType.Yes)` and asserting the persisted `Vote` int equals `2`, not `0` — directly guards against reintroducing the bug this phase fixes.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `QuestBoard.Repository/.../GetTopWaitlistedCandidateAsync` query logic | repository (query) | transform | No existing "priority + fallback timestamp" ordering query exists anywhere in the codebase — closest conceptual analog is `Manage.cshtml`'s inline `yesVotes`/`maybeVotes`/`noVotes` grouping (not a repository method), per RESEARCH.md Pattern 2. Use RESEARCH.md's `OrderWaitlist` extension-method sketch (Architecture Patterns § Pattern 2) as the starting point instead of a codebase analog. |
| `QuestBoard.UnitTests/.../WaitlistOrderingTests.cs` | test | transform | No existing ordering-specific test class exists; follow `QuestServiceTests.cs`'s general xUnit+FluentAssertions style (Arrange/Act/Assert, `[Fact]` per scenario) but the assertions themselves are net-new (no dispatcher/repository substitution needed — likely pure-function tests against the ordering method/extension). |

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Controllers`, `QuestBoard.Service/Services`, `QuestBoard.Service/Jobs`, `QuestBoard.Service/Components/Emails`, `QuestBoard.Service/Views/Quest`, `QuestBoard.UnitTests/Services`
**Files scanned:** ~18 (read in full or targeted sections)
**Pattern extraction date:** 2026-07-04
