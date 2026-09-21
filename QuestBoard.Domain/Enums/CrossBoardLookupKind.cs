namespace QuestBoard.Domain.Enums;

/// <summary>
/// One member per kind of thing a cross-board link's id can point at. A route whose kind is not
/// listed here cannot be resolved across boards at all -- adding a member is an explicit,
/// reviewable decision to open a new lookup, not something that happens by routing coincidence.
/// </summary>
public enum CrossBoardLookupKind
{
    Quest,
    Event,
    Character,
    Contact,
    ShopItem,
    EventSeries,
    ContactCategory,

    /// <summary>
    /// Resolved through <see cref="Interfaces.ICrossBoardLinkRepository.ResolveSharedBoardIdsForUserAsync"/>,
    /// never through <see cref="Interfaces.ICrossBoardLinkRepository.ResolveBoardIdAsync"/> --
    /// the id here is a target user, not an entity, and the ambiguity rule needs the whole set
    /// of shared boards rather than the first match.
    /// </summary>
    BoardMember
}
