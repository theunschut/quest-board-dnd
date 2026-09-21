namespace QuestBoard.Service.Constants;

/// <summary>
/// TempData keys for the one-shot cross-board switch banner. Written by the cross-board
/// deep-link middleware and read by the shared toast partial -- two places that cannot see each
/// other's literals, so both sides reference this file rather than a repeated string. The older
/// toast keys in _Toasts.cshtml (Success/Error/Warning/Info/GoldReceived) are deliberately left
/// as plain string literals rather than refactored here -- this file exists for this banner, not
/// as a general cleanup of every toast key in the application.
/// </summary>
public static class TempDataKeys
{
    public const string BoardSwitchTargetName = "BoardSwitchTargetName";
    public const string BoardSwitchPreviousGroupId = "BoardSwitchPreviousGroupId";
    public const string BoardSwitchPreviousGroupName = "BoardSwitchPreviousGroupName";
}
