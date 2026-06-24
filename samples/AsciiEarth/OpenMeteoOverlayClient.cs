using System.Globalization;
using System.Text.Json;

namespace AsciiEarth;

/// <summary>
/// Fetches current weather samples from Open-Meteo for a list of latitude/longitude points.
/// </summary>
internal sealed class OpenMeteoOverlayClient : IDisposable
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";
    private const string UserAgent = "Hex1b-AsciiEarth/1.0 (overlay sampler; https://github.com/mitchdenny/hex1b)";
    private const int MaxPointsPerRequest = 8;

    private readonly HttpClient _http;

    public OpenMeteoOverlayClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async ValueTask<(double TempC, double WindKmh, double WindDirDeg)[]> GetCurrentSamplesAsync(
        IReadOnlyList<(double Lat, double Lon)> points,
        CancellationToken ct = default)
    {
        var result = new (double TempC, double WindKmh, double WindDirDeg)[points.Count];
        if (points.Count == 0)
            return result;

        for (var start = 0; start < points.Count; start += MaxPointsPerRequest)
        {
            var count = Math.Min(MaxPointsPerRequest, points.Count - start);
            var chunk = new (double Lat, double Lon)[count];
            for (var i = 0; i < count; i++)
                chunk[i] = points[start + i];

            var samples = await GetCurrentChunkAsync(chunk, ct);
            for (var i = 0; i < count && i < samples.Length; i++)
                result[start + i] = samples[i];
        }

        return result;
    }

    private async ValueTask<(double TempC, double WindKmh, double WindDirDeg)[]> GetCurrentChunkAsync(
        IReadOnlyList<(double Lat, double Lon)> chunk,
        CancellationToken ct)
    {
        var latCsv = string.Join(",", chunk.Select(p => p.Lat.ToString("0.####", CultureInfo.InvariantCulture)));
        var lonCsv = string.Join(",", chunk.Select(p => p.Lon.ToString("0.####", CultureInfo.InvariantCulture)));
        var url =
            $"{Endpoint}?latitude={latCsv}&longitude={lonCsv}" +
            "&current=temperature_2m,wind_speed_10m,wind_direction_10m&timezone=UTC&wind_speed_unit=kmh";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseSamples(json, chunk.Count);
    }

    private static (double TempC, double WindKmh, double WindDirDeg)[] ParseSamples(string json, int expectedCount)
    {
        var values = new (double TempC, double WindKmh, double WindDirDeg)[expectedCount];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in root.EnumerateArray())
            {
                if (i >= expectedCount)
                    break;
                values[i++] = ReadCurrent(item);
            }
            return values;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            values[0] = ReadCurrent(root);
            return values;
        }

        return values;
    }

    private static (double TempC, double WindKmh, double WindDirDeg) ReadCurrent(JsonElement item)
    {
        if (!item.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
            return default;

        var temp = GetOptionalDouble(current, "temperature_2m");
        var wind = GetOptionalDouble(current, "wind_speed_10m");
        var dir = GetOptionalDouble(current, "wind_direction_10m");
        return (temp, wind, dir);
    }

    private static double GetOptionalDouble(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetDouble();
        return 0.0;
    }

    public void Dispose() => _http.Dispose();
}
