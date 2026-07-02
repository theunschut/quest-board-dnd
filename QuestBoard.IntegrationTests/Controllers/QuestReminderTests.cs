using System.Net;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Controllers.QuestBoard;

namespace QuestBoard.IntegrationTests.Controllers;

#pragma warning disable CS9113 // Parameter is unread.
public class QuestReminderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
#pragma warning restore CS9113 // Parameter is unread.
{
    // Verify controller constructor uses IReminderJobDispatcher (not IBackgroundJobClient directly)
    [Fact]
    public void QuestController_ConstructorInjectsIReminderJobDispatcher()
    {
        var constructor = typeof(QuestController).GetConstructors().Single();
        var paramTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        paramTypes.Should().Contain(typeof(IReminderJobDispatcher),
            "REMIND-03 requires IReminderJobDispatcher in QuestController constructor — " +
            "IBackgroundJobClient must not be injected directly (unavailable in Testing environment)");
    }

    // Verify IBackgroundJobClient is NOT in QuestController constructor
    [Fact]
    public void QuestController_ConstructorDoesNotInjectIBackgroundJobClient()
    {
        var constructor = typeof(QuestController).GetConstructors().Single();
        var paramTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        paramTypes.Should().NotContain(t => t.Name == "IBackgroundJobClient",
            "REMIND-03: IBackgroundJobClient is not registered in Testing environment; " +
            "use IReminderJobDispatcher abstraction instead");
    }

    // SendReminder action requires authentication
    [Fact]
    public async Task SendReminder_WhenUnauthenticated_RedirectsToLogin()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", "dummy")
        });

        var response = await client.PostAsync("/Quest/SendReminder/1", formData, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Unauthorized, HttpStatusCode.Found],
            "SendReminder must redirect unauthenticated users to login");
    }
}
