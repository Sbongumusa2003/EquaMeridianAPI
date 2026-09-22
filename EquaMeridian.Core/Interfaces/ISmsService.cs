public interface ISmsService
{
    Task SendSmsAsync(string? toPhoneNumber, string message);

    Task SendQuotationExpiredSmsAsync(string? toPhoneNumber, string name, int quotationId, string listingTitle);

    Task SendBookingConfirmedSmsAsync(string? toPhoneNumber, string name, int bookingId, string listingTitle);
}
