using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IContactService : IBaseService<Contact>
{
    /// <summary>
    /// Returns all contacts in the active group with creator, profile image, and notes loaded,
    /// ordered alphabetically by name. Notes are ordered newest first.
    /// </summary>
    Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a single contact with creator, profile image, and notes loaded (notes ordered
    /// newest first), or null if not found or outside the active group.
    /// </summary>
    Task<Contact?> GetContactWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns the raw profile image bytes for a contact, or null if none is set.
    /// </summary>
    Task<byte[]?> GetContactImageAsync(int id, CancellationToken token = default);

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
