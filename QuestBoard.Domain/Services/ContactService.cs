using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class ContactService(IContactRepository repository, IMapper mapper) : BaseService<Contact>(repository, mapper), IContactService
{
    /// <inheritdoc/>
    public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
    {
        return await repository.GetAllContactsWithDetailsAsync(token);
    }

    /// <inheritdoc/>
    public async Task<Contact?> GetContactWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetContactWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Contact model, CancellationToken token = default)
    {
        // No caller-supplied signal (e.g. a not-yet-updated call site) defaults to the safe
        // preserve-crop behaviour, since model.ContactImageData is never null on a no-photo-change edit.
        await UpdateAsync(model, hasNewOriginalUpload: false, token);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Contact model, bool hasNewOriginalUpload, CancellationToken token = default)
    {
        // The image write and the rest of the entity's fields are saved together in a single
        // repository call so a failure in either half cannot leave the contact in a
        // half-updated state (new photo, stale metadata, or vice versa).
        byte[]? croppedImageData;
        if (hasNewOriginalUpload)
        {
            // A genuinely new original arrived this request -- clear any stale crop of the
            // superseded photo, since it belonged to the photo that's being replaced.
            croppedImageData = null;
        }
        else
        {
            // No new file; model.ContactImageData is the round-tripped existing original. Fetch
            // the currently-stored crop and pass it through unchanged so it survives an
            // unrelated-field edit.
            croppedImageData = await repository.GetContactCroppedImageAsync(model.Id, token);
        }

        await repository.UpdateWithProfileImageAsync(model, model.ContactImageData, croppedImageData, token);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetContactOriginalImageAsync(int id, CancellationToken token = default)
    {
        return repository.GetContactOriginalImageAsync(id, token);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetContactCroppedImageAsync(int id, CancellationToken token = default)
    {
        return repository.GetContactCroppedImageAsync(id, token);
    }

    /// <inheritdoc/>
    public Task AddNoteAsync(ContactNote note, CancellationToken token = default)
    {
        return repository.AddNoteAsync(note, token);
    }

    /// <inheritdoc/>
    public Task UpdateNoteAsync(ContactNote note, CancellationToken token = default)
    {
        return repository.UpdateNoteAsync(note, token);
    }

    /// <inheritdoc/>
    public Task DeleteNoteAsync(int noteId, CancellationToken token = default)
    {
        return repository.DeleteNoteAsync(noteId, token);
    }
}
