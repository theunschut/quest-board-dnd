using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.CharacterViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Characters
{
    [Authorize]
    public class GuildMembersController(
        ICharacterService characterService,
        IUserService userService,
        IActiveGroupContext activeGroupContext,
        IMapper mapper) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var allCharacters = await characterService.GetAllCharactersWithDetailsAsync(token);
            var characterViewModels = mapper.Map<List<CharacterViewModel>>(allCharacters);

            var viewModel = new CharactersIndexViewModel
            {
                CurrentUserId = currentUser.Id,
                MyCharacters = characterViewModels.Where(c => c.OwnerId == currentUser.Id)
                    .OrderByDescending(c => c.Role == CharacterRole.Main)
                    .ThenByDescending(c => c.Status == CharacterStatus.Active)
                    .ThenBy(c => c.Name)
                    .ToList(),
                OtherCharacters = characterViewModels.Where(c => c.OwnerId != currentUser.Id)
                    .OrderBy(c => c.OwnerName)
                    .ThenByDescending(c => c.Status == CharacterStatus.Active)
                    .ThenBy(c => c.Name)
                    .ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken token = default)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(id, token);
            if (character == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            var viewModel = mapper.Map<CharacterViewModel>(character);
            // GetUserAsync never returns null - an unresolvable identity comes back as a User
            // with Id == 0. Checking Id != 0 (rather than a null check, which is always true)
            // is what actually distinguishes a resolved identity from an unresolvable one.
            var isOwner = currentUser.Id != 0 && character.OwnerId == currentUser.Id;
            GroupRole? role = null;
            if (currentUser.Id != 0)
            {
                role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            }

            viewModel.IsOwner = isOwner;
            // Admins/SuperAdmins can manage any character in their active group, not just their
            // own, so they should see the same Edit/Retire/Delete controls the owner sees. The
            // controller-side guards on Edit/Delete/ToggleRetirement remain the actual security
            // boundary — this flag only controls whether the buttons are shown.
            viewModel.CanEdit = isOwner || role == GroupRole.Admin;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var viewModel = new CharacterViewModel
            {
                OwnerId = currentUser.Id,
                OwnerName = currentUser.Name,
                Level = 1,
                Status = CharacterStatus.Active,
                Role = CharacterRole.Backup,
                Classes = [new CharacterClassViewModel { ClassLevel = 1 }]
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CharacterViewModel viewModel, CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            viewModel.OwnerId = currentUser.Id;

            // Validate class levels
            if (!await characterService.ValidateCharacterClassLevelsAsync(viewModel.Level, 
                mapper.Map<List<CharacterClass>>(viewModel.Classes)))
            {
                ModelState.AddModelError(string.Empty, 
                    "The sum of all class levels must equal the character's total level.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            // Handle profile picture upload
            if (viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0)
            {
                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedMimeTypes.Contains(viewModel.ProfilePictureFile.ContentType,
                    StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
                        "Only JPG, PNG, or GIF images are accepted.");
                    return View(viewModel);
                }
                const long maxFileSizeBytes = 5 * 1024 * 1024;
                if (viewModel.ProfilePictureFile.Length > maxFileSizeBytes)
                {
                    ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
                        "Profile picture cannot exceed 5 MB.");
                    return View(viewModel);
                }
                using var memoryStream = new MemoryStream();
                await viewModel.ProfilePictureFile.CopyToAsync(memoryStream, token);
                viewModel.ProfilePicture = memoryStream.ToArray();
            }

            var character = mapper.Map<Character>(viewModel);

            // Tag the character to the active group so Guild Members scoping applies
            // (CharacterEntity is scoped by a global query filter on GroupId).
            character.GroupId = activeGroupContext.RequireActiveGroupId();

            await characterService.AddAsync(character, token);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken token = default)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(id, token);
            if (character == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            // An Admin (per-group role, or the global SuperAdmin role, both resolved by
            // GetEffectiveGroupRoleAsync) may edit any character in their active group, not just
            // their own, so a broken/stranded character sheet can be corrected without needing
            // the player's own login.
            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            if (character.OwnerId != currentUser.Id && role != GroupRole.Admin)
            {
                return Forbid();
            }

            var viewModel = mapper.Map<CharacterViewModel>(character);
            viewModel.IsOwner = character.OwnerId == currentUser.Id;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CharacterViewModel viewModel, CancellationToken token = default)
        {
            if (id != viewModel.Id)
            {
                return BadRequest();
            }

            var existingCharacter = await characterService.GetCharacterWithDetailsAsync(id, token);
            if (existingCharacter == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            // An Admin (per-group role, or the global SuperAdmin role, both resolved by
            // GetEffectiveGroupRoleAsync) may edit any character in their active group, not just
            // their own, so a broken/stranded character sheet can be corrected without needing
            // the player's own login.
            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            if (existingCharacter.OwnerId != currentUser.Id && role != GroupRole.Admin)
            {
                return Forbid();
            }

            // Validate class levels
            if (!await characterService.ValidateCharacterClassLevelsAsync(viewModel.Level,
                mapper.Map<List<CharacterClass>>(viewModel.Classes)))
            {
                ModelState.AddModelError(string.Empty,
                    "The sum of all class levels must equal the character's total level.");
            }

            if (!ModelState.IsValid)
            {
                viewModel.IsOwner = existingCharacter.OwnerId == currentUser.Id;
                return View(viewModel);
            }

            // Update the existing character properties manually instead of mapping
            existingCharacter.Name = viewModel.Name;
            existingCharacter.Level = viewModel.Level;
            existingCharacter.Status = viewModel.Status;
            existingCharacter.Role = viewModel.Role;
            existingCharacter.SheetLink = viewModel.SheetLink;
            existingCharacter.Description = viewModel.Description;
            existingCharacter.Backstory = viewModel.Backstory;
            
            // Handle profile picture upload - clear old picture first if new one is being uploaded
            if (viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0)
            {
                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedMimeTypes.Contains(viewModel.ProfilePictureFile.ContentType,
                    StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
                        "Only JPG, PNG, or GIF images are accepted.");
                    viewModel.IsOwner = existingCharacter.OwnerId == currentUser.Id;
                    return View(viewModel);
                }
                const long maxFileSizeBytes = 5 * 1024 * 1024;
                if (viewModel.ProfilePictureFile.Length > maxFileSizeBytes)
                {
                    ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
                        "Profile picture cannot exceed 5 MB.");
                    viewModel.IsOwner = existingCharacter.OwnerId == currentUser.Id;
                    return View(viewModel);
                }
                using var memoryStream = new MemoryStream();
                await viewModel.ProfilePictureFile.CopyToAsync(memoryStream, token);
                existingCharacter.ProfilePicture = memoryStream.ToArray();
            }
            // Otherwise, profile picture remains unchanged

            // Update classes
            existingCharacter.Classes = mapper.Map<List<CharacterClass>>(viewModel.Classes);

            // Persist all edited fields first so they are saved regardless of whether this
            // edit also promotes the character to Main below.
            await characterService.UpdateAsync(existingCharacter, token);

            // If setting as main, demote the character's owner's other characters to Backup.
            // Must use the character's own owner id (not the acting user's id) so an Admin
            // editing another player's character doesn't demote the Admin's own roster instead.
            if (viewModel.Role == CharacterRole.Main && existingCharacter.Role != CharacterRole.Main)
            {
                await characterService.SetAsMainCharacterAsync(id, existingCharacter.OwnerId, token);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken token = default)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(id, token);
            if (character == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            // An Admin (per-group role, or the global SuperAdmin role, both resolved by
            // GetEffectiveGroupRoleAsync) may delete any character in their active group, not
            // just their own, matching the same bypass granted to Edit.
            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            if (character.OwnerId != currentUser.Id && role != GroupRole.Admin)
            {
                return Forbid();
            }

            await characterService.RemoveAsync(character, token);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRetirement(int id, CancellationToken token = default)
        {
            var character = await characterService.GetCharacterWithDetailsAsync(id, token);
            if (character == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Forbid();
            }

            // An Admin (per-group role, or the global SuperAdmin role, both resolved by
            // GetEffectiveGroupRoleAsync) may retire/reactivate any character in their active
            // group, not just their own, matching the same bypass granted to Edit and Delete.
            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            if (character.OwnerId != currentUser.Id && role != GroupRole.Admin)
            {
                return Forbid();
            }

            if (character.Status == CharacterStatus.Dead)
            {
                return BadRequest("Dead characters cannot be retired or reactivated.");
            }

            character.Status = character.Status == CharacterStatus.Active
                ? CharacterStatus.Retired
                : CharacterStatus.Active;

            await characterService.UpdateAsync(character, token);

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> GetProfilePicture(int id, CancellationToken token = default)
        {
            var profilePicture = await characterService.GetCharacterProfilePictureAsync(id, token);
            if (profilePicture == null)
            {
                return NotFound();
            }

            return File(profilePicture, DetectImageMimeType(profilePicture));
        }

        private static string DetectImageMimeType(byte[] data) =>
            data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
            data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
            "image/jpeg";
    }
}
