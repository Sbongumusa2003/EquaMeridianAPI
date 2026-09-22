public interface IDeliveryDistanceService
{
    Task<decimal> CalculateDistanceKmAsync(string? supplierDispatchLocation, string deliveryAddress);
}
