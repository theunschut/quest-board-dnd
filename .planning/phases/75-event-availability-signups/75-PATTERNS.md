# Phase 75: Event Availability Signups - Pattern Map

**Mapped:** 2026-08-27
**Files analyzed:** 15
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Domain/Models/EventSignup.cs` | model | CRUD | `QuestBoard.Domain/Models/Event.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` | repository interface | CRUD (narrow scalar-update) | `QuestBoard.Repository/PlayerSignupRepository.cs` (`ChangeVoteAsync`) | exact |
| `QuestBoard.Repository/EventSignupRepository.cs` | repository | CRUD (narrow scalar-update) | `QuestBoard.Repository/PlayerSignupRepository.cs` | exact |
| `QuestBoard.Domain/Interfaces/IEventSignupService.cs` / `QuestBoard.Domain/Services/EventSignupService.cs` | service | CRUD | `IEventService`/`EventService` (Phase 74) | exact |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (extended) | config (DI) | — | existing `AddScoped<IEventRepository, EventRepository>()` line | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (extended) | config (DI) | — | existing `AddScoped<IEventService, EventService>()` line | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (extended) | config (AutoMapper) | transform | `Event`/`EventEntity` map block | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (extended) | config (AutoMapper) | transform | `Event`/`EventViewModel` map block | exact |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` (extended — `SetAvailability`, `Withdraw`, fan-out in `Create`) | controller | request-response | `QuestController.ChangeVote` / `RevokeSignup` (write actions), `QuestController.Close` (board-type guard) | exact |
| `QuestBoard.Service/Views/Events/Details.cshtml` (extended) | view | request-response | `Views/Quest/Details.cshtml` (vote buttons, roster, revoke script) | exact |
| `QuestBoard.Domain/Services/GroupService.cs` (`AddMemberAsync`/`RemoveMemberAsync` extended) | service | event-driven (membership hook) | itself (existing methods) — thin pass-through, no logic change needed at this layer | exact |
| `QuestBoard.Repository/GroupRepository.cs` (`AddMemberAsync`/`RemoveMemberAsync` extended) | repository | CRUD + batch (atomic multi-entity write) | `QuestBoard.Repository/CharacterRepository.cs` (`UpdateWithProfileImageAsync`) for atomicity shape; itself for the existing race-handling to preserve | exact |
| `QuestBoard.Domain/Interfaces/IEventRepository.cs` / `EventRepository.cs` (extended — explicit-groupId future-event query) | repository | CRUD | `QuestBoard.Repository/QuestRepository.cs` (`GetQuestsForTomorrowAllGroupsAsync`) | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (`RemoveMember` confirmation) + Members view | controller/view | request-response | itself (`AddMember`/`RemoveMember` actions) | exact |
| `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` | test | request-response | `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` | exact |
| `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` | test | CRUD | `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` | exact |

## Pattern Assignments

### `QuestBoard.Domain/Models/EventSignup.cs` (model, CRUD)

**Analog:** `QuestBoard.Domain/Models/Event.cs` (full file, 25 lines)

```csharp
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class Event : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public int? SeriesId { get; set; }
    public int? SeriesSlotIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int GroupId { get; set; }
}
```

