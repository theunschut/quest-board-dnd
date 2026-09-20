using Hangfire;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Extensions;

// Hangfire is not registered in the Testing environment, so the live cron registration in
// Program.cs can never be observed by an integration test. This factory pulls the options
// construction out into its own directly unit-testable seam, so the board-zone wiring stays
// provable even though the registration call itself is not.
internal static class RecurringJobOptionsFactory
{
    internal static RecurringJobOptions ForBoardZone(IBoardClock boardClock) => new() { TimeZone = boardClock.TimeZone };
}
