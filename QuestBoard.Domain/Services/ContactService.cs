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
        if (hasNewOriginalUpload)
        {
            // A genuinely new original arrived this request -- clear any stale crop of the
            // superseded photo (Pitfall 5).
            await repository.UpdateProfileImageAsync(model.Id, model.ContactImageData, croppedImageData: null, token);
        }
        else
        {
            // No new file; model.ContactImageData is the round-tripped existing original. Fetch
            // the currently-stored crop and pass it through unchanged so it survives an
            // unrelated-field edit (Pitfall 4).
            var existingCropped = await repository.GetContactCroppedImageAsync(model.Id, token);
            await repository.UpdateProfileImageAsync(model.Id, model.ContactImageData, existingCropped, token);
        }

        await repository.UpdateAsync(model, token);
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