Follow this shape exactly: plain `IModel`-implementing POCO, no navigation collections (D-30's warning about `EventEntity.Signups` applies symmetrically — do **not** add a `Signups` collection to the `Event` domain model). `EventSignup` needs `Id`, `EventId`, `UserId`, `Availability` (as `VoteType`, not `int` — the entity stores `int`, the domain model should expose the enum, mirroring how `PlayerSignupEntity.SignupRole` (`int`) maps to `PlayerSignup.Role` (`SignupRole`) — see the `PlayerSignupEntity`/`PlayerSignup` AutoMapper excerpt below), `CreatedAt`, `UpdatedAt`, and a computed `HasAnswered` property per D-11 (`public bool HasAnswered => UpdatedAt != null;`).

**Entity source it maps from** (`QuestBoard.Repository/Entities/EventSignupEntity.cs`, full file):
```csharp
[Table("EventSignups")]
public class EventSignupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual EventEntity Event { get; set; } = null!;

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    // Stores the same three availability values used for quest date votes, where 0 is No,
    // 1 is Maybe and 2 is Yes.
    [Range(0, 2)]
    public int Availability { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // A null value means the answer has never been changed since it was created.
    public DateTime? UpdatedAt { get; set; }
}
```
**D-12 requires this comment rewritten** — replace `"A null value means the answer has never been changed since it was created."` with wording that says: null = no human has ever set this answer (auto-signup fan-out never stamps it).

---

### `QuestBoard.Repository/EventSignupRepository.cs` + `IEventSignupRepository.cs` (repository, CRUD/narrow scalar-update)

**Analog:** `QuestBoard.Repository/PlayerSignupRepository.cs` (full file, verbatim below — this is D-30's mandated template)

```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class PlayerSignupRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<PlayerSignup, PlayerSignupEntity>(dbContext, mapper), IPlayerSignupRepository
{
    /// <inheritdoc/>
    public async Task<bool> ChangeVoteAsync(int playerSignupId, int proposedDateId, VoteType vote, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet
            .Include(ps => ps.DateVotes)
            .FirstOrDefaultAsync(ps => ps.Id == playerSignupId, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException("Player signup not found", nameof(playerSignupId));
        }

        // ... mutate scalar/collection fields directly on the tracked entity ...

        entity.LastVoteChangeTime = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return /* bool the caller needs, e.g. "a seat was freed" */;
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(PlayerSignup model, CancellationToken token = default)
    {
        var entity = await DbSet
            .Include(ps => ps.DateVotes)
            .FirstOrDefaultAsync(ps => ps.Id == model.Id, token);
        if (entity == null) return;

        // Update scalar properties manually — never Mapper.Map(model, entity) here, because
        // AutoMapper's default map replaces navigation collections wholesale.
        entity.IsSelected = model.IsSelected;
        entity.CharacterId = model.CharacterId;
        entity.SignupRole = (int)model.Role;

        entity.DateVotes.Clear();
        var dateVoteEntities = Mapper.Map<List<PlayerDateVoteEntity>>(model.DateVotes);
        foreach (var vote in dateVoteEntities)
        {
            entity.DateVotes.Add(vote);
        }

        await DbContext.SaveChangesAsync(token);
    }
}
```

**Recommended method set for `EventSignupRepository`** (from RESEARCH.md Pattern 3 — names are illustrative, not mandated):

```csharp
Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default); // create-or-update, stamps UpdatedAt
Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default);                        // deletes the row (One-Shot only, enforced by controller)
Task AddFanOutForEventAsync(int eventId, IEnumerable<int> userIds, CancellationToken token = default);       // bulk insert Yes, UpdatedAt left null
Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default);              // single .Include(es => es.User), no N+1
Task<int> CountForEventAsync(int eventId, CancellationToken token = default);                                 // D-25/D-26 delete-confirmation count, ALL rows
```

`GetRosterForEventAsync` recommended body (RESEARCH.md Pattern 4):
```csharp
public async Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default)
{
    // The ambient EventSignupEntity filter (es.Event.GroupId == ActiveGroupId) is correct here:
    // this always runs from EventsController.Details, inside a normal request where ActiveGroupId
    // already matches the event's board (the event itself was fetched through the same filter).
    var entities = await DbContext.EventSignups
        .Include(es => es.User)
        .Where(es => es.EventId == eventId)
        .ToListAsync(token);
    return Mapper.Map<IList<EventSignup>>(entities);
}
```

**Do not add `Signups` to the `Event` domain model** — see the model section above and Anti-Patterns in RESEARCH.md. Do not use `BaseRepository.UpdateAsync`/`Mapper.Map(model, entity)` for signup writes — D-30 is explicit.

---

### `QuestBoard.Domain/Services/EventSignupService.cs` / `IEventSignupService.cs` (service, CRUD)

**Analog:** the Phase 74 `IEventService`/`EventService` pair (thin pass-through over the repository, same shape as `GroupService` below). No `EventService.cs` excerpt was needed beyond the DI registration below — follow the one-liner pass-through style visible in `GroupService`:

```csharp
internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
        => await repository.AddMemberAsync(groupId, userId, groupRole, token);

    public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
        => await repository.RemoveMemberAsync(groupId, userId, token);
}
```

---

### DI registration (config)

**`QuestBoard.Repository/Extensions/ServiceExtensions.cs`** — add alongside the existing line:
```csharp
services.AddScoped<IEventRepository, EventRepository>();
// add: services.AddScoped<IEventSignupRepository, EventSignupRepository>();
```

**`QuestBoard.Domain/Extensions/ServiceExtensions.cs`** — add alongside the existing line:
```csharp
services.AddScoped<IEventService, EventService>();
// add: services.AddScoped<IEventSignupService, EventSignupService>();
```
Both registrations are explicit `AddScoped<TInterface, TImplementation>()` calls — there is no assembly-scanning/convention-based registration in this codebase; every repository and service is listed individually in these two files.

---

### `QuestBoard.Repository/Automapper/EntityProfile.cs` (Entity ↔ DomainModel boundary)

**Analog — `Event`/`EventEntity` map** (`EntityProfile.cs:141-145`):
```csharp
// Event mapping. Group and Series are ignored on the reverse map so mapping a domain
// model onto an already-tracked entity during an update never replaces a loaded
// navigation with null.
CreateMap<EventEntity, Event>();

CreateMap<Event, EventEntity>()
    .ForMember(dest => dest.Group, opt => opt.Ignore())
    .ForMember(dest => dest.Series, opt => opt.Ignore());
```

**Analog — enum-carrying scalar field, `PlayerSignup`/`PlayerSignupEntity` map** (`EntityProfile.cs:41-49`), the pattern for `Availability` (`int` on entity) ↔ `Availability` (`VoteType` on domain model):
```csharp
CreateMap<PlayerSignup, PlayerSignupEntity>()
    .ForMember(dest => dest.Quest, opt => opt.Ignore())
    .ForMember(dest => dest.SignupRole, opt => opt.MapFrom(src => (int)src.Role));

CreateMap<PlayerSignupEntity, PlayerSignup>()
    .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (SignupRole)src.SignupRole));
```

Apply the same shape for `EventSignup`/`EventSignupEntity`: `.ForMember(dest => dest.Availability, opt => opt.MapFrom(src => (int)src.Availability))` on the domain→entity map, `(VoteType)src.Availability` on the reverse. Also `.ForMember(dest => dest.Event, opt => opt.Ignore())` and `.ForMember(dest => dest.User, opt => opt.Ignore())` on the domain→entity map (navigations, same reasoning as `Event`/`Group`/`Series`). `HasAnswered` is a computed domain-model-only property — ignore it on the entity-bound map or simply don't reference it (AutoMapper only maps members present on both sides by convention; a getter-only property with no entity counterpart needs no explicit `Ignore()` on the *entity→model* direction, but the *model→entity* direction has no matching destination member either, so nothing to configure).

---

### `QuestBoard.Service/Automapper/ViewModelProfile.cs` (DomainModel ↔ ViewModel boundary)

**Analog — `Event`/`EventViewModel` map** (`ViewModelProfile.cs:103-114`):
```csharp
// Event to EventViewModel
CreateMap<Event, EventViewModel>()
    .ForMember(dest => dest.CanManage, opt => opt.Ignore());

// EventViewModel to Event
// GroupId, SeriesId, SeriesSlotIndex and CreatedAt are set server-side and are never
// taken from a submitted form, because a hidden field is not a security boundary.
CreateMap<EventViewModel, Event>()
    .ForMember(dest => dest.GroupId, opt => opt.Ignore())
    .ForMember(dest => dest.SeriesId, opt => opt.Ignore())
    .ForMember(dest => dest.SeriesSlotIndex, opt => opt.Ignore())
    .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
```
Follow this shape for a new `EventSignupViewModel` (roster row + button state): map `EventSignup → EventSignupViewModel` one-way (read-only roster display), ignoring any server-computed display flag the same way `CanManage` is ignored. There is no reverse map needed if the write actions (`SetAvailability`/`Withdraw`) take primitive route/form parameters (`eventId`, `availability`) rather than round-tripping a bound view model — this mirrors `QuestController.ChangeVote(int id, VoteType vote)` taking primitives, not a bound model.

---

### `QuestBoard.Service/Controllers/Events/EventsController.cs` (controller, request-response)

**Current full file already read** — key excerpts to build from:

**Imports/class shape** (already established in this file, extend rather than duplicate):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.EventViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class EventsController(
    IEventService eventService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
```
Add `IEventSignupService eventSignupService` (or fold the calls through `eventService` if that is the chosen shape) to the constructor.

**Analog for the vote-change action — `QuestController.ChangeVote`** (`QuestController.cs:596-632`):
```csharp
public async Task<IActionResult> ChangeVote(int id, VoteType vote)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || !quest.IsFinalized || quest.FinalizedDate == null)
        return BadRequest("Quest not found or not finalized.");

    var user = await userService.GetUserAsync(User);
    if (user == null)
        return Challenge();

    // Find the user's signup for this quest — never trust a client-supplied signup id
    var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
    if (playerSignup == null)
    {
        return BadRequest("You are not signed up for this quest.");
    }

    if (!Enum.IsDefined(typeof(VoteType), vote))
    {
        return BadRequest("Invalid vote value.");
    }

    await questService.ChangeVoteAsync(id, playerSignup.Id, vote, finalizedProposedDate.Id);

    return Ok();
}
```
D-09's "acting user always from `User`, never the request body" is exactly this shape: `userService.GetUserAsync(User)` resolves the actor, and the target row is found *by that resolved user*, never by a submitted user id. `SetAvailability(int eventId, VoteType availability)` should follow this exactly — resolve `eventId` from the route, `availability` from the form, and `userId` only from `User`.

**Analog for the delete-my-own-row action — `QuestController.RevokeSignup`** (`QuestController.cs:637-660`):
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

    await questService.RevokeSignupAsync(id, playerSignup.Id);

    return Ok();
}
```
`Withdraw(int eventId)` follows this shape, plus D-08's board-type guard below.

**Analog for the server-side board-type guard — `QuestController.Close`** (`QuestController.cs:748-785`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Close(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);

    if (quest == null || quest.IsClosed)
    {
        return NotFound();
    }

    // Close/Reopen only makes sense for campaign-board quests; never trust the
    // client-rendered button visibility to enforce this server-side.
    var boardType = await GetActiveBoardTypeAsync();
    if (boardType != BoardType.Campaign)
    {
        return BadRequest("Close is only supported for campaign quests.");
    }

    // ...

    await questService.CloseQuestAsync(id);

    return RedirectToAction("Manage", new { id });
}
```
For `Withdraw`, D-08 requires the inverse comparison: resolve the board type server-side (`EventsController` already has `IActiveGroupContext activeGroupContext` — resolve via the same `IBoardTypeResolver` mechanism `QuestController` uses, injected the same way) and `return BadRequest(...)` when the board is **not** One-Shot, never trusting that the Withdraw button was hidden client-side on Campaign boards.

**Fan-out on Create (D-15)** — extend the existing `Create` POST action (already read in full above) immediately after `await eventService.AddAsync(newEvent, token);`, in the same request/unit of work:
```csharp
await eventService.AddAsync(newEvent, token);

if (/* active board's BoardType == Campaign */)
{
    var memberIds = await userService.GetAllGroupMembersAsync(activeGroupId, token); // GetAllGroupMembers precedent — role-agnostic, D-14
    await eventSignupService.AddFanOutForEventAsync(newEvent.Id, memberIds.Select(m => m.Id), token);
}
```
D-16 says this fan-out runs regardless of the event's date — no date comparison belongs here, only in the join/leave backfill (D-17).

---

### `QuestBoard.Service/Views/Events/Details.cshtml` (view)

**Analog — three vote buttons + roster region + revoke button, `Views/Quest/Details.cshtml:718-745`:**
```html
@* Revoke and Update button section - shown for any signed up user *@
@if (boardType != BoardType.Campaign && User.Identity?.IsAuthenticated == true && (bool)ViewBag.IsPlayerSignedUp)
{
    <div class="d-flex justify-content-between">
        <div class="d-flex gap-2">
            <button type="button" class="btn btn-danger" onclick="revokeSignup(@ViewContext.RouteData.Values["id"])">
                <i class="fas fa-times me-2"></i>Revoke My Signup
            </button>
        </div>
        <div class="d-flex gap-2">
            <button type="button" class="btn btn-success" onclick="changeVote(@Model.Quest?.Id, 2)">
                <i class="fas fa-check me-2"></i>Vote Yes
            </button>
            <button type="button" class="btn btn-warning" onclick="changeVote(@Model.Quest?.Id, 1)">
                <i class="fas fa-question me-2"></i>Vote Maybe
            </button>
            <button type="button" class="btn btn-danger" onclick="changeVote(@Model.Quest?.Id, 0)">
                <i class="fas fa-times me-2"></i>Vote No
            </button>
        </div>
    </div>
}
```
Numeric values `2`/`1`/`0` are `VoteType.Yes`/`Maybe`/`No` — reuse the enum's int values directly in the `onclick`, matching this exact idiom.

**Analog — roster row rendering, `Views/Quest/Details.cshtml:195-224`:**
```html
<td>
    @if (participantVote == VoteType.Yes)
    {
        <span class="badge bg-success"><i class="fas fa-check me-1"></i>Yes</span>
    }
    else if (participantVote == VoteType.Maybe)
    {
        <span class="badge bg-warning text-dark"><i class="fas fa-question me-1"></i>Maybe</span>
    }
    else if (participantVote == VoteType.No)
    {
        <span class="badge bg-danger"><i class="fas fa-times me-1"></i>No</span>
    }
    else
    {
        <span class="badge bg-secondary"><i class="fas fa-minus me-1"></i>No Vote</span>
    }
</td>
```
D-04 says the roster must NOT distinguish an untouched Campaign default — so the `else` "No Vote"/unanswered badge only applies on a One-Shot board's absent rows (D-03: no "hasn't answered" row is rendered at all there, so the "No Vote" branch above is only reachable, if at all, in a form this phase doesn't need — a Campaign roster always has a row, so every row hits Yes/Maybe/No, never the else).

**Analog — `changeVote()` fetch script, `Views/Quest/Details.cshtml:966-980`:**
```javascript
function changeVote(questId, vote) {
    const formData = new FormData();
    formData.append('__RequestVerificationToken', '@tokens.RequestToken');
    formData.append('vote', vote);

    fetch(`/Quest/ChangeVote/${questId}`, {
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
    });
}
```
Copy verbatim, retargeting the URL to `/Events/SetAvailability/${eventId}`.

**Analog — `revokeSignup()` fetch script with `confirm()`, `Views/Quest/Details.cshtml:944-964`:**
```javascript
function revokeSignup(questId) {
    if (confirm("Are you sure you want to revoke your signup for this quest? This action cannot be undone.")) {
        const formData = new FormData();
        formData.append('__RequestVerificationToken', '@tokens.RequestToken');

        fetch(`/Quest/RevokeSignup/${questId}`, {
            method: "DELETE",
            body: formData
        }).then(res => {
            if (res.ok) {
                location.reload();
            } else {
                res.text().then(text => {
                    alert(`Failed to revoke signup: ${text}`);
                });
            }
        }).catch(err => {
            alert("An error occurred while revoking signup.");
        });
    }
}
```
Copy for `Withdraw`, retargeting to `/Events/Withdraw/${eventId}`; this button only renders when the board is One-Shot (D-08), matching the `@if (boardType != BoardType.Campaign ...)` guard shape above.

Per `Views/Events/Details.cshtml` note in `EventsController.Details`: `viewModel.CanManage` is already computed server-side — extend that same pattern to compute a `CanWithdraw`/board-type flag for the view rather than inlining `IBoardTypeResolver` calls into the Razor file.

---

### `QuestBoard.Domain/Services/GroupService.cs` (`AddMemberAsync`/`RemoveMemberAsync`)

**Current file — full, both methods are one-line pass-throughs today:**
```csharp
public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
    => await repository.AddMemberAsync(groupId, userId, groupRole, token);

public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
    => await repository.RemoveMemberAsync(groupId, userId, token);
```
D-18/D-23 hook these two methods, but per RESEARCH.md's Primary Recommendation, the actual fan-out/cleanup logic and the atomic `SaveChangesAsync` belong in `GroupRepository`, not here — these Domain-layer methods likely stay unchanged pass-throughs, or gain only a doc-comment noting they're the chokepoint. Do not put `IgnoreQueryFilters()` logic in this file; it belongs in the repository (Pattern 2 in RESEARCH.md).

---

### `QuestBoard.Repository/GroupRepository.cs` (`AddMemberAsync`/`RemoveMemberAsync`)

**Current `AddMemberAsync`, full (`GroupRepository.cs:49-75`):**
```csharp
public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
{
    // Check existence first — UserGroups has unique composite index on (UserId, GroupId)
    var exists = await DbContext.UserGroups
        .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (exists)
        throw new InvalidOperationException("User is already a member of this group.");

    DbContext.UserGroups.Add(new UserGroupEntity
    {
        UserId = userId,
        GroupId = groupId,
        GroupRole = (int)groupRole
    });

    try
    {
        await DbContext.SaveChangesAsync(token);
    }
    catch (DbUpdateException)
    {
        // A concurrent request can win the race between the AnyAsync check above and this
        // insert; the table's unique index on (UserId, GroupId) then rejects the write.
        // Surface it as the same friendly exception the pre-check throws.
        throw new InvalidOperationException("User is already a member of this group.");
    }
}
```
**Current `RemoveMemberAsync`, full (`GroupRepository.cs:78-85`):**
```csharp
public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (ug == null) return;
    DbContext.UserGroups.Remove(ug);
    await DbContext.SaveChangesAsync(token);
}
```

**Atomicity precedent to graft in — `CharacterRepository.UpdateWithProfileImageAsync` shape** (Phase 45's shipped fix; single tracked graph, one `SaveChangesAsync`, no `BeginTransactionAsync` because the InMemory provider every test uses throws on it):
```csharp
var entity = await DbContext.Characters
    .Include(c => c.Classes)
    .Include(c => c.ProfileImage)
    .FirstOrDefaultAsync(c => c.Id == model.Id, token);
