using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.ContactViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Contacts
{
    [Authorize]
    public class ContactsController(
        IContactService contactService,
        IContactCategoryService contactCategoryService,
        IUserService userService,
        IActiveGroupContext activeGroupContext,
        IImageValidationService imageValidationService,
        IMapper mapper) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser.Id == 0)
            {
                return Challenge();
            }

            var viewerIsDmTier = await IsDmTierAsync();
            var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

            var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
            var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden)).ToList();

            var contactViewModels = mapper.Map<List<ContactViewModel>>(visibleContacts);
            foreach (var vm in contactViewModels)
            {
                vm.CanManage = viewerIsDmTier;
            }

            // Whether the board has ever created a category, not whether any group turns out
            // non-empty for this viewer -- that distinction keeps a board that never adopted the
            // feature rendering exactly as it does today, while a board that has categories but
            // nothing visible right now still renders as a grouped page rather than reverting.
            var categories = await contactCategoryService.GetOrderedAsync(token);
            var hasCategories = categories.Any();

            // Grouping runs over contactViewModels, which was built from the already-filtered
            // visibleContacts above -- a group here can never disclose a contact the viewer
            // cannot see, and a category with nothing visible simply produces no group at all.
            var categoryGroups = hasCategories
                ? contactViewModels
                    .GroupBy(vm => (vm.CategoryId, vm.CategoryName, vm.CategorySortOrder))
                    .OrderBy(g => g.Key.CategoryId is null)
                    .ThenBy(g => g.Key.CategorySortOrder)
                    .ThenBy(g => g.Key.CategoryId)
                    .Select(g => new ContactCategoryGroupViewModel
                    {
                        Title = g.Key.CategoryId is null ? "Ungrouped" : g.Key.CategoryName ?? string.Empty,
                        IsUngrouped = g.Key.CategoryId is null,
                        Contacts = g.OrderBy(c => c.Name).ToList()
                    })
                    .ToList()
                : [];

            var viewModel = new ContactsIndexViewModel
            {
                Contacts = contactViewModels,
                CategoryGroups = categoryGroups,
                HasCategories = hasCategories,
                ShowHidden = includeHidden,
                ViewerIsDmTier = viewerIsDmTier
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            var viewerIsDmTier = currentUser.Id != 0 && await IsDmTierAsync();
            var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

            if (!IsVisibleTo(contact, currentUser.Id, includeHidden))
            {
                return NotFound();
            }

            var viewModel = mapper.Map<ContactViewModel>(contact);
            viewModel.CanManage = viewerIsDmTier;

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Policy = "DungeonMasterOnly")]
        public IActionResult Create()
        {
            var viewModel = new ContactViewModel();

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Policy = "DungeonMasterOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactViewModel viewModel, CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser.Id == 0)
            {
                return Challenge();
            }

            // A SuperAdmin has no active group by design, so there is no board to stamp onto
            // the new contact. Send them to pick one rather than letting the write throw.
            if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
            {
                return RedirectToAction("Index", "GroupPicker");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            byte[]? croppedImageData = null;
            byte[]? uploadedOriginalImageData = null;
            var newContactImageFile = viewModel.ContactImageFile;
            if (newContactImageFile != null && newContactImageFile.Length > 0)
            {
                var original = new ImageFileInput(newContactImageFile.Length, newContactImageFile.ContentType,
                    newContactImageFile.FileName, nameof(viewModel.ContactImageFile));

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
                await newContactImageFile.CopyToAsync(memoryStream, token);
                uploadedOriginalImageData = memoryStream.ToArray();

                if (viewModel.CroppedPictureFile is { Length: > 0 } submittedCrop)
                {
                    using var croppedMemoryStream = new MemoryStream();
                    await submittedCrop.CopyToAsync(croppedMemoryStream, token);
                    croppedImageData = croppedMemoryStream.ToArray();
                }
            }

            var contact = mapper.Map<Contact>(viewModel);
            contact.ContactImageData = uploadedOriginalImageData;

            // Tag the contact to the active group so the group-scoped roster query filter
            // applies (ContactEntity is scoped by a global query filter on GroupId).
            contact.GroupId = activeGroupId;
            contact.CreatedByUserId = currentUser.Id;
            contact.IsRevealed = false;

            await contactService.AddAsync(contact, croppedImageData, token);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "DungeonMasterOnly")]
        public async Task<IActionResult> Edit(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            var viewModel = mapper.Map<ContactViewModel>(contact);
            viewModel.CanManage = true;

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Policy = "DungeonMasterOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ContactViewModel viewModel, CancellationToken token = default)
        {
            if (id != viewModel.Id)
            {
                return BadRequest();
            }

            var existingContact = await contactService.GetContactWithDetailsAsync(id, token);
            if (existingContact == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                viewModel.CanManage = true;
                return View(viewModel);
            }

            // Update the existing contact's core fields only — no notes editing here (notes
            // stay on the Details page).
            existingContact.Name = viewModel.Name;
            existingContact.Description = viewModel.Description;
            existingContact.TownCity = viewModel.TownCity;
            existingContact.SubLocation = viewModel.SubLocation;

            // A genuinely new original photo was uploaded this request. Hoisted into a single
            // local reused both to gate the byte-copy below and to signal the service, so the
            // two checks can never drift apart.
            var hasNewOriginalUpload = viewModel.ContactImageFile != null && viewModel.ContactImageFile.Length > 0;

            // The crop is read whenever it's submitted, independent of hasNewOriginalUpload, so a
            // crop-only re-save (re-cropping the stored original without re-uploading it) isn't
            // silently dropped -- ContactService.UpdateAsync already handles newCroppedImageData
            // independently of hasNewOriginalUpload.
            ImageFileInput? original = hasNewOriginalUpload
                ? new ImageFileInput(viewModel.ContactImageFile!.Length, viewModel.ContactImageFile.ContentType,
                    viewModel.ContactImageFile.FileName, nameof(viewModel.ContactImageFile))
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
                    viewModel.CanManage = true;
                    return View(viewModel);
                }
            }

            if (hasNewOriginalUpload)
            {
                using var memoryStream = new MemoryStream();
                await viewModel.ContactImageFile!.CopyToAsync(memoryStream, token);
                existingContact.ContactImageData = memoryStream.ToArray();
            }
            // Otherwise, the contact image remains unchanged.

            if (cropped != null)
            {
                using var croppedStream = new MemoryStream();
                await viewModel.CroppedPictureFile!.CopyToAsync(croppedStream, token);
                newCroppedImageData = croppedStream.ToArray();
            }

            // Passing hasNewOriginalUpload lets the service clear any stale cropped image when a
            // genuinely new original arrives, while preserving it on an edit that doesn't touch
            // the photo. newCroppedImageData carries a real submitted crop through so it persists
            // instead of being cleared.
            await contactService.UpdateAsync(existingContact, hasNewOriginalUpload, newCroppedImageData, token);

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Policy = "DungeonMasterOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            await contactService.RemoveAsync(contact, token);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = "DungeonMasterOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReveal(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            contact.IsRevealed = !contact.IsRevealed;

            await contactService.UpdateAsync(contact, token);

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Policy = "DungeonMasterOnly")]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleShowHidden()
        {
            // A SuperAdmin has no active group by design, so there is no per-board toggle to
            // flip. Send them to pick one rather than letting the write throw.
            if (activeGroupContext.ActiveGroupId is not { } groupId)
            {
                return RedirectToAction("Index", "GroupPicker");
            }

            var key = SessionKeys.ShowHiddenContactsKey(groupId);
            var current = HttpContext.Session.GetInt32(key) == 1;

            HttpContext.Session.SetInt32(key, current ? 0 : 1);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int contactId, ContactNoteViewModel viewModel, CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser.Id == 0)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Note text is required and cannot exceed 2000 characters.";
                return RedirectToAction(nameof(Details), new { id = contactId });
            }

            var note = new ContactNote
            {
                ContactId = contactId,
                Text = viewModel.Text,
                AuthorUserId = currentUser.Id
            };

            await contactService.AddNoteAsync(note, token);

            return RedirectToAction(nameof(Details), new { id = contactId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNote(int id, int contactId, ContactNoteViewModel viewModel, CancellationToken token = default)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Note text is required and cannot exceed 2000 characters.";
                return RedirectToAction(nameof(Details), new { id = contactId });
            }

            var note = new ContactNote
            {
                Id = id,
                ContactId = contactId,
                Text = viewModel.Text
            };

            await contactService.UpdateNoteAsync(note, token);

            return RedirectToAction(nameof(Details), new { id = contactId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int id, int contactId, CancellationToken token = default)
        {
            await contactService.DeleteNoteAsync(id, token);

            return RedirectToAction(nameof(Details), new { id = contactId });
        }

        [HttpGet]
        public async Task<IActionResult> GetContactImage(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            var viewerIsDmTier = currentUser.Id != 0 && await IsDmTierAsync();
            var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

            if (!IsVisibleTo(contact, currentUser.Id, includeHidden))
            {
                return NotFound();
            }

            var image = await contactService.GetContactOriginalImageAsync(id, token);
            if (image == null)
            {
                return NotFound();
            }

            return File(image, DetectImageMimeType(image));
        }

        [HttpGet]
        public async Task<IActionResult> GetCroppedContactImage(int id, CancellationToken token = default)
        {
            var contact = await contactService.GetContactWithDetailsAsync(id, token);
            if (contact == null)
            {
                return NotFound();
            }

            var currentUser = await userService.GetUserAsync(User);
            var viewerIsDmTier = currentUser.Id != 0 && await IsDmTierAsync();
            var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

            if (!IsVisibleTo(contact, currentUser.Id, includeHidden))
            {
                return NotFound();
            }

            var image = await contactService.GetContactCroppedImageAsync(id, token);
            if (image == null)
            {
                return NotFound();
            }

            return File(image, DetectImageMimeType(image));
        }

        private static string DetectImageMimeType(byte[] data) =>
            data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
            data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
            "image/jpeg";

        // The DungeonMasterOnly policy attribute is the security boundary for
        // Create/Edit/Delete/ToggleReveal/ToggleShowHidden. This helper is used only to compute
        // a display-only flag (CanManage / toggle visibility) for views — it deliberately
        // resolves the same way GetEffectiveGroupRoleAsync does, but never gates an action.
        private async Task<bool> IsDmTierAsync()
        {
            var role = await GetEffectiveRoleAsync();
            return role == GroupRole.Admin || role == GroupRole.DungeonMaster;
        }

        // SuperAdmin has no active group by design, so short-circuit to Admin here rather than
        // calling RequireActiveGroupId(), which would throw for a SuperAdmin with no active group.
        private async Task<GroupRole?> GetEffectiveRoleAsync() =>
            User.IsInRole("SuperAdmin")
                ? GroupRole.Admin
                : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());

        // No active group means there is nothing to key the hidden-contacts toggle by. Failing
        // closed (never showing hidden contacts) is safe here since this only relaxes visibility
        // when true — it never widens what a SuperAdmin without a selected board can see.
        private bool ReadShowHiddenToggle()
        {
            if (activeGroupContext.ActiveGroupId is not { } groupId)
            {
                return false;
            }

            return HttpContext.Session.GetInt32(SessionKeys.ShowHiddenContactsKey(groupId)) == 1;
        }

        // Three-branch visibility check: the creator always sees their own hidden
        // Contact; a revealed Contact is visible to everyone; a DM-tier viewer with the Show
        // Hidden toggle on sees all hidden Contacts too. Plain Players never see hidden Contacts.
        private static bool IsVisibleTo(Contact contact, int currentUserId, bool includeHidden)
        {
            if (contact.IsRevealed)
            {
                return true;
            }

            if (currentUserId != 0 && contact.CreatedByUserId == currentUserId)
            {
                return true;
            }

            return includeHidden;
        }
    }
}
