using Microsoft.AspNetCore.Authorization;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Authorization;

public class AdminHandler(
    IUserService userService,
    IActiveGroupContext activeGroupContext)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        // Step 1: SuperAdmin bypass — reads claims directly, no DB call
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Step 2: Null group guard
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            context.Fail();
            return;
        }

        // Step 3: Group role check
        var role = await userService.GetGroupRoleAsync(context.User, groupId);
        if (role == GroupRole.Admin)
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