if (entity == null) return;

Mapper.Map(model, entity);
ApplyProfileImage(entity, originalImageData, croppedImageData);

await DbContext.SaveChangesAsync(token);   // ONE call, both mutations committed together
```

**Recommended `AddMemberAsync` shape, preserving the existing race-handling catch:**
```csharp
public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
{
    var exists = await DbContext.UserGroups
        .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (exists)
        throw new InvalidOperationException("User is already a member of this group.");

    DbContext.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)groupRole });

    // BoardType resolved from the explicit groupId, never from IBoardTypeResolver/ActiveGroupId —
    // GroupEntity carries no query filter, so this is safe regardless of the caller's own active board.
    var group = await DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
    if (group?.BoardType == (int)BoardType.Campaign)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Explicit groupId bypasses the ambient ActiveGroupId filter deliberately.
        var futureEventIds = await DbContext.Events
            .IgnoreQueryFilters()
            .Where(e => e.GroupId == groupId && e.Date >= today)
            .Select(e => e.Id)
            .ToListAsync(token);

        foreach (var eventId in futureEventIds)
        {
            // No UpdatedAt stamp — this is an automatic backfill, not a human answer (D-10/D-13).
            DbContext.EventSignups.Add(new EventSignupEntity
            {
                EventId = eventId,
                UserId = userId,
                Availability = (int)VoteType.Yes
            });
        }
    }

    try
    {
        await DbContext.SaveChangesAsync(token); // membership + backfill committed together
    }
    catch (DbUpdateException)
    {
        throw new InvalidOperationException("User is already a member of this group.");
    }
}
```
Mirror this shape for `RemoveMemberAsync` — load the `UserGroups` row, query existing signup rows for that `(groupId, userId)` with `IgnoreQueryFilters()` + explicit `Where`, stage both removals, one `SaveChangesAsync`.

---

### `QuestBoard.Domain/Interfaces/IEventRepository.cs` / `EventRepository.cs` (explicit-groupId query)

**Analog — `QuestRepository.GetQuestsForTomorrowAllGroupsAsync`, verbatim (`QuestBoard.Repository/QuestRepository.cs:264-271`):**
```csharp
/// <inheritdoc/>
public async Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default)
{
    // Explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter on QuestEntity
    var entities = await ProjectWithoutCharacterImages(DbContext.Quests.IgnoreQueryFilters())
        .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
        .ToListAsync(token);
    return Mapper.Map<IList<Quest>>(entities);
}
```
This is the codebase's **only** other production use of `IgnoreQueryFilters()` and the naming/comment convention to match: the `...AllGroups` suffix signals "deliberately cross-tenant," and the one-line comment states the reason inline at the call site. The join/leave backfill query is narrower — it re-imposes scope with an explicit `Where(GroupId == groupId)` immediately after `IgnoreQueryFilters()` (unlike this precedent, which has no such re-scoping because its whole point is cross-group). Recommended new method, matching the naming convention:
```csharp
// Explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter on EventEntity,
// re-scoped immediately by the explicit groupId parameter rather than the ambient ActiveGroupId.
Task<IList<int>> GetFutureEventIdsForGroupAsync(int groupId, DateOnly today, CancellationToken token = default);
```

**Query filter block this must bypass deliberately — `QuestBoard.Repository/Entities/QuestBoardContext.cs:420-441`, verbatim:**
```csharp
// Event/EventSeries/EventSignup filters follow the same fail-closed rule as
// everything above: with no group selected, every event query returns nothing
// rather than every board's events merged together. activeGroupContext is
// dereferenced inline in each lambda rather than read into a local first.
modelBuilder.Entity<EventEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// A series cannot be scoped through an event, because the foreign key points from
// event to series and is nullable, so a series must carry its own group.
modelBuilder.Entity<EventSeriesEntity>()
    .HasQueryFilter(es =>
        activeGroupContext.ActiveGroupId != null &&
        es.GroupId == activeGroupContext.ActiveGroupId);

