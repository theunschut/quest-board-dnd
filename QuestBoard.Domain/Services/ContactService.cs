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
    public Task ReplaceContactTagsAsync(int contactId, IReadOnlyList<string> tagNames, CancellationToken token = default) =>
        repository.ReplaceContactTagsAsync(contactId, tagNames, token);

    /// <inheritdoc/>
    public IReadOnlyList<string> ParseTagNames(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return [];

        var names = new List<string>();
        foreach (var part in rawInput.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            // De-duplicate case-insensitively while preserving the first occurrence's casing
            // and the input order.
            if (names.Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
            names.Add(trimmed);
        }
        return names;
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Contact model, CancellationToken token = default)
    {
        // No caller-supplied signal (e.g. a not-yet-updated call site) defaults to the safe
        // preserve-crop behaviour, since model.ContactImageData is never null on a no-photo-change edit.
        await UpdateAsync(model, hasNewOriginalUpload: false, token);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Contact model, bool hasNewOriginalUpload, CancellationToken token = default) =>
        UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData: null, token);

    /// <inheritdoc/>
    public async Task UpdateAsync(Contact model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default)
    {
        // The image write and the rest of the entity's fields are saved together in a single
        // repository call so a failure in either half cannot leave the contact in a
        // half-updated state (new photo, stale metadata, or vice versa).
        byte[]? croppedImageData;
        if (newCroppedImageData != null)
        {
            // The caller submitted a genuinely new crop this request -- persist it directly.
            croppedImageData = newCroppedImageData;
        }
        else if (hasNewOriginalUpload)
        {
            // A genuinely new original arrived this request with no accompanying crop -- clear
            // any stale crop of the superseded photo, since it belonged to the photo that's
            // being replaced.
            croppedImageData = null;
        }
        else
        {
            // No new file; model.ContactImageData is the round-tripped existing original. Fetch
            // the currently-stored crop and pass it through unchanged so it survives an
            // unrelated-field edit.
            croppedImageData = await repository.GetContactCroppedImageAsync(model.Id, token);
        }

        // The original follows the same "don't trust the round-tripped value" rule as the
        // crop above: the read path that populates model.ContactImageData no longer loads the
        // original's bytes, so on a no-upload edit it must be re-fetched fresh here rather
        // than passed straight through -- otherwise an unrelated-field edit would wipe the
        // stored original image.
        var originalImageData = hasNewOriginalUpload
            ? model.ContactImageData
            : await repository.GetContactOriginalImageAsync(model.Id, token);

        await repository.UpdateWithProfileImageAsync(model, originalImageData, croppedImageData, token);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Contact model, byte[]? newCroppedImageData, CancellationToken token = default)
    {
        // The base Add call creates the contact and its profile image row (original only).
        await repository.AddAsync(model, token);

        // Only make a second write when the caller actually submitted a crop this request --
        // otherwise the freshly-created row is left exactly as the base Add produced it.
        if (newCroppedImageData != null)
        {
            await repository.UpdateWithProfileImageAsync(model, model.ContactImageData, newCroppedImageData, token);
        }
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
