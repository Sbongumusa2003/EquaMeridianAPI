public class PlaceholderDeliveryDistanceService : IDeliveryDistanceService
{
    public Task<decimal> CalculateDistanceKmAsync(string? supplierDispatchLocation, string deliveryAddress)
    {
        var origin = (supplierDispatchLocation ?? string.Empty).Trim().ToLowerInvariant();
        var destination = (deliveryAddress ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination) || origin == destination)
            return Task.FromResult(0m);
        var hash = (origin + "|" + destination).GetHashCode();
        var normalized = Math.Abs(hash % 1150) / 10m;
        var distance = 5m + normalized;

        return Task.FromResult(Math.Round(distance, 1));
    }
}
