using QuestBoard.Domain.Enums;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Writes a board's page settings straight to the store so a test sets up exactly the switches and
/// home page it needs, and puts the board back to its unconfigured state afterwards.
/// </summary>
public static class BoardPageSeed
{
    public static async Task SetAsync(WebApplicationFactoryBase factory, int groupId, BoardPageSet disabled, BoardPage home)
    {
        await using var ctx = factory.Database.CreateContext();
        var group = ctx.Groups.Single(g => g.Id == groupId);
        group.DisabledPages = disabled.Mask;
        group.HomePage = (int)home;
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task ResetAsync(WebApplicationFactoryBase factory, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        var group = ctx.Groups.SingleOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return;
        }

        group.DisabledPages = 0;
        group.HomePage = 0;
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
