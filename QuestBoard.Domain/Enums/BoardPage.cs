namespace QuestBoard.Domain.Enums;

/// <summary>
/// The pages a board can offer. The values are stored in the database (as the bit positions of
/// <see cref="BoardPageSet"/> and as a board's home page), so they must never be reordered or
/// renumbered; a new page takes the next unused value.
/// </summary>
public enum BoardPage
{
    Quests = 0,
    Calendar = 1,
    Events = 2,
    QuestLog = 3,
    Characters = 4,
    Contacts = 5,
    Players = 6,
    Shop = 7,
    DungeonMasterProfile = 8
}

/// <summary>
/// The pages a board has switched off, held as a bit mask. Each bit position is the numeric value
/// of the matching <see cref="BoardPage"/>, which is why the two types share a file: the stored
/// contract reads in one place. An empty set means nothing is switched off.
/// </summary>
public readonly record struct BoardPageSet(int Mask)
{
    public static BoardPageSet Empty => new(0);

    public bool Contains(BoardPage page) => (Mask & (1 << (int)page)) != 0;

    public BoardPageSet With(BoardPage page) => new(Mask | (1 << (int)page));

    public BoardPageSet Without(BoardPage page) => new(Mask & ~(1 << (int)page));
}
