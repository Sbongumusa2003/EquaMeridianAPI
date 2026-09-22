public class SaCityDeliveryDistanceService : IDeliveryDistanceService
{
    private static readonly Dictionary<string, (double Lat, double Lon)> Cities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["johannesburg"] = (-26.2041, 28.0473),
            ["joburg"] = (-26.2041, 28.0473),
            ["sandton"] = (-26.1076, 28.0567),
            ["pretoria"] = (-25.7479, 28.2293),
            ["tshwane"] = (-25.7479, 28.2293),
            ["centurion"] = (-25.8603, 28.1894),
            ["midrand"] = (-25.9992, 28.1264),
            ["germiston"] = (-26.2111, 28.1578),
            ["boksburg"] = (-26.2110, 28.2590),
            ["kempton park"] = (-26.1000, 28.2300),
            ["soweto"] = (-26.2678, 27.8585),
            ["durban"] = (-29.8587, 31.0218),
            ["pietermaritzburg"] = (-29.6006, 30.3794),
            ["cape town"] = (-33.9249, 18.4241),
            ["stellenbosch"] = (-33.9321, 18.8602),
            ["somerset west"] = (-34.0833, 18.8500),
            ["port elizabeth"] = (-33.9608, 25.6022),
            ["gqeberha"] = (-33.9608, 25.6022),
            ["east london"] = (-33.0153, 27.9116),
            ["bloemfontein"] = (-29.0852, 26.1596),
            ["polokwane"] = (-23.9045, 29.4689),
            ["nelspruit"] = (-25.4753, 30.9694),
            ["mbombela"] = (-25.4753, 30.9694),
            ["rustenburg"] = (-25.6674, 27.2421),
            ["witbank"] = (-25.8738, 29.2134),
            ["emalahleni"] = (-25.8738, 29.2134),
            ["secunda"] = (-26.5158, 29.1914),
            ["kimberley"] = (-28.7282, 24.7499),
            ["upington"] = (-28.4478, 21.2561),
            ["richards bay"] = (-28.7807, 32.0383),
            ["newcastle"] = (-27.7580, 29.9318),
            ["vereeniging"] = (-26.6731, 27.9261),
            ["vanderbijlpark"] = (-26.7117, 27.8382),
            ["potchefstroom"] = (-26.7145, 27.0970),
            ["klerksdorp"] = (-26.8521, 26.6667),
            ["welkom"] = (-27.9831, 26.7340),
            ["george"] = (-33.9642, 22.4597),
            ["mossel bay"] = (-34.1831, 22.1460),
        };

    public Task<decimal> CalculateDistanceKmAsync(string? supplierDispatchLocation, string deliveryAddress)
    {
        var origin = ResolvePoint(supplierDispatchLocation);
        var dest = ResolvePoint(deliveryAddress);

        if (origin == null || dest == null)
            return Task.FromResult(HashFallback(supplierDispatchLocation, deliveryAddress));

        var km = HaversineKm(origin.Value.Lat, origin.Value.Lon, dest.Value.Lat, dest.Value.Lon);
        var roadKm = Math.Round((decimal)(km * 1.25), 1);
        return Task.FromResult(Math.Max(5m, roadKm));
    }
    public static decimal EstimateDeliveryFeeZar(decimal distanceKm, decimal? listingFlatFee = null)
    {
        if (listingFlatFee.HasValue && listingFlatFee.Value > 0)
            return Math.Round(listingFlatFee.Value, 2);

        const decimal baseFee = 1500m;
        const decimal perKm = 28m;
        const decimal minimum = 2500m;
        var fee = baseFee + (distanceKm * perKm);
        return Math.Round(Math.Max(minimum, fee), 2);
    }

    private static (double Lat, double Lon)? ResolvePoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().ToLowerInvariant();
        foreach (var kv in Cities.OrderByDescending(c => c.Key.Length))
        {
            if (t.Contains(kv.Key))
                return kv.Value;
        }
        return null;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private static decimal HashFallback(string? origin, string? dest)
    {
        var o = (origin ?? "").Trim().ToLowerInvariant();
        var d = (dest ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(o) || string.IsNullOrEmpty(d) || o == d) return 0m;
        var hash = (o + "|" + d).GetHashCode();
        var normalized = Math.Abs(hash % 1150) / 10m;
        return Math.Round(5m + normalized, 1);
    }
}
