using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IContactService : IBaseService<Contact>
{
    /// <summary>
    /// Returns all contacts in the active group with creator, profile image, notes, and tags
    /// loaded, ordered alphabetically by name. Notes are ordered newest first; tags are ordered
    /// alphabetically by name.
    /// </summary>
    Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a single contact with creator, profile image, notes, and tags loaded (notes
    /// ordered newest first, tags ordered alphabetically by name), or null if not found or
    /// outside the active group.
    /// </summary>
    Task<Contact?> GetContactWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Reconciles a contact's tag list against the submitted set of names: a name matching an
    /// existing board tag (case-insensitively) reuses that row rather than creating a second
    /// one; a name with no match creates a new tag row on the contact's own board. Any tag left
    /// with no contacts afterward is deleted in the same save. A contact id outside the active
    /// group is a silent no-op -- nothing is created and nothing is thrown.
    /// </summary>
    Task ReplaceContactTagsAsync(int contactId, IReadOnlyList<string> tagNames, CancellationToken token = default);

    /// <summary>
    /// Splits a raw comma-separated string into tag names: trims each part, drops empty parts,
    /// and de-duplicates case-insensitively while preserving the first occurrence's casing and
    /// the input order. Returns an empty list for null or whitespace-only input.
    /// </summary>
    IReadOnlyList<string> ParseTagNames(string? rawInput);

    /// <summary>
    /// Returns the contact's original (unmodified) profile image bytes, or null if none is set.
    /// </summary>
    Task<byte[]?> GetContactOriginalImageAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved. Null if neither set.
    /// </summary>
    Task<byte[]?> GetContactCroppedImageAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Updates a contact, threading an explicit signal for whether a genuinely new original
    /// image was uploaded this request. hasNewOriginalUpload == true clears any stale cropped
    /// image (a new original supersedes the old crop); false preserves the existing crop
    /// unchanged, since model.ContactImageData is never null on a no-photo-change edit.
    /// </summary>
    Task UpdateAsync(Contact model, bool hasNewOriginalUpload, CancellationToken token = default);

    /// <summary>
    /// Updates a contact, threading both the new-original-upload signal and a caller-supplied
    /// cropped image. A non-null newCroppedImageData is persisted directly; when null, falls back
    /// to the same hasNewOriginalUpload-driven clear-or-preserve resolution as the 3-arg overload.
    /// </summary>
    Task UpdateAsync(Contact model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default);

    /// <summary>
    /// Creates a contact, then persists a caller-supplied cropped image immediately after,
    /// so a crop chosen at creation time isn't silently dropped. A null newCroppedImageData
    /// behaves identically to the plain AddAsync.
    /// </summary>
    Task AddAsync(Contact model, byte[]? newCroppedImageData, CancellationToken token = default);

    /// <summary>
    /// Adds a new note to a contact and propagates the DB-generated Id back onto the model.
    /// </summary>
    Task AddNoteAsync(ContactNote note, CancellationToken token = default);

    /// <summary>
    /// Updates an existing note's text and stamps its UpdatedAt.
    /// </summary>
    Task UpdateNoteAsync(ContactNote note, CancellationToken token = default);

    /// <summary>
    /// Deletes the note with the given Id, if it exists.
    /// </summary>
    Task DeleteNoteAsync(int noteId, CancellationToken token = default);
}
