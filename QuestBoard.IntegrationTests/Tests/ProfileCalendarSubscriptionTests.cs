using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

// Proves the Calendar Subscription section on both Profile layouts, over real HTTP. Every
// markup fact below runs twice -- once with the default (desktop) user agent and once with a
// real mobile user agent -- because the mobile view is selected purely from that request
// header, and devtools viewport emulation cannot exercise it at all. This codebase has shipped
// a control on one Profile layout and not the other more than once (Phases 43, 54, 72), which
// is why every layout-sensitive fact here is written as a theory over both layouts rather than
// assumed to carry over from one.
public class ProfileCalendarSubscriptionTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    // Requests Profile, optionally attaching a real mobile User-Agent header, and returns the
    // status and rendered markup. The client's own default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetProfileAsync(HttpClient client, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        if (mobile)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        }
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // Mints through the real service from a scope, the same production write path Profile's Add
    // control calls, rather than a direct database insert.
    private async Task<CalendarSubscription> MintSubscriptionAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        return await subscriptionService.MintForUserAsync(userId, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountSubscriptionsAsync(int userId)
    {
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        var subscriptions = await subscriptionService.GetForUserAsync(userId, TestContext.Current.CancellationToken);
        return subscriptions.Count;
    }

    // Fetches Profile, extracts the antiforgery token from the Add form, and posts it -- the
    // real form-post path every mutation fact below must go through so the antiforgery guard is
    // actually exercised rather than assumed.
    private async Task<HttpResponseMessage> PostAddAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
        var (antiForgeryToken, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(new Dictionary<string, string>(), antiForgeryToken);
        return await client.PostAsync("/Account/AddCalendarSubscription", formContent, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostRenameAsync(HttpClient client, int id, string name)
    {
        var getResponse = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
        var (antiForgeryToken, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string> { ["id"] = id.ToString(), ["name"] = name }, antiForgeryToken);
        return await client.PostAsync("/Account/RenameCalendarSubscription", formContent, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostRevokeAsync(HttpClient client, int id)
    {
        var getResponse = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
        var (antiForgeryToken, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);
        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string> { ["id"] = id.ToString() }, antiForgeryToken);
        return await client.PostAsync("/Account/RevokeCalendarSubscription", formContent, TestContext.Current.CancellationToken);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    // Extracts the subscription name text from the desktop row's own label element, rather than
    // asserting against the whole page -- the shared rename modal's "e.g. My Phone" placeholder
    // legitimately coexists elsewhere on the same page once any row exists, so a whole-page
    // NotContain would be the wrong scope for proving the row's own label is not that placeholder.
    private static string ExtractSubscriptionNameCellText(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, @"<div>(.*?)</div>\s*<div class=""calendar-subscription-meta"">");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    // ---- Empty state ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_RendersTheEmptyState_WhenTheMemberHoldsNoSubscription(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_empty", "profcal_empty@example.com");

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("No calendar subscriptions yet");
        html.Should().Contain(
            "Get this board's schedule onto your phone's own calendar app. The address below acts like a password");
        html.Should().Contain("Add Subscription");
    }

    [Fact]
    public async Task Profile_MintsNothingOnLoad()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_noload", "profcal_noload@example.com");

        await GetProfileAsync(client, false);
        await GetProfileAsync(client, true);
        await GetProfileAsync(client, false);

        (await CountSubscriptionsAsync(user.Id)).Should().Be(0,
            because: "a page load is not a press of Add Subscription, and a subscription address is a never-expiring credential -- nothing should be minted just from viewing the page");
    }

    // ---- Populated state and its shape ----

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task Profile_RendersOneRowPerSubscription(int count, bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_rowcount", "profcal_rowcount@example.com");

        for (var i = 0; i < count; i++)
        {
            await MintSubscriptionAsync(user.Id);
        }

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var marker = mobile ? "calendar-subscription-row" : "calendar-subscription-address";
        CountOccurrences(html, marker).Should().Be(count,
            because: "the same row marker must appear once per subscription in the same container markup at every count -- there is no separate single-subscription branch");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_RendersTheDesktopTableShape_AndTheMobileCardShape(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_shape", "profcal_shape@example.com");
        await MintSubscriptionAsync(user.Id);

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        if (mobile)
        {
            html.Should().Contain("calendar-subscription-row");
            html.Should().NotContain("table table-striped table-hover align-middle");
        }
        else
        {
            html.Should().Contain("table table-striped table-hover align-middle");
            html.Should().NotContain("calendar-subscription-row");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_RendersEachRowsFetchStateIndependently(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_fetchstate", "profcal_fetchstate@example.com");
        var fetched = await MintSubscriptionAsync(user.Id);
        await MintSubscriptionAsync(user.Id);

        // Fetch only one of the two addresses so only it carries a fetch time.
        await client.GetAsync($"/feeds/calendar/{fetched.Token}.ics", TestContext.Current.CancellationToken);

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Never fetched yet");
        html.Should().Contain("Last fetched");
        html.Should().NotContain("All subscriptions",
            because: "there is no shared or aggregate status line implying every row is in the same fetch state");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_RendersNoPaginationControl(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_nopaging", "profcal_nopaging@example.com");
        for (var i = 0; i < 10; i++)
        {
            await MintSubscriptionAsync(user.Id);
        }

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var marker = mobile ? "calendar-subscription-row" : "calendar-subscription-address";
        CountOccurrences(html, marker).Should().Be(10,
            because: "the list is deliberately uncapped -- there is no per-member ceiling");
        html.Should().NotContain("Show more");
        html.Should().NotContain("pagination");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_CapsTheNameInputAtSixtyCharacters(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_maxlen", "profcal_maxlen@example.com");
        await MintSubscriptionAsync(user.Id);

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("maxlength=\"60\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_TruncatesAnOverlongNameOnTheMobileLayout(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_truncate", "profcal_truncate@example.com");
        await MintSubscriptionAsync(user.Id);

        var (response, html) = await GetProfileAsync(client, mobile);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        if (mobile)
        {
            html.Should().Contain("calendar-subscription-name",
                because: "the mobile row label element must carry the class the ellipsis rule in account.mobile.css targets");
        }
    }

    // ---- The freshly-minted default name ----

    [Fact]
    public async Task Profile_RendersANonEmptyDefaultName_ForAFreshlyMintedSubscription()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_defaultname", "profcal_defaultname@example.com");

        var addResponse = await PostAddAsync(client);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var html = await (await client.GetAsync(addResponse.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var subscriptions = await CountSubscriptionsAsync(user.Id);
        subscriptions.Should().Be(1);

        // The service's own default label -- a non-empty string, distinct from the rename
        // field's "e.g. My Phone" placeholder, that a member can rename afterwards. Scoped to
        // the row's own label rather than the whole page, since the rename modal's placeholder
        // attribute legitimately coexists elsewhere on the same page once any row exists.
        var rowLabel = ExtractSubscriptionNameCellText(html);
        rowLabel.Should().NotBeNullOrWhiteSpace();
        rowLabel.Should().NotBe("e.g. My Phone");
        rowLabel.Should().Be("New subscription");
    }

    // ---- The round trip ----

    [Fact]
    public async Task AddSubscription_CreatesExactlyOneSubscription_AndShowsIt()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_addonce", "profcal_addonce@example.com");

        var response = await PostAddAsync(client);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var followed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = await followed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        (await CountSubscriptionsAsync(user.Id)).Should().Be(1);
        CountOccurrences(html, "calendar-subscription-address").Should().Be(1);
        html.Should().Contain("Subscription added.");
    }

    [Fact]
    public async Task AddSubscription_RefusesAnImmediateSecondSubmit()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_adddouble", "profcal_adddouble@example.com");

        await PostAddAsync(client);
        var secondResponse = await PostAddAsync(client);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await client.GetAsync(secondResponse.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        (await CountSubscriptionsAsync(user.Id)).Should().Be(1,
            because: "a double submit, browser retry or replay must never mint a second never-expiring credential from one intent");
        html.Should().Contain("Couldn&#x27;t add this subscription");
    }

    [Fact]
    public async Task RenameSubscription_ChangesTheLabel()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_rename", "profcal_rename@example.com");
        var subscription = await MintSubscriptionAsync(user.Id);

        var response = await PostRenameAsync(client, subscription.Id, "My Phone");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("My Phone");
        html.Should().NotContain("New subscription");
    }

    [Fact]
    public async Task RenameSubscription_RefusesAnEmptyName()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_renameempty", "profcal_renameempty@example.com");
        var subscription = await MintSubscriptionAsync(user.Id);

        var response = await PostRenameAsync(client, subscription.Id, "   ");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("New subscription",
            because: "an empty/whitespace-only rename must be refused, leaving the stored name unchanged");
        html.Should().Contain("Couldn&#x27;t rename this subscription");
    }

    [Fact]
    public async Task RenameSubscription_DoesNothing_ForAnotherMembersSubscription()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, ownerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_renameowner", "profcal_renameowner@example.com");
        var subscription = await MintSubscriptionAsync(ownerUser.Id);

        var (otherClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_renameother", "profcal_renameother@example.com");

        var response = await PostRenameAsync(otherClient, subscription.Id, "Hijacked");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await otherClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("Couldn&#x27;t rename this subscription",
            because: "the response must reveal nothing about whether the foreign id exists");
        html.Should().NotContain("Hijacked");

        (await CountSubscriptionsAsync(ownerUser.Id)).Should().Be(1);
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        var ownerSubscriptions = await subscriptionService.GetForUserAsync(ownerUser.Id, TestContext.Current.CancellationToken);
        ownerSubscriptions.Single().Name.Should().Be("New subscription");
    }

    [Fact]
    public async Task RevokeSubscription_RemovesTheRowFromTheList_ButKeepsTheAddressAnswering()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_revoke", "profcal_revoke@example.com");
        var subscription = await MintSubscriptionAsync(user.Id);

        var response = await PostRevokeAsync(client, subscription.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("No calendar subscriptions yet");
        html.Should().NotContain(subscription.Token);

        var feedResponse = await client.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        feedResponse.StatusCode.Should().Be(HttpStatusCode.Gone,
            because: "delete is a retirement -- the address keeps answering, now as gone rather than as if it never existed");
    }

    [Fact]
    public async Task RevokeSubscription_DoesNothing_ForAnotherMembersSubscription()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, ownerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_revokeowner", "profcal_revokeowner@example.com");
        var subscription = await MintSubscriptionAsync(ownerUser.Id);

        var (otherClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profcal_revokeother", "profcal_revokeother@example.com");

        var response = await PostRevokeAsync(otherClient, subscription.Id);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await (await otherClient.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("Couldn&#x27;t delete this subscription",
            because: "the response must reveal nothing about whether the foreign id exists");

        (await CountSubscriptionsAsync(ownerUser.Id)).Should().Be(1);

        var feedResponse = await otherClient.GetAsync(
            $"/feeds/calendar/{subscription.Token}.ics", TestContext.Current.CancellationToken);
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the owner's subscription must remain live after another member's revoke attempt");
    }

    // WebApplicationFactoryBase installs TestAntiforgeryDecorator for every other fact in this
    // suite (and every other suite) so that a form post without a fetched token still succeeds --
    // a live HTTP test against that decorator cannot detect a missing or wrong antiforgery token,
    // matching AntiForgeryTokenCoverageTests' own documented reason for staying reflection-only.
    // This one fact needs the opposite: a genuine runtime proof that the three mutations actually
    // refuse an unauthenticated-by-token request, not merely that the attribute is present on the
    // action. It swaps the decorator back out for the framework's own real antiforgery service in
    // a variant factory, following the same WithWebHostBuilder override pattern
    // ContactCategoryManagementControllerIntegrationTests already uses for a different service.
    private WebApplicationFactory<Program> CreateRealAntiforgeryFactory()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.Antiforgery.IAntiforgery));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddAntiforgery();
            });
        });
    }

    [Fact]
    public async Task Mutations_AreRejectedWithoutAnAntiforgeryToken()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var variantFactory = CreateRealAntiforgeryFactory();
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            variantFactory, "profcal_noantiforgery", "profcal_noantiforgery@example.com");
        var subscription = await MintSubscriptionAsync(user.Id);

        // None of these three posts carries a __RequestVerificationToken field or an antiforgery
        // cookie -- against the real antiforgery service (not the always-succeeds test decorator)
        // this must fail every one of them, proven here by the redirect that a successful mutation
        // always produces never appearing, and by the underlying state staying untouched.
        var addResponse = await client.PostAsync(
            "/Account/AddCalendarSubscription", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        addResponse.StatusCode.Should().NotBe(HttpStatusCode.Redirect,
            because: "a successful Add always redirects back to Profile; an antiforgery failure must never reach that redirect");

        var renameResponse = await client.PostAsync(
            "/Account/RenameCalendarSubscription",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = subscription.Id.ToString(), ["name"] = "No Token" }),
            TestContext.Current.CancellationToken);
        renameResponse.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        var revokeResponse = await client.PostAsync(
            "/Account/RevokeCalendarSubscription",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = subscription.Id.ToString() }),
            TestContext.Current.CancellationToken);
        revokeResponse.StatusCode.Should().NotBe(HttpStatusCode.Redirect);

        (await CountSubscriptionsAsync(user.Id)).Should().Be(1,
            because: "none of the three token-less posts may have succeeded");
        using var scope = factory.Services.CreateScope();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ICalendarSubscriptionService>();
        var stillThere = (await subscriptionService.GetForUserAsync(user.Id, TestContext.Current.CancellationToken)).Single();
        stillThere.Name.Should().Be("New subscription",
            because: "the token-less rename must not have taken effect");
        stillThere.IsRevoked.Should().BeFalse(
            because: "the token-less revoke must not have taken effect");
    }
}
