using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.ContactViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

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
        public async Task<IActionResult> Index(IList<int>? tag = null, CancellationToken token = default)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser.Id == 0)
            {
                return Challenge();
            }

            var viewerIsDmTier = await IsDmTierAsync();
            var includeHidden = viewerIsDmTier && ReadShowHiddenToggle();

            // Gated on the same DM-tier flag that already drives CanManage, rather than a
            // second check that could drift from it -- a player supplying a tag id in the
            // query string gets exactly the list they would have gotten without it.
            var selectedTagIds = viewerIsDmTier ? tag ?? [] : [];

            var allContacts = await contactService.GetAllContactsWithDetailsAsync(token);
            var visibleContacts = allContacts.Where(c => IsVisibleTo(c, currentUser.Id, includeHidden)).ToList();

            // The vocabulary is a projection of the visible-but-unfiltered set -- take it from
            // the filtered set instead and ticking one tag would make every other tag vanish,
            // so a second one could never be added.
            var availableTags = viewerIsDmTier
                ? mapper.Map<List<ContactTagViewModel>>(BuildTagVocabulary(visibleContacts))
                : [];

            // The filter runs after the visibility gate and never inside the query, so it can
            // only narrow what the viewer could already see -- applying it upstream would be
            // the exact way a filter turns into a way of surfacing something hidden.
            var filteredContacts = ApplyTagFilter(visibleContacts, selectedTagIds);

            var contactViewModels = mapper.Map<List<ContactViewModel>>(filteredContacts);
            foreach (var vm in contactViewModels)
            {
                vm.CanManage = viewerIsDmTier;
                if (!viewerIsDmTier)
                {
                    vm.Tags = [];
                }
            }

            // Whether the board has ever created a category, not whether any group turns out
            // non-empty for this viewer -- that distinction keeps a board that never adopted the
            // feature rendering exactly as it does today, while a board that has categories but
            // nothing visible right now still renders as a grouped page rather than reverting.
            var categories = await contactCategoryService.GetOrderedAsync(token);
            var hasCategories = categories.Any();

            // Grouping runs over contactViewModels, which was built from filteredContacts above
            // -- the tag filter has already run by this point, so a group here can never
            // disclose a contact the viewer cannot see or one the filter excluded, and a
            // category with nothing left simply produces no group at all.
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
                ViewerIsDmTier = viewerIsDmTier,
                SelectedTagIds = selectedTagIds,
                AvailableTags = availableTags
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
            if (!viewerIsDmTier)
            {
                viewModel.Tags = [];
            }

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Policy = "DungeonMasterOnly")]
        public async Task<IActionResult> Create(CancellationToken token = default)
        {
            var viewModel = new ContactViewModel();
            await PopulateCategoryOptionsAsync(viewModel, token);

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
                await PopulateCategoryOptionsAsync(viewModel, token);
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
                    await PopulateCategoryOptionsAsync(viewModel, token);
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

            // The posted category id is a raw integer under the caller's control -- resolve it
            // through the board-filtered service before it ever touches the mapped contact, so a
            // foreign board's category can never ride along into a write on this board.
            if (!await IsCategoryAcceptableAsync(viewModel.CategoryId, token))
            {
                ModelState.AddModelError(nameof(viewModel.CategoryId), "Selected category is not available on this board.");
                await PopulateCategoryOptionsAsync(viewModel, token);
                return View(viewModel);
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
            await PopulateCategoryOptionsAsync(viewModel, token);

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
                await PopulateCategoryOptionsAsync(viewModel, token);
                return View(viewModel);
            }

            // The posted category id is a raw integer under the caller's control -- resolve it
            // through the board-filtered service before it ever reaches the loaded contact, so a
            // foreign board's category can never overwrite the stored reference.
            if (!await IsCategoryAcceptableAsync(viewModel.CategoryId, token))
            {
                ModelState.AddModelError(nameof(viewModel.CategoryId), "Selected category is not available on this board.");
                viewModel.CanManage = true;
                await PopulateCategoryOptionsAsync(viewModel, token);
                return View(viewModel);
            }

            // Update the existing contact's core fields only — no notes editing here (notes
            // stay on the Details page).
            existingContact.Name = viewModel.Name;
            existingContact.Description = viewModel.Description;
            existingContact.TownCity = viewModel.TownCity;
            existingContact.SubLocation = viewModel.SubLocation;
            existingContact.CategoryId = viewModel.CategoryId;

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
                    await PopulateCategoryOptionsAsync(viewModel, token);
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
        public IActionResult ToggleShowHidden(IList<int>? tag = null)
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

            // Preserve the selection across the redirect as repeated tag query parameters, so
            // the shape matches what Index binds. Passing the collection as a single anonymous
            // route value would stringify it to its type name instead of expanding it, so the
            // query string is composed by hand from the application's own action url plus
            // integer ids only -- always local, never a user-supplied string.
            var selectedTagIds = tag ?? [];
            var indexUrl = Url.Action(nameof(Index))!;
            var redirectUrl = selectedTagIds.Count == 0
                ? indexUrl
                : QueryHelpers.AddQueryString(
                    indexUrl,
                    selectedTagIds.Select(id => new KeyValuePair<string, string?>("tag", id.ToString())));

            return Redirect(redirectUrl);
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

        // Reads the board's ordered categories and projects them straight into select list
        // items, preserving the service's own sort-position-then-id order. Never re-sorted here
        // -- the dropdown must present the same vocabulary in the same order the DM already
        // recognises from the index headings, not a second, alphabetical one.
        private async Task PopulateCategoryOptionsAsync(ContactViewModel viewModel, CancellationToken token)
        {
            var categories = await contactCategoryService.GetOrderedAsync(token);
            viewModel.CategoryOptions = categories
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToList();
            viewModel.HasCategories = categories.Count > 0;
        }

        // A null id is always acceptable. Any other id is acceptable only when a board-filtered
        // read by that id resolves -- the category service's own query filter already confines
        // the read to the active board, so a category owned by another board simply does not
        // resolve and is indistinguishable from a nonexistent one.
        private async Task<bool> IsCategoryAcceptableAsync(int? categoryId, CancellationToken token)
        {
            if (categoryId is null)
            {
                return true;
            }

            var category = await contactCategoryService.GetByIdAsync(categoryId.Value, token);
            return category != null;
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

        // Flattens every visible contact's tags, de-duplicates by id, and orders by name with a
        // case-insensitive comparer so near-duplicates sit next to each other and a DM can spot
        // them. Takes only the visible-but-unfiltered set as its input, which is what makes it
        // structurally impossible for a tag borne solely by contacts this viewer cannot see to
        // reach the browser -- there is no separate vocabulary query to get wrong.
        private static IList<ContactTag> BuildTagVocabulary(IEnumerable<Contact> visibleContacts) =>
            visibleContacts
                .SelectMany(c => c.Tags)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Returns the input unchanged when no ids are selected; otherwise returns the contacts
        // carrying at least one selected tag id. Union semantics, so ticking more boxes widens
        // the result. A selection that matches nothing at all -- every id unknown, already
        // pruned, or belonging to another board -- falls back to the full visible list rather
        // than an empty page: it is never treated as an error, and an error response for it
        // would itself confirm that an id in that range exists somewhere.
        private static IList<Contact> ApplyTagFilter(IList<Contact> visibleContacts, IList<int> selectedTagIds)
        {
            if (selectedTagIds.Count == 0)
            {
                return visibleContacts;
            }

            var matched = visibleContacts
                .Where(c => c.Tags.Any(t => selectedTagIds.Contains(t.Id)))
                .ToList();

            return matched.Count == 0 ? visibleContacts : matched;
        }
    }
}
