using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QuestBoard.Service.Services;

namespace QuestBoard.UnitTests.Services;

[Trait("Category", "EmailStats")]
public class ResendStatsClientTests
{
    private const string ApiKey = "test-api-key";

    private static DateTime CutoffUtc => new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string SingleRecordJson(string id = "e1", DateTime? createdAt = null)
    {
        var created = createdAt ?? CutoffUtc.AddDays(1);
        return $$"""
        {"data":[{"id":"{{id}}","created_at":"{{created:yyyy-MM-dd HH:mm:ss.ffffff}}+00","last_event":"delivered"}]}
        """;
    }

    private static ResendStatsClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Resend").Returns(httpClient);

        // Shrink the backoff base delay so retry tests run fast and deterministically.
        return new ResendStatsClient(factory, NullLogger<ResendStatsClient>.Instance, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FetchAllRecordsAsync_429ThenSuccess_RetriesAndReturnsRecords()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.TooManyRequests, null);
        handler.Enqueue(HttpStatusCode.OK, SingleRecordJson());
        var client = CreateClient(handler);

        // Act
        var (records, error) = await client.FetchAllRecordsAsync(ApiKey, CutoffUtc, TestContext.Current.CancellationToken);

        // Assert
        error.Should().BeFalse();
        records.Should().HaveCount(1);
        handler.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task FetchAllRecordsAsync_PersistentThrottle_ReturnsErrorAfterBudget()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.AlwaysReturn(HttpStatusCode.TooManyRequests, null);
        var client = CreateClient(handler);

        // Act
        var (records, error) = await client.FetchAllRecordsAsync(ApiKey, CutoffUtc, TestContext.Current.CancellationToken);

        // Assert: 1 initial attempt + 3 retries = 4 total invocations — proves the retry budget is bounded.
        error.Should().BeTrue();
        handler.InvocationCount.Should().Be(4);
    }

    [Fact]
    public async Task FetchAllRecordsAsync_NonThrottleFailure_ReturnsErrorWithoutRetry()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        handler.AlwaysReturn(HttpStatusCode.InternalServerError, null);
        var client = CreateClient(handler);

        // Act
        var (records, error) = await client.FetchAllRecordsAsync(ApiKey, CutoffUtc, TestContext.Current.CancellationToken);

        // Assert: non-429 failures must not be retried.
        error.Should().BeTrue();
        handler.InvocationCount.Should().Be(1);
    }

    /// <summary>
    /// Scripted HttpMessageHandler that replays a queue of responses in order, then falls back
    /// to a configured "always" response once the queue is exhausted. Counts invocations so tests
    /// can assert exactly how many HTTP calls (i.e. retry attempts) were made.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string? JsonBody)> _scripted = new();
        private (HttpStatusCode Status, string? JsonBody)? _fallback;

        public int InvocationCount { get; private set; }

        public void Enqueue(HttpStatusCode status, string? jsonBody) => _scripted.Enqueue((status, jsonBody));

        public void AlwaysReturn(HttpStatusCode status, string? jsonBody) => _fallback = (status, jsonBody);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InvocationCount++;

            var (status, jsonBody) = _scripted.Count > 0
                ? _scripted.Dequeue()
                : _fallback ?? (HttpStatusCode.OK, """{"data":[]}""");

            var response = new HttpResponseMessage(status);
            if (jsonBody != null)
            {
                response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
