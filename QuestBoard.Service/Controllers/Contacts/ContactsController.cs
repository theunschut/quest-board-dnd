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
        IUserService userService,
        IActiveGroupContext activeGroupContext,
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

            var viewModel = new ContactsIndexViewModel
            {
                Contacts = contactViewModels,
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

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            if (viewModel.ContactImageFile != null && viewModel.ContactImageFile.Length > 0)
            {
                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedMimeTypes.Contains(viewModel.ContactImageFile.ContentType,
                    StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(viewModel.ContactImageFile),
                        "Only JPG, PNG, or GIF images are accepted.");
                    return View(viewModel);
                }
                const long maxFileSizeBytes = 5 * 1024 * 1024;
                if (viewModel.ContactImageFile.Length > maxFileSizeBytes)
                {
                    ModelState.AddModelError(nameof(viewModel.ContactImageFile),
                        "Image cannot exceed 5 MB.");
                    return View(viewModel);
                }
                using var memoryStream = new MemoryStream();
                await viewModel.ContactImageFile.CopyToAsync(memoryStream, token);
                viewModel.ContactImage = memoryStream.ToArray();
            }

            var contact = mapper.Map<Contact>(viewModel);

            // Tag the contact to the active group so the group-scoped roster query filter
            // applies (ContactEntity is scoped by a global query filter on GroupId).
            contact.GroupId = activeGroupContext.RequireActiveGroupId();
            contact.CreatedByUserId = currentUser.Id;
            contact.IsRevealed = false;

            await contactService.AddAsync(contact, token);

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

            if (viewModel.ContactImageFile != null && viewModel.ContactImageFile.Length > 0)
            {
                var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedMimeTypes.Contains(viewModel.ContactImageFile.ContentType,
                    StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(viewModel.ContactImageFile),
                        "Only JPG, PNG, or GIF images are accepted.");
                    viewModel.CanManage = true;
                    return View(viewModel);
                }
                const long maxFileSizeBytes = 5 * 1024 * 1024;
                if (viewModel.ContactImageFile.Length > maxFileSizeBytes)
                {
                    ModelState.AddModelError(nameof(viewModel.ContactImageFile),
                        "Image cannot exceed 5 MB.");
                    viewModel.CanManage = true;
                    return View(viewModel);
                }
                using var memoryStream = new MemoryStream();
                await viewModel.ContactImageFile.CopyToAsync(memoryStream, token);
                existingContact.ContactImageData = memoryStream.ToArray();
            }
            // Otherwise, the contact image remains unchanged.

            await contactService.UpdateAsync(existingContact, token);

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
            var groupId = activeGroupContext.RequireActiveGroupId();
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

            var image = await contactService.GetContactImageAsync(id, token);
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
            var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
            return role == GroupRole.Admin || role == GroupRole.DungeonMaster;
        }

        private bool ReadShowHiddenToggle()
        {
            var groupId = activeGroupContext.RequireActiveGroupId();
            return HttpContext.Session.GetInt32(SessionKeys.ShowHiddenContactsKey(groupId)) == 1;
        }

        // Three-branch visibility check (D-13/D-15): the creator always sees their own hidden
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
