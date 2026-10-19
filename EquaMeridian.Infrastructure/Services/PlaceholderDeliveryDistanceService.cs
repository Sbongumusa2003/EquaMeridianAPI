/// <summary>
/// Legacy hash-based distance estimator. Prefer <see cref="OpenRouteDeliveryDistanceService"/>.
/// Kept for tests and emergency fallback. Uses a stable hash so results no longer
/// change across process restarts.
/// </summary>
public class PlaceholderDeliveryDistanceService : IDeliveryDistanceService
{
    public Task<decimal> CalculateDistanceKmAsync(string? supplierDispatchLocation, string deliveryAddress)
    {
        var origin = (supplierDispatchLocation ?? string.Empty).Trim().ToLowerInvariant();
        var destination = (deliveryAddress ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination) || origin == destination)
            return Task.FromResult(0m);

        unchecked
        {
            uint hash = 5381;
            foreach (var c in origin + "|" + destination)
                hash = ((hash << 5) + hash) + c;
            var normalized = (hash % 1150) / 10m;
            return Task.FromResult(Math.Round(5m + normalized, 1));
        }
    }
}
