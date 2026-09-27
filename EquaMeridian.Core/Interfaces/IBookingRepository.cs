using EquaMeridian.DTOs.Bookings;

public interface IBookingRepository
{
    Task<BookingsPageDto> GetAllForUserAsync(
        int userId, string role, int page, int pageSize,
        string? search = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null);

    Task<BookingsPageDto> GetAllForAdminAsync(
        int page, int pageSize, string? search = null, string? status = null);
    Task<DeliveryDetailDto?> GetDeliveryDetailAsync(int bookingId, int userId, string role);
    Task<bool> UpdateDeliveryAddressAsync(int bookingId, int contractorId, string newAddress);
    Task<ConfirmDeliveryResult> ConfirmDeliveryAsync(int bookingId, int contractorId, ConfirmDeliveryDto dto);
    Task<ReturnRequestResult> RequestReturnAsync(int bookingId, int contractorId, RequestReturnDto dto);
    Task<MarkReadyResult> MarkReadyForPickupAsync(int bookingId, int supplierId);
    Task<MarkReadyResult> MarkReadyForReturnPickupAsync(int bookingId, int supplierId);
    Task<BookingTrackingDto?> GetTrackingAsync(int bookingId, int userId, string role);

    Task<ConfirmReturnResult> ConfirmReturnAsync(
        int bookingId, int supplierId, ConfirmReturnDto dto, IReadOnlyList<string> photoEvidencePaths);
    Task<CancelBookingResult> CancelAsync(int bookingId, int userId, string role, string reason);
}