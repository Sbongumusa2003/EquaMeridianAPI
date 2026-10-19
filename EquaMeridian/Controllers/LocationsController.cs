using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EquaMeridian.Controllers;

/// <summary>
/// Place search for delivery addresses (OpenRouteService geocode, South Africa only).
/// Used by Book Now / cart so contractors search suburb/city instead of typing everything.
/// </summary>
[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<LocationsController> _logger;

    private const string AutocompleteUrl = "https://api.openrouteservice.org/geocode/autocomplete";
    private const string SearchUrl = "https://api.openrouteservice.org/geocode/search";

    public LocationsController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<LocationsController> logger)
    {
        _http = httpClientFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EquaMeridian/1.0");
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/locations/search?q=Sandton
    /// Returns SA place suggestions (suburb, city, province, postal code).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<PlaceSuggestionDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new List<PlaceSuggestionDto>());

        var apiKey = _config["OpenRouteService:ApiKey"]
                     ?? _config["DeliveryDistance:OpenRouteApiKey"]
                     ?? Environment.GetEnvironmentVariable("OPENROUTESERVICE_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenRouteService API key not configured — place search unavailable.");
            return Ok(new List<PlaceSuggestionDto>());
        }

        try
        {
            // Prefer autocomplete for type-ahead; fall back to search if needed.
            var results = await QueryOrsAsync(AutocompleteUrl, q, apiKey, ct);
            if (results.Count == 0)
                results = await QueryOrsAsync(SearchUrl, q, apiKey, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Place search failed for '{Query}'", q);
            return Ok(new List<PlaceSuggestionDto>());
        }
    }

    private async Task<List<PlaceSuggestionDto>> QueryOrsAsync(
        string baseUrl, string text, string apiKey, CancellationToken ct)
    {
        var url = $"{baseUrl}?api_key={Uri.EscapeDataString(apiKey)}"
                + $"&text={Uri.EscapeDataString(text)}"
                + "&boundary.country=ZA"
                + "&size=8"
                + "&layers=locality,neighbourhood,borough,county,postalcode";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("ORS geocode HTTP {Status}: {Body}", (int)response.StatusCode, body);
            return new List<PlaceSuggestionDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<PlaceSuggestionDto>();
        if (!doc.RootElement.TryGetProperty("features", out var features))
            return list;

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var props))
                continue;

            var label = GetStr(props, "label") ?? GetStr(props, "name") ?? "";
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var name = GetStr(props, "name");
            var locality = GetStr(props, "locality") ?? GetStr(props, "county");
            var neighbourhood = GetStr(props, "neighbourhood") ?? GetStr(props, "borough");
            var region = GetStr(props, "region"); // province in SA
            var postal = GetStr(props, "postalcode");
            var country = GetStr(props, "country") ?? "South Africa";

            // Build a clean display line without forcing the user to retype place parts.
            var suburb = neighbourhood ?? name;
            var city = locality ?? name;
            if (string.Equals(suburb, city, StringComparison.OrdinalIgnoreCase))
                suburb = neighbourhood; // avoid duplicating city as suburb

            double? lon = null, lat = null;
            if (feature.TryGetProperty("geometry", out var geom)
                && geom.TryGetProperty("coordinates", out var coords)
                && coords.GetArrayLength() >= 2)
            {
                lon = coords[0].GetDouble();
                lat = coords[1].GetDouble();
            }

            list.Add(new PlaceSuggestionDto
            {
                Label = label,
                Suburb = suburb,
                City = city,
                Province = region,
                PostalCode = postal,
                Country = country,
                Latitude = lat,
                Longitude = lon
            });
        }

        return list;
    }

    private static string? GetStr(JsonElement props, string name)
    {
        if (!props.TryGetProperty(name, out var el)) return null;
        var s = el.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}

public class PlaceSuggestionDto
{
    public string Label { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
