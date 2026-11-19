using System.Net;
using System.Text;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Stands in for the network behind the named Resend client: it answers the emails list request with canned records
/// or an error status, and remembers what it was asked so a test can prove no real key or address was involved.
/// </summary>
public sealed class CannedResendHandler : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<(Uri? Uri, string? Authorization)> _requests = new();
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _body = """{"data":[]}""";

    /// <summary>Every request seen so far: the address it went to and the authorization header it carried.</summary>
    public IReadOnlyList<(Uri? Uri, string? Authorization)> Requests
    {
        get { lock (_gate) return _requests.ToList(); }
    }

    /// <summary>Answers with these delivery states, each created an hour ago so it falls inside the stats window.</summary>
    public void RespondWithRecords(params string[] lastEvents)
    {
        var created = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss.ffffff");
        var records = lastEvents.Select((lastEvent, index) =>
            $$"""{"id":"e{{index}}","created_at":"{{created}}+00","last_event":"{{lastEvent}}"}""");

        lock (_gate)
        {
            _status = HttpStatusCode.OK;
            _body = $$"""{"data":[{{string.Join(",", records)}}]}""";
        }
    }

    /// <summary>Answers every request with this error status and an empty error body.</summary>
    public void RespondWithStatus(HttpStatusCode status)
    {
        lock (_gate)
        {
            _status = status;
            _body = """{"message":"canned error"}""";
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpStatusCode status;
        string body;
        lock (_gate)
        {
            _requests.Add((request.RequestUri, request.Headers.Authorization?.ToString()));
            status = _status;
            body = _body;
        }

        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }

    // The client factory disposes the handlers it built when their lifetime rotates; the one canned handler is shared
    // by the whole test host, so it must survive that.
    protected override void Dispose(bool disposing)
    {
    }
}
