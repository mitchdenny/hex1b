using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FormDemo;

/// <summary>
/// Geocoding client using OpenStreetMap's Nominatim service.
/// Converts address strings to latitude/longitude coordinates.
/// </summary>
internal sealed class NominatimClient : IDisposable
{
    private const string BaseUrl = "https://nominatim.openstreetmap.org/search";
    private const string UserAgent = "Hex1b-FormDemo/1.0 (terminal form demo; https://github.com/mitchdenny/hex1b)";

    private readonly HttpClient _http;
    private CancellationTokenSource? _debounceCts;
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(800);

    public NominatimClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>
    /// Geocodes an address string with debouncing. Cancels any pending request.
    /// Returns null if the address cannot be resolved.
    /// </summary>
    public async Task<GeocodingResult?> GeocodeAsync(string address)
    {
        // Cancel any pending debounced request
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        try
        {
            await Task.Delay(_debounceDelay, ct);

            if (string.IsNullOrWhiteSpace(address))
                return null;

            var encoded = Uri.EscapeDataString(address);
            var url = $"{BaseUrl}?q={encoded}&format=json&limit=1";

            var results = await _http.GetFromJsonAsync<NominatimResult[]>(url, ct);
            if (results is null || results.Length == 0)
                return null;

            var first = results[0];
            if (double.TryParse(first.Lat, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(first.Lon, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                return new GeocodingResult(lat, lon, first.DisplayName ?? address);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _http.Dispose();
    }
}

internal sealed record GeocodingResult(double Latitude, double Longitude, string DisplayName);

internal sealed class NominatimResult
{
    [JsonPropertyName("lat")]
    public string? Lat { get; set; }

    [JsonPropertyName("lon")]
    public string? Lon { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
