using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class ContactRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Contact, ContactEntity>(dbContext, mapper), IContactRepository
{
    /// <inheritdoc/>
    public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
    {
        // Group scoping is enforced entirely by ContactEntity's fail-closed query filter here --
        // no manual GroupId .Where is needed or added. Ordering is flat alphabetical by name
        // (no owner-based grouping, since Contacts have no ownership/edit-restriction concept).
        var entities = await DbContext.Contacts
            .Include(c => c.ProfileImage)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Notes).ThenInclude(n => n.Author)
            .OrderBy(c => c.Name)
            .ToListAsync(token);

        var contacts = Mapper.Map<IList<Contact>>(entities);
        foreach (var contact in contacts)
        {
            contact.Notes = [.. contact.Notes.OrderByDescending(n => n.CreatedAt)];
        }
        return contacts;
    }

    /// <inheritdoc/>
    public async Task<Contact?> GetContactWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.Contacts
            .Include(c => c.ProfileImage)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Notes).ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(c => c.Id == id, token);
        if (entity == null) return null;

        var contact = Mapper.Map<Contact>(entity);
        contact.Notes = [.. contact.Notes.OrderByDescending(n => n.CreatedAt)];
        return contact;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetContactImageAsync(int id, CancellationToken token = default)
    {
        // Rooted at the filtered Contacts DbSet (not ContactImages directly) so the
        // ContactEntity group filter applies -- a cross-group id returns null here.
        return await DbContext.Contacts
            .Where(c => c.Id == id)
            .Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null)
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public override async Task UpdateAsync(Contact model, CancellationToken token = default)
    {
        // Handles only the Contact's own core fields (Name/Description/TownCity/SubLocation/
        // IsRevealed). ProfileImage goes through UpdateProfileImageAsync separately, so the
        // tracked instance is restored here rather than re-derived from the incoming model.
        // Notes are intentionally NOT reconciled here -- they have their own dedicated
        // Add/Update/Delete methods below that manipulate the ContactNotes DbSet directly,
        // avoiding AutoMapper's child-collection replacement problem entirely.
        var entity = await DbContext.Contacts
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == model.Id, token);
        if (entity == null) return;

        var trackedProfileImage = entity.ProfileImage;

        Mapper.Map(model, entity);

        entity.ProfileImage = trackedProfileImage;

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task UpdateProfileImageAsync(int contactId, byte[]? imageData, CancellationToken token = default)
    {
        var entity = await DbContext.Contacts
            .Include(c => c.ProfileImage)
            .FirstOrDefaultAsync(c => c.Id == contactId, token);
        if (entity == null) return;

        if (imageData == null)
        {
            entity.ProfileImage = null;
        }
        else if (entity.ProfileImage == null)
        {
            entity.ProfileImage = new ContactImageEntity
            {
                Id = entity.Id,
                ImageData = imageData
            };
        }
        else
        {
            entity.ProfileImage.ImageData = imageData;
        }

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task AddNoteAsync(ContactNote note, CancellationToken token = default)
    {
        var entity = Mapper.Map<ContactNoteEntity>(note);
        DbContext.Set<ContactNoteEntity>().Add(entity);
        await DbContext.SaveChangesAsync(token);
        note.Id = entity.Id;
    }

    /// <inheritdoc/>
    public async Task UpdateNoteAsync(ContactNote note, CancellationToken token = default)
    {
        var entity = await DbContext.Set<ContactNoteEntity>()
            .FirstOrDefaultAsync(n => n.Id == note.Id, token);
        // Guard against a caller passing a ContactId that doesn't match the note's actual
        // owning Contact (e.g. a stale form) — no-op rather than silently editing the note
        // and redirecting to an unrelated Contact's page.
        if (entity == null || entity.ContactId != note.ContactId) return;

        entity.Text = note.Text;
        entity.UpdatedAt = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task DeleteNoteAsync(int noteId, CancellationToken token = default)
    {
        var entity = await DbContext.Set<ContactNoteEntity>()
            .FirstOrDefaultAsync(n => n.Id == noteId, token);
        if (entity == null) return;

        DbContext.Set<ContactNoteEntity>().Remove(entity);
        await DbContext.SaveChangesAsync(token);
    }
}
