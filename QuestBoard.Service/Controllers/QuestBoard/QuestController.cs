using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using QuestBoard.Service.ViewModels.QuestViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;

public class QuestController(
    IUserService userService,
    IMapper mapper,
    IPlayerSignupService playerSignupService,
    IQuestService questService,
    ICharacterService characterService,
    IReminderJobDispatcher reminderJobDispatcher,
    IActiveGroupContext activeGroupContext,
    IGroupService groupService
    ) : Controller
{
    [HttpGet]
    [Route("quests")]
    [Authorize]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        // Get current user if authenticated to check if they're a DM and for signup status
        string? currentUserName = null;
        int? currentUserId = null;
        Role userRole = Role.Player; // Default to Player role

        var userEntity = await userService.GetUserAsync(User);
        if (userEntity != null)
        {
            var user = await userService.GetByIdAsync(userEntity.Id, token);
            currentUserName = user?.Name;
            currentUserId = user?.Id;

            // Determine user role for quest filtering. SuperAdmin has no active group by design
            // (ActiveGroupContextExtensions.RequireActiveGroupId's documented contract), so only
            // require a concrete group id for the non-SuperAdmin path — GetEffectiveGroupRoleAsync
            // short-circuits SuperAdmin to GroupRole.Admin without ever using the group id.
            var role = User.IsInRole("SuperAdmin")
                ? GroupRole.Admin
                : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());

            if (role == GroupRole.Admin)
                userRole = Role.Admin;
            else if (role == GroupRole.DungeonMaster)
                userRole = Role.DungeonMaster;
        }

        // Get quests filtered by user role
        var isAdminOrDm = userRole == Role.Admin || userRole == Role.DungeonMaster;
        var quests = await questService.GetQuestsWithSignupsForRoleAsync(isAdminOrDm, token);

        ViewBag.CurrentUserName = currentUserName;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.BoardType = await GetActiveBoardTypeAsync(token);
        return View(quests);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Create(CancellationToken token = default)
    {
        // Get current user
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        ViewBag.BoardType = await GetActiveBoardTypeAsync(token);
        return View(new QuestViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Create(QuestViewModel viewModel, CancellationToken token = default)
    {
        // Get current user
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Automatically set the current user as the DM
        viewModel.DungeonMasterId = currentUser.Id;

        // BoardType is always resolved server-side from the active group, never trusted
        // from the posted form.
        var boardType = await GetActiveBoardTypeAsync(token);
        if (boardType == BoardType.OneShot && (viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0))
        {
            ModelState.AddModelError(nameof(viewModel.ProposedDates), "At least one proposed date is required.");
        }
        else if (boardType == BoardType.Campaign)
        {
            // Campaign quests have no date picker or per-quest signup — silently override
            // any posted values with fixed defaults regardless of what the client sent.
            viewModel.ProposedDates = [];
            viewModel.ChallengeRating = 1;
            viewModel.TotalPlayerCount = 0;
            viewModel.DungeonMasterSession = false;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.BoardType = boardType;
            return View(viewModel);
        }

        // Create Quest entity from ViewModel using AutoMapper
        var quest = mapper.Map<Quest>(viewModel);

        // Tag the quest to the active group so it is visible on the correct board
        // (QuestEntity is scoped by a global query filter on GroupId).
        quest.GroupId = activeGroupContext.RequireActiveGroupId();

        // Set Quest reference for all ProposedDates
        foreach (var proposedDate in quest.ProposedDates)
        {
            proposedDate.Quest = quest;
            proposedDate.QuestId = quest.Id;
        }

        await questService.AddAsync(quest, token);

        return RedirectToAction("Index");
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Edit(int id, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        // Don't allow editing of finalized quests
        if (quest.IsFinalized)
        {
            return BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.");
        }

        var dms = await userService.GetAllDungeonMastersAsync(token);
        var questViewModel = mapper.Map<QuestViewModel>(quest);

        // Allow editing proposed dates even with signups (service will handle it intelligently)
        var canEditProposedDates = true;
        var hasExistingSignups = quest.PlayerSignups.Any();

        return View(new EditQuestViewModel
        {
            Id = quest.Id,
            Quest = questViewModel,
            DungeonMasters = dms,
            CanEditProposedDates = canEditProposedDates,
            HasExistingSignups = hasExistingSignups
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Edit(int id, EditQuestViewModel viewModel, CancellationToken token = default)
    {
        if (id != viewModel.Id)
        {
            return BadRequest();
        }

        var existingQuest = await questService.GetQuestWithDetailsAsync(id, token);

        if (existingQuest == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, existingQuest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        // Don't allow editing of finalized quests
        if (existingQuest.IsFinalized)
        {
            return BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.");
        }

        // Allow editing of proposed dates even with signups (service will handle it intelligently)
        var canEditProposedDates = true;
        var hasExistingSignups = existingQuest.PlayerSignups.Any();
        viewModel.CanEditProposedDates = canEditProposedDates;
        viewModel.HasExistingSignups = hasExistingSignups;

        if (!ModelState.IsValid)
        {
            var dms = await userService.GetAllDungeonMastersAsync(token);
            viewModel.DungeonMasters = dms;
            return View(viewModel);
        }

        await questService.UpdateQuestPropertiesWithNotificationsAsync(
            id,
            viewModel.Quest.Title,
            viewModel.Quest.Description,
            viewModel.Quest.ChallengeRating,
            viewModel.Quest.TotalPlayerCount,
            viewModel.Quest.DungeonMasterSession,
            true,
            viewModel.Quest.ProposedDates,
            token
        );

        return RedirectToAction("Manage", new { id });
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);

        if (quest == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM or an Admin
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        await questService.RemoveAsync(quest);

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        // Get current user if authenticated
        User? currentUser = null;
        IList<Character>? userCharacters = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userEntity = await userService.GetUserAsync(User);
            if (userEntity != null)
            {
                currentUser = await userService.GetByIdAsync(userEntity.Id, token);
                
                // Get user's active characters
                if (currentUser != null)
                {
                    var allCharacters = await characterService.GetCharactersByOwnerIdAsync(currentUser.Id, token);
                    userCharacters = allCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
                }
            }
        }

        // Check if current user is signed up
        ViewBag.IsPlayerSignedUp = currentUser != null && quest.PlayerSignups.Any(ps => ps.Player.Id == currentUser.Id);
        ViewBag.UserCharacters = userCharacters ?? new List<Character>();

        // Check if current user can manage this quest (DM or admin)
        var isQuestDm = currentUser != null && IsQuestOwner(currentUser, quest.DungeonMaster);
        var isAdmin = currentUser != null && await GetEffectiveRoleAsync() == GroupRole.Admin;
        ViewBag.CanManage = isQuestDm || isAdmin;

        // Get all quests for calendar context
        var allQuests = await questService.GetQuestsForCalendarAsync(token);

        // Get unique months that have proposed dates for this quest
        var monthsWithProposedDates = quest.ProposedDates
            .Select(pd => new { pd.Date.Year, pd.Date.Month })
            .Distinct()
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .Select(m => new CalendarViewModel
            {
                Year = m.Year,
                Month = m.Month,
                Quests = allQuests.ToList()
            })
            .ToList();

        ViewBag.CalendarMonths = monthsWithProposedDates;
        ViewBag.IsDetailsPage = true;
        ViewBag.CurrentQuestId = id;
        ViewBag.CurrentUserId = currentUser?.Id;
        ViewBag.BoardType = await GetActiveBoardTypeAsync(token);

        var signup = new PlayerSignup
        {
            Quest = quest,
            Player = currentUser ?? new User { Name = "", Email = "" },
            DateVotes = [.. quest.ProposedDates.Select(x => new PlayerDateVote { ProposedDate = x, ProposedDateId = x.Id })],
        };

        return View(signup);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Details(PlayerSignup signup, int selectedRole = 0)
    {
        if (signup.Quest?.Id == null || signup.Quest.Id == 0) return NotFound();
        var questId = signup.Quest.Id;

        var quest = await questService.GetQuestWithDetailsAsync(questId);
        if (quest == null || quest.IsFinalized)
            return NotFound();

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Check if user already signed up
        if (quest.PlayerSignups.Any(ps => ps.Player.Id == user.Id))
        {
            ModelState.AddModelError("", "You have already signed up for this quest.");
            return await Details(questId);
        }

        // Use the authenticated user instead of form input
        signup.Player = user;
        signup.Quest = quest;
        signup.Role = (SignupRole)selectedRole; // Set role from form

        
        // Validate character if selected
        if (signup.CharacterId.HasValue)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(signup.CharacterId.Value);
            if (character == null || character.OwnerId != user.Id || character.Status != CharacterStatus.Active)
            {
                ModelState.AddModelError("", "Invalid character selection.");
                return await Details(questId);
            }
        }

        await playerSignupService.AddAsync(signup);

        return RedirectToAction("Details", new { id = questId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> JoinFinalizedQuest(int questId, int? characterId = null, int selectedRole = 0)
    {
        var quest = await questService.GetQuestWithDetailsAsync(questId);
        if (quest == null || !quest.IsFinalized || quest.FinalizedDate == null)
            return NotFound();

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Check if user already signed up
        if (quest.PlayerSignups.Any(ps => ps.Player.Id == user.Id))
        {
            ModelState.AddModelError("", "You have already signed up for this quest.");
            return RedirectToAction("Details", new { id = questId });
        }

        var role = (SignupRole)selectedRole;

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

        // Validate character if selected
        if (characterId.HasValue)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(characterId.Value);
            if (character == null || character.OwnerId != user.Id || character.Status != CharacterStatus.Active)
            {
                ModelState.AddModelError("", "Invalid character selection.");
                return RedirectToAction("Details", new { id = questId });
            }
        }

        // Find the finalized date's corresponding proposed date for vote creation
        var finalizedProposedDate = quest.ProposedDates
            .FirstOrDefault(pd => pd.Date.Date == quest.FinalizedDate.Value.Date);

        if (finalizedProposedDate == null)
        {
            ModelState.AddModelError("", "Could not find the finalized date information.");
            return RedirectToAction("Details", new { id = questId });
        }

        // Create signup
        var signup = new PlayerSignup
        {
            Player = user,
            Quest = quest,
            CharacterId = characterId,
            Role = role,
            IsSelected = true, // Auto-approve all roles when joining finalized quest
            DateVotes = role == SignupRole.Spectator ? [] : // Spectators don't vote
                [new PlayerDateVote
                {
                    ProposedDateId = finalizedProposedDate.Id,
                    Vote = VoteType.Yes
                }]
        };

        await playerSignupService.AddAsync(signup);

        return RedirectToAction("Details", new { id = questId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> UpdateSignup(int questId, List<PlayerDateVote> dateVotes)
    {
        var quest = await questService.GetQuestWithDetailsAsync(questId);
        if (quest == null || quest.IsFinalized)
            return NotFound();

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Find the user's signup for this quest
        var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
        if (playerSignup == null)
        {
            return BadRequest("You are not signed up for this quest.");
        }

        // Update the player's date votes
        await playerSignupService.UpdatePlayerDateVotesAsync(playerSignup.Id, dateVotes);

        return RedirectToAction("Details", new { id = questId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> UpdateSignupCharacter(int questId, int? characterId)
    {
        var quest = await questService.GetQuestWithDetailsAsync(questId);
        if (quest == null)
            return NotFound();

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Find the user's signup for this quest
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangeVoteToYes(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);
        if (quest == null || !quest.IsFinalized || quest.FinalizedDate == null)
            return BadRequest("Quest not found or not finalized.");

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Find the user's signup for this quest
        var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
        if (playerSignup == null)
        {
            return BadRequest("You are not signed up for this quest.");
        }

        // Check if already selected
        if (playerSignup.IsSelected)
        {
            return BadRequest("You are already selected for this quest.");
        }

        // Check if there are available spots for players
        var selectedPlayersCount = quest.PlayerSignups
            .Where(ps => ps.IsSelected && ps.Role == SignupRole.Player)
            .Count();

        if (selectedPlayersCount >= quest.TotalPlayerCount)
        {
            return BadRequest("No available spots in this quest.");
        }

        // Find the finalized date's corresponding proposed date
        var finalizedProposedDate = quest.ProposedDates
            .FirstOrDefault(pd => pd.Date.Date == quest.FinalizedDate.Value.Date);

        if (finalizedProposedDate == null)
        {
            return BadRequest("Could not find the finalized date information.");
        }

        // Use the specialized service method to update vote and mark as selected
        await playerSignupService.ChangeVoteToYesAndSelectAsync(playerSignup.Id, finalizedProposedDate.Id);

        return Ok();
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> RevokeSignup(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);
        if (quest == null)
            return NotFound();

        // Get current authenticated user
        var user = await userService.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Find the user's signup for this quest
        var playerSignup = quest.PlayerSignups.FirstOrDefault(ps => ps.Player.Id == user.Id);
        if (playerSignup == null)
        {
            return BadRequest("You are not signed up for this quest.");
        }

        // Remove the player signup (allow revoking at any time)
        await playerSignupService.RemoveAsync(playerSignup);

        return Ok();
    }

    [HttpDelete("Quest/RemovePlayerSignup/{id}")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemovePlayerSignup(int id)
    {
        // Get the signup
        var signup = await playerSignupService.GetByIdAsync(id);
        if (signup == null)
        {
            return NotFound();
        }

        // Remove the player signup (this will cascade delete all associated votes)
        await playerSignupService.RemoveAsync(signup);

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Finalize(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);
        if (quest == null || quest.IsFinalized) return NotFound();
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();
        if (!int.TryParse(Request.Form["SelectedDateId"], out var selectedDateId))
        { TempData["Error"] = "Please select a date."; return RedirectToAction("Manage", new { id }); }
        var selectedDate = quest.ProposedDates.FirstOrDefault(pd => pd.Id == selectedDateId);
        if (selectedDate == null)
        { TempData["Error"] = "Please select a date."; return RedirectToAction("Manage", new { id }); }
        var selectedPlayerIds = ParseSelectedPlayerIds(Request.Form["SelectedPlayerIds"]);
        var playerRoleCount = quest.PlayerSignups.Where(ps => selectedPlayerIds.Contains(ps.Id) && ps.Role == SignupRole.Player).Count();
        if (playerRoleCount > quest.TotalPlayerCount)
        { TempData["Error"] = $"Cannot select more than {quest.TotalPlayerCount} players."; return RedirectToAction("Manage", new { id }); }
        await questService.FinalizeQuestAsync(id, selectedDate.Date, selectedPlayerIds);
        return RedirectToAction("Details", new { id });
    }

    private static List<int> ParseSelectedPlayerIds(Microsoft.Extensions.Primitives.StringValues raw) =>
        raw.Where(s => !string.IsNullOrEmpty(s) && int.TryParse(s, out _))
           .Select(s => int.Parse(s!)).ToList();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Open(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);

        if (quest == null || !quest.IsFinalized)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Verify DM authorization
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        // Open the quest using the specialized service method
        await questService.OpenQuestAsync(id);

        return RedirectToAction("Manage", new { id });
    }

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

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Verify DM authorization
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        // Close the quest using the specialized service method
        await questService.CloseQuestAsync(id);

        return RedirectToAction("Manage", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Reopen(int id)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);

        if (quest == null || !quest.IsClosed)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Verify DM authorization
        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        // Reopen the quest using the specialized service method
        await questService.ReopenQuestAsync(id);

        return RedirectToAction("Manage", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> SendReminder(int id, bool forceResend = false, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id);
        if (quest == null)
        {
            return NotFound();
        }

        if (!quest.IsFinalized)
        {
            TempData["Error"] = "Only finalized quests can send reminders.";
            return RedirectToAction("Manage", new { id });
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var role = await GetEffectiveRoleAsync();
        if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        if (!quest.FinalizedDate.HasValue)
        {
            TempData["Error"] = "Quest has no finalized date. Please re-finalize the quest.";
            return RedirectToAction("Manage", new { id });
        }

        // DM trigger sends to Yes + Maybe voters for the finalized date only.
        // RESEARCH.md Pitfall 1: filter by finalized proposed date to avoid including
        // players who voted Yes/Maybe on a different proposed date.
        var finalizedProposedDate = quest.ProposedDates
            .FirstOrDefault(pd => pd.Date.Date == quest.FinalizedDate.Value.Date);

        var eligibleSignups = quest.PlayerSignups
            .Where(ps => ps.DateVotes.Any(dv =>
                dv.ProposedDate?.Id == finalizedProposedDate?.Id &&
                (dv.Vote == VoteType.Yes || dv.Vote == VoteType.Maybe)))
            .ToList();

        if (eligibleSignups.Count == 0)
        {
            TempData["Error"] = "No eligible players found for this quest.";
            return RedirectToAction("Manage", new { id });
        }

        // Enqueue a fire-and-forget Hangfire job.
        // The job itself checks the ReminderLog per-player before sending (idempotency).
        // The forceResend flag (from the confirm button) bypasses the log check in the job.
        // Use the quest's own group id rather than the caller's ambient active group, since a
        // SuperAdmin sending a reminder has no active group selected but the quest still belongs
        // to a concrete group.
        reminderJobDispatcher.EnqueueSessionReminder(id, quest.GroupId, forceResend, useYesMaybeVoters: true);

        TempData["Success"] = $"Reminder queued for {eligibleSignups.Count} eligible players.";
        return RedirectToAction("Manage", new { id });
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Manage(int id)
    {
        var quest = await questService.GetQuestWithManageViewDetailsAsync(id);

        if (quest == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM or an admin
        var isQuestDm = IsQuestOwner(currentUser, quest.DungeonMaster);
        var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;
        ViewBag.IsAuthorized = isQuestDm || isAdmin;
        ViewBag.IsAdmin = isAdmin;
        ViewBag.BoardType = await GetActiveBoardTypeAsync();

        return View(quest);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> CreateFollowUp(int id, CancellationToken token = default)
    {
        var original = await questService.GetQuestWithDetailsAsync(id, token);
        if (original == null)
            return NotFound();

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
            return Challenge();

        // Guard: only the quest's DM or an admin may create a follow-up
        var isQuestDm = IsQuestOwner(currentUser, original.DungeonMaster);
        var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;
        if (!isQuestDm && !isAdmin)
            return Forbid();

        // Guard: enforce at most one direct follow-up
        if (original.FollowUpQuest != null)
        {
            TempData["Error"] = "A follow-up quest already exists for this quest.";
            return RedirectToAction("Manage", new { id });
        }

        // Guard: only finalized quests can have a follow-up
        if (!original.IsFinalized)
        {
            TempData["Error"] = "Only finalized quests can have a follow-up created.";
            return RedirectToAction("Manage", new { id });
        }

        // Pre-fill view model: copy fields, append title, clear dates, reset DM session
        var viewModel = new FollowUpQuestViewModel
        {
            OriginalQuestId = original.Id,
            Title = $"{original.Title} - Part 2",
            Description = original.Description,
            ChallengeRating = original.ChallengeRating,
            TotalPlayerCount = original.TotalPlayerCount,
            DungeonMasterId = original.DungeonMasterId,
            DungeonMasterSession = false,
            ProposedDates = [],   // always empty
        };

        // List IsSelected=true players for the sidebar (display only)
        ViewBag.PreApprovedPlayers = original.PlayerSignups
            .Where(ps => ps.IsSelected)
            .Select(ps => new { ps.Player.Name })
            .ToList();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> CreateFollowUp(int id, FollowUpQuestViewModel viewModel, CancellationToken token = default)
    {
        var original = await questService.GetQuestWithDetailsAsync(id, token);
        if (original == null)
            return NotFound();

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
            return Challenge();

        // Guard: only the quest's DM or an admin may create a follow-up
        var isQuestDm = IsQuestOwner(currentUser, original.DungeonMaster);
        var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;
        if (!isQuestDm && !isAdmin)
            return Forbid();

        // Guard: enforce at most one direct follow-up
        if (original.FollowUpQuest != null)
        {
            TempData["Error"] = "A follow-up quest already exists for this quest.";
            return RedirectToAction("Manage", new { id });
        }

        // Guard: only finalized quests can have a follow-up
        if (!original.IsFinalized)
        {
            TempData["Error"] = "Only finalized quests can have a follow-up created.";
            return RedirectToAction("Manage", new { id });
        }

        // Require at least one proposed date
        if (!ModelState.IsValid || viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0)
        {
            if (viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0)
            {
                ModelState.AddModelError("ProposedDates",
                    "At least one proposed date is required before saving a follow-up quest.");
            }

            ViewBag.PreApprovedPlayers = original.PlayerSignups
                .Where(ps => ps.IsSelected)
                .Select(ps => new { ps.Player.Name })
                .ToList();

            return View(viewModel);
        }

        // Override OriginalQuestId from route to prevent form spoofing
        viewModel.OriginalQuestId = id;

        // Delegate the create-shell + apply-details + rollback-on-failure sequence to the service
        try
        {
            var newQuestId = await questService.CreateFollowUpQuestWithDetailsAsync(
                id,
                viewModel.Title,
                viewModel.Description,
                viewModel.ChallengeRating,
                viewModel.TotalPlayerCount,
                viewModel.DungeonMasterSession,
                viewModel.ProposedDates,
                token);

            return RedirectToAction("Manage", new { id = newQuestId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            ViewBag.PreApprovedPlayers = original.PlayerSignups
                .Where(ps => ps.IsSelected)
                .Select(ps => new { ps.Player.Name })
                .ToList();
            return View(viewModel);
        }
    }

    // SuperAdmin legitimately has no active group selected (see ActiveGroupContextExtensions'
    // documented contract), so short-circuit to Admin here rather than calling
    // RequireActiveGroupId(), which would throw for a SuperAdmin with no active group.
    private async Task<GroupRole?> GetEffectiveRoleAsync() =>
        User.IsInRole("SuperAdmin")
            ? GroupRole.Admin
            : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());

    // Resolves the active group's board type server-side. Never trust a client-posted
    // BoardType — a single lookup per render/mutation is fine since BoardType is immutable
    // per group and every quest on a board shares it. SuperAdmin legitimately has no active
    // group selected (see ActiveGroupContextExtensions' documented contract), so default to
    // OneShot rather than calling RequireActiveGroupId(), which would throw.
    private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return BoardType.OneShot;
        }

        var group = await groupService.GetByIdAsync(groupId, token);
        return group?.BoardType ?? BoardType.OneShot;
    }

    // Id-based identity comparison for "is this the quest's DM" — deliberately avoids
    // currentUser.Equals(dungeonMaster), which is full value equality (Id, Name, Email, HasKey,
    // EmailConfirmed) and can silently return false if either operand comes from a lighter
    // projection missing one of those fields.
    private static bool IsQuestOwner(User currentUser, User? dungeonMaster) =>
        dungeonMaster != null && currentUser.Id == dungeonMaster.Id;
}
