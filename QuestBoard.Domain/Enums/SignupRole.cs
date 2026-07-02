namespace QuestBoard.Domain.Enums;

public enum SignupRole
{
    Player = 0,      // Default - counts toward player limit, can vote
    Spectator = 1,   // Auto-approved, doesn't count toward limit, cannot vote
    AssistantDM = 2  // Pre-checked for DM approval, doesn't count, can vote
}
