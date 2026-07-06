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
        await repository.UpdateProfileImageAsync(model.Id, model.ContactImageData, token);
        await repository.UpdateAsync(model, token);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetContactImageAsync(int id, CancellationToken token = default)
    {
        return repository.GetContactImageAsync(id, token);
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
