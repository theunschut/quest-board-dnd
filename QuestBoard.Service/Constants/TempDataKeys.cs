namespace QuestBoard.Service.Constants;

/// <summary>
/// TempData keys for the one-shot cross-board switch banner. Written by the cross-board
/// deep-link middleware (all three keys) and by the group picker's return-URL skip path (the
/// target name alone, since a viewer arriving at the picker has no previous board to be offered
/// a way back to), and read by the shared toast partial -- none of these can see the others'
/// literals, so every side references this file rather than a repeated string. The older
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
