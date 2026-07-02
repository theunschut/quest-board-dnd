using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuestBoard.Service.Services;

public class ResendStatsClient(IHttpClientFactory httpClientFactory, ILogger<ResendStatsClient> logger)
{
    private const int MaxRetries = 3;

    public async Task<(IReadOnlyList<ResendEmailRecord> records, bool error)> FetchAllRecordsAsync(
        string apiKey, DateTime cutoffUtc, CancellationToken token)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Resend");
            var collected = new List<ResendEmailRecord>();
            string? afterId = null;
            bool hasMore = true;

            while (hasMore)
            {
                var url = afterId == null ? "emails?limit=100" : $"emails?limit=100&after={afterId}";

                HttpResponseMessage? response = null;
                for (int attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    // Inject the Bearer token per-request rather than as an HttpClient default header,
                    // so the token is never held in shared client state and can be scoped/rotated per call.
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    response = await client.SendAsync(request, token);

                    if (response.StatusCode != HttpStatusCode.TooManyRequests)
                        break;

                    if (attempt == MaxRetries)
                        break;

                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.Error.WriteLine($"[Resend] 429 received, retrying in {delay.TotalSeconds}s (attempt {attempt + 1}/{MaxRetries})");
                    await Task.Delay(delay, token);
                }

                if (response == null)
                {
                    logger.LogError("Resend stats fetch produced no response");
                    return (collected, true);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(token);
                    Console.Error.WriteLine($"[Resend] {(int)response.StatusCode} {response.StatusCode} — {body}");
                    return (collected, true);
                }

                var json = await response.Content.ReadAsStringAsync(token);
                Console.Error.WriteLine($"[Resend] raw: {json[..Math.Min(500, json.Length)]}");
                var result = JsonSerializer.Deserialize<ResendEmailListResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Data == null || result.Data.Count == 0) break;

                bool reachedCutoff = false;
                foreach (var email in result.Data)
                {
                    if (email.CreatedAt.UtcDateTime < cutoffUtc) { reachedCutoff = true; break; }
                    collected.Add(email);
                }

                if (reachedCutoff || result.Data.Count < 100) hasMore = false;
                else afterId = result.Data[^1].Id;
            }

            return (collected, false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Resend] Exception: {ex.GetType().Name}: {ex.Message}");
            return (Array.Empty<ResendEmailRecord>(), true);
        }
    }
}
