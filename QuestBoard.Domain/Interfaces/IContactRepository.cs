using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IContactRepository : IBaseRepository<Contact>
{
    /// <summary>
    /// Returns all contacts in the active group with creator, profile image, and notes loaded,
    /// ordered alphabetically by name. Notes are ordered newest first. Group scoping is enforced
    /// by the entity's query filter, not by a parameter on this method.
    /// </summary>
    Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a single contact with creator, profile image, and notes loaded (notes ordered
    /// newest first), or null if not found or outside the active group.
    /// </summary>
    Task<Contact?> GetContactWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the contact's original (unmodified) profile image bytes, or null if none is set or
    /// the contact is outside the active group.
    /// </summary>
    Task<byte[]?> GetContactOriginalImageAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the cropped/display image, falling back to the original when no crop was ever saved.
    /// Null if neither set or the contact is outside the active group.
    /// </summary>
    Task<byte[]?> GetContactCroppedImageAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Atomically sets, replaces, or clears (when originalImageData is null) both the original and
    /// cropped profile image columns. A null croppedImageData with a non-null originalImageData
    /// writes NULL to the cropped column, clearing any stale crop from a prior upload.
    /// </summary>
    Task UpdateProfileImageAsync(int contactId, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);

    /// <summary>
    /// Updates a contact's scalar fields and its profile image in a single save, so a failure in
    /// either half (e.g. a concurrency conflict on the entity update) cannot leave the image durably
    /// committed while the rest of the contact's fields are left stale, or vice versa.
    /// </summary>
    Task UpdateWithProfileImageAsync(Contact model, byte[]? originalImageData, byte[]? croppedImageData, CancellationToken token = default);

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
