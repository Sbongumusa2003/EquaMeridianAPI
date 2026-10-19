using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Real road-distance service for South Africa using the OpenRouteService API
/// (https://openrouteservice.org). Geocodes free-text addresses and returns
/// driving distance in km. Results are cached so the same origin→destination
/// pair always yields a stable fee.
///
/// Falls back to <see cref="SaCityDeliveryDistanceService"/> when the API key
/// is missing or the remote call fails.
/// </summary>
public class OpenRouteDeliveryDistanceService : IDeliveryDistanceService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenRouteDeliveryDistanceService> _logger;
    private readonly SaCityDeliveryDistanceService _fallback;
    private readonly ConcurrentDictionary<string, decimal> _cache = new(StringComparer.OrdinalIgnoreCase);

    private const string GeocodeBase = "https://api.openrouteservice.org/geocode/search";
    private const string DirectionsBase = "https://api.openrouteservice.org/v2/directions/driving-car";

    public OpenRouteDeliveryDistanceService(
        HttpClient http,
        IConfiguration config,
        ILogger<OpenRouteDeliveryDistanceService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _fallback = new SaCityDeliveryDistanceService();
    }

    public async Task<decimal> CalculateDistanceKmAsync(string? supplierDispatchLocation, string deliveryAddress)
    {
        var originText = (supplierDispatchLocation ?? string.Empty).Trim();
        var destText = (deliveryAddress ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(originText) || string.IsNullOrWhiteSpace(destText))
            return 0m;

        // Normalise key so minor whitespace/case differences don't break the cache.
        var cacheKey = NormalizeKey(originText) + " → " + NormalizeKey(destText);
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var apiKey = _config["OpenRouteService:ApiKey"]
                     ?? _config["DeliveryDistance:OpenRouteApiKey"]
                     ?? Environment.GetEnvironmentVariable("OPENROUTESERVICE_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "OpenRouteService API key not configured. Falling back to SA city distance service.");
            return await CacheAndReturn(cacheKey, await _fallback.CalculateDistanceKmAsync(originText, destText));
        }

        try
        {
            var origin = await GeocodeAsync(originText, apiKey);
            var dest = await GeocodeAsync(destText, apiKey);

            if (origin == null || dest == null)
            {
                _logger.LogWarning(
                    "Could not geocode origin='{Origin}' or dest='{Dest}'. Using SA city fallback.",
                    originText, destText);
                return await CacheAndReturn(cacheKey, await _fallback.CalculateDistanceKmAsync(originText, destText));
            }

            var roadKm = await GetDrivingDistanceKmAsync(origin.Value, dest.Value, apiKey);
            if (roadKm == null)
            {
                _logger.LogWarning(
                    "Directions call failed for {Origin} → {Dest}. Using SA city fallback.",
                    originText, destText);
                return await CacheAndReturn(cacheKey, await _fallback.CalculateDistanceKmAsync(originText, destText));
            }

            // Clamp to a sensible minimum so very short hops still attract the base fee logic upstream.
            var result = Math.Max(1m, Math.Round(roadKm.Value, 1));
            return await CacheAndReturn(cacheKey, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouteService distance lookup failed. Using SA city fallback.");
            return await CacheAndReturn(cacheKey, await _fallback.CalculateDistanceKmAsync(originText, destText));
        }
    }

    private async Task<decimal> CacheAndReturn(string key, decimal value)
    {
        _cache[key] = value;
        return value;
    }

    private static string NormalizeKey(string text) =>
        string.Join(' ', text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Geocode a free-text address, restricted to South Africa.</summary>
    private async Task<(double Lon, double Lat)?> GeocodeAsync(string text, string apiKey)
    {
        // Append ", South Africa" when the text does not already mention it – improves match quality.
        var query = text;
        if (!text.Contains("south africa", StringComparison.OrdinalIgnoreCase)
            && !text.Contains(" rsa", StringComparison.OrdinalIgnoreCase)
            && !text.Contains(", za", StringComparison.OrdinalIgnoreCase))
        {
            query = text + ", South Africa";
        }

        var url = $"{GeocodeBase}?api_key={Uri.EscapeDataString(apiKey)}"
                + $"&text={Uri.EscapeDataString(query)}"
                + "&boundary.country=ZA"
                + "&size=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Geocode HTTP {Status} for '{Text}'", (int)response.StatusCode, text);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        if (!doc.RootElement.TryGetProperty("features", out var features)
            || features.GetArrayLength() == 0)
            return null;

        var coords = features[0].GetProperty("geometry").GetProperty("coordinates");
        // GeoJSON order is [lon, lat]
        var lon = coords[0].GetDouble();
        var lat = coords[1].GetDouble();
        return (lon, lat);
    }

    /// <summary>Driving distance in km via OpenRouteService directions endpoint.</summary>
    private async Task<decimal?> GetDrivingDistanceKmAsync(
        (double Lon, double Lat) origin,
        (double Lon, double Lat) dest,
        string apiKey)
    {
        // Body format required by ORS directions API
        var body = new
        {
            coordinates = new[]
            {
                new[] { origin.Lon, origin.Lat },
                new[] { dest.Lon, dest.Lat }
            },
            units = "km"
        };

        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, DirectionsBase);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Directions HTTP {Status}: {Body}", (int)response.StatusCode, err);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        // Response shape: { "routes": [ { "summary": { "distance": <km> } } ] }
        if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            return null;

        var summary = routes[0].GetProperty("summary");
        if (!summary.TryGetProperty("distance", out var distEl))
            return null;

        // When units=km the value is already in kilometres.
        var km = distEl.GetDouble();
        return (decimal)km;
    }
}
