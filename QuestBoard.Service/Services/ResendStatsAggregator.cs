using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestBoard.Service.Services;

// Resend returns created_at as "2026-06-27 14:38:46.864865+00" —
// space separator and +HH offset without minutes, both rejected by System.Text.Json.
internal sealed class ResendCreatedAtConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("null created_at");
        s = s.Replace(' ', 'T');
        // "+00" → "+00:00"
        if (s.Length > 3 && s[^3] is '+' or '-' && char.IsDigit(s[^2]) && char.IsDigit(s[^1]))
            s += ":00";
        return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("o"));
}

public record ResendEmailRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("created_at"), JsonConverter(typeof(ResendCreatedAtConverter))] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_event")] string LastEvent);

public record ResendEmailListResponse(
    [property: JsonPropertyName("data")] List<ResendEmailRecord> Data);

public readonly record struct ResendStatCounts(int Sent, int Delivered, int Bounced, int Failed);

public static class ResendStatsAggregator
{
    public static ResendStatCounts Aggregate(IEnumerable<ResendEmailRecord> records, DateTime cutoffUtc)
    {
        int sent = 0, delivered = 0, bounced = 0, failed = 0;

        foreach (var record in records)
        {
            if (record.CreatedAt.UtcDateTime < cutoffUtc)
                continue;

            switch (record.LastEvent)
            {
                case "sent":
                    sent++;
                    break;
                case "delivered":
                case "opened":
                case "clicked":
                    delivered++;
                    break;
                case "bounced":
                    bounced++;
                    break;
                case "failed":
                    failed++;
                    break;
                // delivery_delayed, complained, scheduled — excluded
            }
        }

        return new ResendStatCounts(sent, delivered, bounced, failed);
    }
}
