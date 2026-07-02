using Microsoft.AspNetCore.Authorization;

namespace QuestBoard.Service.Authorization;

public class DungeonMasterRequirement : IAuthorizationRequirement
{
    // This requirement doesn't need any additional parameters
}