using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DailyReport.Infrastructure.Sources.GoatCounter;

// Wire shapes for GoatCounter's v0 stats API. Field names and nullability follow api.json (see the research
// note in GoatCounterClient.cs) as closely as the untyped JSON allows; every list is nullable because an
// endpoint that returns no rows for a range omits the array in practice as often as it empties it.

/// <summary>GET /api/v0/stats/total → handlers.apiCountTotalResponse.</summary>
internal sealed class GoatCounterTotalResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("total_events")]
    public int TotalEvents { get; init; }

    [JsonPropertyName("total_utc")]
    public int TotalUtc { get; init; }

    [JsonPropertyName("stats")]
    public List<GoatCounterHitListStatDto>? Stats { get; init; }
}

/// <summary>GET /api/v0/stats/hits → handlers.apiHitsResponse.</summary>
internal sealed class GoatCounterHitsResponseDto
{
    [JsonPropertyName("hits")]
    public List<GoatCounterHitDto>? Hits { get; init; }

    [JsonPropertyName("more")]
    public bool More { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

/// <summary>One path's entry in a /stats/hits response → goatcounter.HitList.</summary>
internal sealed class GoatCounterHitDto
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("path_id")]
    public long PathId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }

    /// <summary>Whether this path is flagged as an event ("solve", "solve-four", "share", ...) rather than a page.</summary>
    [JsonPropertyName("event")]
    public bool Event { get; init; }

    [JsonPropertyName("stats")]
    public List<GoatCounterHitListStatDto>? Stats { get; init; }
}

/// <summary>One calendar day's bucket within a HitList or the total response → goatcounter.HitListStat.</summary>
internal sealed class GoatCounterHitListStatDto
{
    [JsonPropertyName("day")]
    [JsonConverter(typeof(GoatCounterDayConverter))]
    public DateOnly? Day { get; init; }

    /// <summary>Total visitors for this day. GoatCounter's v0 API has no separate pageview figure; see the
    /// class comment on <see cref="GoatCounterClient"/> for how <see cref="GoatCounterDay"/> maps this.</summary>
    [JsonPropertyName("daily")]
    public int? Daily { get; init; }
}

/// <summary>GET /api/v0/stats/{page} (page=toprefs, browsers, ...) → handlers.apiStatsResponse.</summary>
internal sealed class GoatCounterStatsResponseDto
{
    [JsonPropertyName("more")]
    public bool More { get; init; }

    [JsonPropertyName("stats")]
    public List<GoatCounterHitStatDto>? Stats { get; init; }
}

/// <summary>One row of a /stats/{page} response → goatcounter.HitStat. For toprefs, Name is the referrer.</summary>
internal sealed class GoatCounterHitStatDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("count")]
    public int? Count { get; init; }
}

/// <summary>
/// GoatCounter's OpenAPI document marks "day" as format "date", but the underlying Go field is a
/// <c>time.Time</c>, so accept a plain "yyyy-MM-dd" (what the project's own tests send as "start"/"end" and
/// what we send too) as well as a full RFC 3339 timestamp, taking its UTC calendar date.
/// </summary>
internal sealed class GoatCounterDayConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var text = reader.GetString();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return DateOnly.FromDateTime(timestamp.UtcDateTime);
        }

        throw new JsonException($"Unrecognised GoatCounter date '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is { } day)
        {
            writer.WriteStringValue(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
