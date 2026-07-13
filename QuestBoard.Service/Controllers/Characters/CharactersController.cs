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
    public class CharactersController(
        ICharacterService characterService,
        IUserService userService,
        IActiveGroupContext activeGroupContext,
        IImageValidationService imageValidationService,
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
            byte[]? croppedImageData = null;
            byte[]? uploadedOriginalImageData = null;
            var newProfilePictureFile = viewModel.ProfilePictureFile;
            if (newProfilePictureFile != null && newProfilePictureFile.Length > 0)
            {
                var original = new ImageFileInput(newProfilePictureFile.Length, newProfilePictureFile.ContentType,
                    newProfilePictureFile.FileName, nameof(viewModel.ProfilePictureFile));

                ImageFileInput? cropped = null;
                if (viewModel.CroppedPictureFile is { Length: > 0 } croppedFile)
                {
                    cropped = new ImageFileInput(croppedFile.Length, croppedFile.ContentType,
                        croppedFile.FileName, nameof(viewModel.CroppedPictureFile));
                }

                var validationErrors = imageValidationService.ValidateImagePair(original, cropped);
                foreach (var error in validationErrors)
                {
                    ModelState.AddModelError(error.FieldName, error.Message);
                }
                if (!ModelState.IsValid)
                {
                    return View(viewModel);
                }

                using var memoryStream = new MemoryStream();
                await newProfilePictureFile.CopyToAsync(memoryStream, token);
                uploadedOriginalImageData = memoryStream.ToArray();

                if (viewModel.CroppedPictureFile is { Length: > 0 } submittedCrop)
                {
                    using var croppedMemoryStream = new MemoryStream();
                    await submittedCrop.CopyToAsync(croppedMemoryStream, token);
                    croppedImageData = croppedMemoryStream.ToArray();
                }
            }

            var character = mapper.Map<Character>(viewModel);
            character.ProfilePicture = uploadedOriginalImageData;

            // Tag the character to the active group so the character-roster scoping applies
            // (CharacterEntity is scoped by a global query filter on GroupId).
            character.GroupId = activeGroupContext.RequireActiveGroupId();

            await characterService.AddAsync(character, croppedImageData, token);

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
            if (!await CanManageCharacterAsync(currentUser, character))
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
            if (!await CanManageCharacterAsync(currentUser, existingCharacter))
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

            // Capture the character's role before it gets overwritten below, so the
            // Main-promotion check further down compares against the pre-edit state
            // instead of the just-assigned value (which would always match).
            var wasMain = existingCharacter.Role == CharacterRole.Main;

            // Update the existing character properties manually instead of mapping
            existingCharacter.Name = viewModel.Name;
            existingCharacter.Level = viewModel.Level;
            existingCharacter.Status = viewModel.Status;
            existingCharacter.Role = viewModel.Role;
            existingCharacter.SheetLink = viewModel.SheetLink;
            existingCharacter.Description = viewModel.Description;
            existingCharacter.Backstory = viewModel.Backstory;

            // A genuinely new original photo was uploaded this request. Hoisted into a single
            // local reused both to gate the byte-copy below and to signal the service, so the
            // two checks can never drift apart.
            var hasNewOriginalUpload = viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0;

            // The crop is read whenever it's submitted, independent of hasNewOriginalUpload, so a
            // crop-only re-save (re-cropping the stored original without re-uploading it) isn't
            // silently dropped -- CharacterService.UpdateAsync already handles newCroppedImageData
            // independently of hasNewOriginalUpload.
            ImageFileInput? original = hasNewOriginalUpload
                ? new ImageFileInput(viewModel.ProfilePictureFile!.Length, viewModel.ProfilePictureFile.ContentType,
                    viewModel.ProfilePictureFile.FileName, nameof(viewModel.ProfilePictureFile))
                : null;

            ImageFileInput? cropped = null;
            if (viewModel.CroppedPictureFile is { Length: > 0 } croppedFile)
            {
                cropped = new ImageFileInput(croppedFile.Length, croppedFile.ContentType,
                    croppedFile.FileName, nameof(viewModel.CroppedPictureFile));
            }

            byte[]? newCroppedImageData = null;
            if (original != null || cropped != null)
            {
                var validationErrors = imageValidationService.ValidateImagePair(original, cropped);
                foreach (var error in validationErrors)
                {
                    ModelState.AddModelError(error.FieldName, error.Message);
                }
                if (!ModelState.IsValid)
                {
                    viewModel.IsOwner = existingCharacter.OwnerId == currentUser.Id;
                    return View(viewModel);
                }
            }

            if (hasNewOriginalUpload)
            {
                using var memoryStream = new MemoryStream();
                await viewModel.ProfilePictureFile!.CopyToAsync(memoryStream, token);
                existingCharacter.ProfilePicture = memoryStream.ToArray();
            }
            // Otherwise, profile picture remains unchanged

            if (cropped != null)
            {
                using var croppedStream = new MemoryStream();
                await viewModel.CroppedPictureFile!.CopyToAsync(croppedStream, token);
                newCroppedImageData = croppedStream.ToArray();
            }

            // Update classes
            existingCharacter.Classes = mapper.Map<List<CharacterClass>>(viewModel.Classes);

            // Persist all edited fields first so they are saved regardless of whether this
            // edit also promotes the character to Main below. Passing hasNewOriginalUpload lets
            // the service clear any stale cropped image when a genuinely new original arrives,
            // while preserving it on an edit that doesn't touch the photo. newCroppedImageData
            // carries a real submitted crop through so it persists instead of being cleared.
            await characterService.UpdateAsync(existingCharacter, hasNewOriginalUpload, newCroppedImageData, token);

            // If setting as main, demote the character's owner's other characters to Backup.
            // Must use the character's own owner id (not the acting user's id) so an Admin
            // editing another player's character doesn't demote the Admin's own roster instead.
            if (viewModel.Role == CharacterRole.Main && !wasMain)
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
            if (!await CanManageCharacterAsync(currentUser, character))
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
            if (!await CanManageCharacterAsync(currentUser, character))
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
            var profilePicture = await characterService.GetCharacterOriginalPictureAsync(id, token);
            if (profilePicture == null)
            {
                return NotFound();
            }

            return File(profilePicture, DetectImageMimeType(profilePicture));
        }

        [HttpGet]
        public async Task<IActionResult> GetCroppedPicture(int id, CancellationToken token = default)
        {
            var croppedPicture = await characterService.GetCharacterCroppedPictureAsync(id, token);
            if (croppedPicture == null)
            {
                return NotFound();
            }

            return File(croppedPicture, DetectImageMimeType(croppedPicture));
        }

        private static string DetectImageMimeType(byte[] data) =>
            data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
            data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
            "image/jpeg";

        // Shared owner-or-admin guard used by Edit, Delete, and ToggleRetirement so the
        // authorization rule only needs to be updated in one place.
        private async Task<bool> CanManageCharacterAsync(User currentUser, Character character)
        {
            if (character.OwnerId == currentUser.Id)
            {
                return true;
            }

            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            return role == GroupRole.Admin;
        }
    }
}