// This filter is added now, before any code reads the table, so the scoping
// convention is settled rather than retrofitted later.
modelBuilder.Entity<EventSignupEntity>()
    .HasQueryFilter(es =>
        activeGroupContext.ActiveGroupId != null &&
        es.Event.GroupId == activeGroupContext.ActiveGroupId);
```
And a few lines above it, the general fail-closed / do-not-capture warning that applies to every filter in the block (`QuestBoardContext.cs:326-334`):
```csharp
// Global query filters for group isolation.
// QuestEntity and ShopItemEntity carry a GroupId and are the two entities directly scoped
// to a tenant, so every read is automatically restricted to the caller's active group.
// A null ActiveGroupId (no group selected yet, or a session that never picked one) must
// return zero rows, never every group's rows merged together — the caller has to pick a
// group before any group-scoped data is servable, full stop.
// Lambda closes over activeGroupContext instance — re-evaluated per query, not at startup
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
//           That captures the value once (null at model-build time). Always reference the service.
```
This "do not capture into a local var" warning is about the *model-build-time* lambda closure and does not directly constrain the new repository methods (they run per-request, not at model build), but the same "always reference the live groupId parameter, never a stale copy" discipline applies to `GetFutureEventIdsForGroupAsync`'s `groupId` parameter usage.

---

### `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (`RemoveMember` confirmation, D-24)

