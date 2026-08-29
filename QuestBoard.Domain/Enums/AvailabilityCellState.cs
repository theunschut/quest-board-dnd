namespace QuestBoard.Domain.Enums;

// Five states, not three: an availability grid cell can be a stored Yes nobody has
// confirmed yet (still counted as available), any of the three confirmed answers, or the
// absence of a signup row entirely. Collapsing UnconfirmedYes into ConfirmedYes or Empty
// would erase a real distinction the page needs to show.
public enum AvailabilityCellState
{
    Empty,
    ConfirmedYes,
    ConfirmedMaybe,
    ConfirmedNo,
    UnconfirmedYes
}
