namespace QuestBoard.Domain.Enums;

/// <summary>
/// The kinds of board-scoped content that are removed together with a board, in the order the
/// deletion confirmation page lists them. Character classes and image rows are not separate
/// kinds because they go with their character or contact.
/// </summary>
public enum BoardContentKind
{
    /// <summary>Quests.</summary>
    Quests,

    /// <summary>Proposed session dates on quests.</summary>
    ProposedDates,

    /// <summary>Players' votes on proposed dates.</summary>
    DateVotes,

    /// <summary>Player signups on quests.</summary>
    PlayerSignups,

    /// <summary>Records of reminder emails sent for quests.</summary>
    ReminderLogs,

    /// <summary>Calendar events.</summary>
    Events,

    /// <summary>Availability answers on events.</summary>
    EventSignups,

    /// <summary>Repeating schedules that generate events.</summary>
    EventSeries,

    /// <summary>Player characters.</summary>
    Characters,

    /// <summary>Contacts.</summary>
    Contacts,

    /// <summary>Notes written on contacts.</summary>
    ContactNotes,

    /// <summary>Contact categories.</summary>
    ContactCategories,

    /// <summary>Contact tags.</summary>
    ContactTags,

    /// <summary>Shop items.</summary>
    ShopItems,

    /// <summary>Purchases, sales and returns recorded against shop items.</summary>
    Transactions
}