**Current `RemoveMember`, full (`GroupController.cs:334-339`) — no confirmation today:**
```csharp
public async Task<IActionResult> RemoveMember(int id, int userId, string? search, string? memberSearch)
{
    await groupService.RemoveMemberAsync(id, userId);
    TempData["Success"] = "Member removed from the group.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
}
```
D-24 requires a confirmation naming what is lost (all that member's event signups on this board, D-20). Follow the app's native `confirm()` idiom (same as `revokeSignup()`'s `confirm()` above) on the *Members view's* remove button, not a Bootstrap modal — this is the same idiom D-25 mandates for the event-delete count. The controller action itself needs no server-side change beyond what D-24 implies for the view; verify against the Members view's current remove-button markup before assuming a controller change is required.

---

## Shared Patterns

### Acting-user resolution (D-09)
**Source:** `QuestController.ChangeVote`/`RevokeSignup` — `var user = await userService.GetUserAsync(User); if (user == null) return Challenge();` followed by locating *that resolved user's own* row, never a submitted id.
**Apply to:** `EventsController.SetAvailability`, `EventsController.Withdraw`.

### Server-side board-type re-resolution, never trust client markup (D-08)
**Source:** `QuestController.Close`/`Reopen` — `var boardType = await GetActiveBoardTypeAsync(); if (boardType != BoardType.Campaign) return BadRequest(...);`
**Apply to:** `EventsController.Withdraw` (inverse check: reject if board is not One-Shot).

### Narrow scalar-update repository methods, never `BaseRepository.UpdateAsync` for signups (D-30)
**Source:** `PlayerSignupRepository.ChangeVoteAsync`/`UpdateAsync` override — load tracked entity, mutate fields directly, one `SaveChangesAsync`.
**Apply to:** `EventSignupRepository.SetAvailabilityAsync`, `WithdrawAsync`, `AddFanOutForEventAsync`.

### Single-`SaveChangesAsync` atomicity, never `BeginTransactionAsync` (D-19/D-20)
**Source:** `CharacterRepository.UpdateWithProfileImageAsync` (Phase 45 precedent) — stage every mutation on one `DbContext`, save once. `BeginTransactionAsync` throws `InvalidOperationException` on the InMemory provider every test in this repo uses.
**Apply to:** `GroupRepository.AddMemberAsync` (membership + backfill), `GroupRepository.RemoveMemberAsync` (membership + cleanup).

### `IgnoreQueryFilters()` deliberately paired with an explicit `Where(GroupId == param)` (D-28, Pitfall 2)
**Source:** `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` — the codebase's only other cross-group query, with an inline comment stating the reason.
**Apply to:** the new `IEventRepository.GetFutureEventIdsForGroupAsync` and its `EventSignups`-cleanup equivalent inside `GroupRepository`. Never resolve via `IBoardTypeResolver`/ambient `ActiveGroupId` inside `GroupService`/`GroupRepository`'s membership hooks — resolve `BoardType` via `DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId)` instead (`GroupEntity` carries no query filter).

### Fail-closed query filter shape
**Source:** `QuestBoardContext.cs:420-441` (`EventEntity`/`EventSeriesEntity`/`EventSignupEntity` filters) — `activeGroupContext.ActiveGroupId != null && <scope> == activeGroupContext.ActiveGroupId`, referenced live per query, never captured into a local.
**Apply to:** understanding why every "normal" read is board-scoped, and why the join/leave backfill queries must deliberately bypass it via `IgnoreQueryFilters()`.

### The `changeVote()`/`revokeSignup()` fetch idiom
**Source:** `Views/Quest/Details.cshtml:944-980` — `FormData` + `__RequestVerificationToken`, `fetch`, `location.reload()` on success, `alert()` on failure, native `confirm()` for destructive actions.
**Apply to:** `Views/Events/Details.cshtml`'s Yes/Maybe/No buttons and Withdraw button; the D-24/D-25 confirmation dialogs.

## No Analog Found

None — every file in this phase's expected set has a direct or role-matched precedent already shipped in this codebase (Phase 74's Event scaffolding, Phase 45's atomicity fix, and the existing Quest/PlayerSignup/Group machinery cover every shape needed).

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Views/`, `QuestBoard.Service/Areas/Platform/Controllers/`, `QuestBoard.IntegrationTests/Tests/`
**Files scanned:** `EventSignupEntity.cs`, `EventEntity.cs`, `Event.cs`, `IEventRepository.cs`, `PlayerSignupRepository.cs`, `QuestRepository.cs`, `GroupRepository.cs`, `GroupService.cs`, `QuestBoardContext.cs`, `EventsController.cs`, `QuestController.cs`, `Views/Quest/Details.cshtml`, `EntityProfile.cs`, `ViewModelProfile.cs`, `Areas/Platform/Controllers/GroupController.cs`, `QuestBoard.Repository/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs`
**Pattern extraction date:** 2026-08-27
