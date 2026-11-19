using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Jobs;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Adding an existing account to a board mails that person, and the mail shares the person's hourly budget with the
/// resend and email-change buttons. Under the budget the mail goes out as before. With the budget spent the person is
/// still added, nothing is mailed, and the Success toast says that no email was sent. This holds for Create User by
/// address on the Admin page and for Add Member and Create New User on the platform board page.
/// </summary>
/// <remarks>
/// The per-target limiter is a process-wide singleton that never resets, and clearing the in-memory database restarts
/// its ids. This class therefore has its own fixture and never clears the database, so every account it makes has an
/// id no earlier test has spent budget on.
/// </remarks>
public class AddedMemberMailBudgetTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string NotSentSentence = "No email was sent, because too many emails were sent to this account recently.";
    private const string SentSentence = "A notification email has been sent.";

    private readonly WebApplicationFactoryBase _factory;

    public AddedMemberMailBudgetTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // ---- Setup -------------------------------------------------------------------------------------

    private async Task SeedBoardAsync()
    {
        await TestDataHelper.SeedRolesAsync(_factory.Services);
        await TestDataHelper.SeedDefaultGroupAsync(_factory.Services);
    }

    // An account that exists but is on no board. A confirmed account is told by a membership email; an unconfirmed one
    // is sent a welcome email with a set-password link instead.
    private async Task<UserEntity> SeedOutsiderAsync(string tag, bool confirmed)
    {
        var account = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"mailbudget_{tag}", $"mailbudget_{tag}@example.com", "Test123!", $"Mail Budget {tag}");
        if (!confirmed)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var row = context.Users.Single(u => u.Id == account.Id);
            row.EmailConfirmed = false;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return account;
    }

    // Spends the person's whole hourly budget on the same limiter the resend and email-change buttons use.
    private void SpendMailBudget(int userId)
    {
        var limiter = _factory.Services.GetRequiredService<PartitionedRateLimiter<int>>();
        for (var i = 0; i < 3; i++)
        {
            using var lease = limiter.AttemptAcquire(userId);
            lease.IsAcquired.Should().BeTrue("the budget is spent here, before the add");
        }

        using var refused = limiter.AttemptAcquire(userId);
        refused.IsAcquired.Should().BeFalse("three mails an hour is the whole budget");
    }

    private int MailsTo(string email) =>
        _factory.JobClient.EnqueuedJobs.Count(j =>
            (j.Type == typeof(GroupMembershipAddedEmailJob) || j.Type == typeof(WelcomeEmailJob))
            && j.Args.Count > 0 && (j.Args[0] as string) == email);

    private async Task<GroupRole?> RoleOnBoardAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var row = await context.UserGroups.FirstOrDefaultAsync(
            ug => ug.UserId == userId && ug.GroupId == 1, TestContext.Current.CancellationToken);
        return row == null ? null : (GroupRole)row.GroupRole;
    }

    // ---- Sending the add ---------------------------------------------------------------------------

    private async Task<(HttpClient Client, HttpResponseMessage Response)> AddAsync(string path, UserEntity account, GroupRole role)
    {
        HttpClient client;
        Dictionary<string, string> form;
        if (path == "Admin/CreateUser")
        {
            (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
                _factory, $"mailbudget_admin_{account.Id}", $"mailbudget_admin_{account.Id}@example.com");
            form = new Dictionary<string, string>
            {
                ["Email"] = account.Email!,
                ["Name"] = "Ignored For An Existing Account",
                ["GroupRole"] = ((int)role).ToString()
            };
        }
        else
        {
            (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
                _factory, $"mailbudget_super_{account.Id}", $"mailbudget_super_{account.Id}@example.com");
            form = path == "platform/Group/AddMember/1"
                ? new Dictionary<string, string> { ["UserId"] = account.Id.ToString(), ["Role"] = ((int)role).ToString() }
                : new Dictionary<string, string>
                {
                    ["Email"] = account.Email!,
                    ["Name"] = "Ignored For An Existing Account",
                    ["GroupRole"] = ((int)role).ToString()
                };
        }

        var response = await client.PostAsync($"/{path}", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        return (client, response);
    }

    private static async Task<(List<string> Kinds, string Html)> LandedToastsAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var kinds = Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value).ToList();
        return (kinds, html);
    }

    // ---- Facts -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Admin/CreateUser", true)]
    [InlineData("Admin/CreateUser", false)]
    [InlineData("platform/Group/AddMember/1", true)]
    [InlineData("platform/Group/AddMember/1", false)]
    [InlineData("platform/Group/CreateMember/1", true)]
    [InlineData("platform/Group/CreateMember/1", false)]
    public async Task AddingAnExistingAccount_UnderItsMailBudget_AddsThemAndSendsTheEmail(string path, bool confirmed)
    {
        await SeedBoardAsync();
        var account = await SeedOutsiderAsync($"under_{Guid.NewGuid():N}"[..20], confirmed);

        var (client, response) = await AddAsync(path, account, GroupRole.DungeonMaster);

        (await RoleOnBoardAsync(account.Id)).Should().Be(GroupRole.DungeonMaster);
        MailsTo(account.Email!).Should().Be(1, "the person had budget left, so they are told about the add");
        var (kinds, html) = await LandedToastsAsync(client, response);
        kinds.Should().Contain("Success");
        html.Should().Contain($"{account.Name} has been added to the group as Dungeon Master. {SentSentence}");
        html.Should().NotContain(NotSentSentence);
    }

    [Theory]
    [InlineData("Admin/CreateUser", true)]
    [InlineData("Admin/CreateUser", false)]
    [InlineData("platform/Group/AddMember/1", true)]
    [InlineData("platform/Group/AddMember/1", false)]
    [InlineData("platform/Group/CreateMember/1", true)]
    [InlineData("platform/Group/CreateMember/1", false)]
    public async Task AddingAnExistingAccount_WithItsMailBudgetSpent_StillAddsThem_SendsNoEmail_AndSaysSo(string path, bool confirmed)
    {
        await SeedBoardAsync();
        var account = await SeedOutsiderAsync($"over_{Guid.NewGuid():N}"[..20], confirmed);
        SpendMailBudget(account.Id);

        var (client, response) = await AddAsync(path, account, GroupRole.DungeonMaster);

        (await RoleOnBoardAsync(account.Id)).Should().Be(GroupRole.DungeonMaster, "a spent mail budget never refuses the add");
        MailsTo(account.Email!).Should().Be(0, "the person's hourly budget was already spent");
        var (kinds, html) = await LandedToastsAsync(client, response);
        kinds.Should().Contain("Success").And.NotContain("Error");
        html.Should().Contain($"{account.Name} has been added to the group as Dungeon Master. {NotSentSentence}");
        html.Should().NotContain(SentSentence);
    }

    // The Success toast names the role in the words the badges and role selects on the same pages show, never the
    // enum's own name ("DungeonMaster"). The facts live here, not beside the other Admin and platform facts, because
    // each add spends a permit of the person's budget, and a class that clears its database reuses the same ids.
    [Theory]
    [InlineData("Admin/CreateUser", GroupRole.Player, "Player")]
    [InlineData("Admin/CreateUser", GroupRole.DungeonMaster, "Dungeon Master")]
    [InlineData("Admin/CreateUser", GroupRole.Admin, "Admin")]
    [InlineData("platform/Group/AddMember/1", GroupRole.Player, "Player")]
    [InlineData("platform/Group/AddMember/1", GroupRole.DungeonMaster, "Dungeon Master")]
    [InlineData("platform/Group/CreateMember/1", GroupRole.DungeonMaster, "Dungeon Master")]
    [InlineData("platform/Group/CreateMember/1", GroupRole.Admin, "Admin")]
    public async Task AddingAnExistingAccount_NamesTheRoleInWordsInTheSuccessToast(string path, GroupRole role, string word)
    {
        await SeedBoardAsync();
        var account = await SeedOutsiderAsync($"word_{Guid.NewGuid():N}"[..20], confirmed: true);

        var (client, response) = await AddAsync(path, account, role);

        var (_, html) = await LandedToastsAsync(client, response);
        html.Should().Contain($"{account.Name} has been added to the group as {word}.");
        html.Should().NotContain("added to the group as DungeonMaster");
    }

    // The resend button refuses once an add has used the last of the person's budget, so the two paths share one
    // budget rather than each having their own.
    [Fact]
    public async Task AnAddThatMailsThePerson_SpendsTheSameBudgetTheResendButtonUses()
    {
        await SeedBoardAsync();
        var account = await SeedOutsiderAsync($"shared_{Guid.NewGuid():N}"[..20], confirmed: true);
        var limiter = _factory.Services.GetRequiredService<PartitionedRateLimiter<int>>();
        for (var i = 0; i < 2; i++)
        {
            using var lease = limiter.AttemptAcquire(account.Id);
            lease.IsAcquired.Should().BeTrue();
        }

        await AddAsync("Admin/CreateUser", account, GroupRole.Player);

        MailsTo(account.Email!).Should().Be(1, "one permit was left, and the add used it");
        using var afterAdd = limiter.AttemptAcquire(account.Id);
        afterAdd.IsAcquired.Should().BeFalse("the add spent the last permit of the shared budget");
    }
}
