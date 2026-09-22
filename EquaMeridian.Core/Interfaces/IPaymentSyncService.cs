public interface IPaymentSyncService
{
    Task<string> SyncIfPendingAsync(int invoiceId, string currentStatus);
    Task NotifySupplierOfPaymentAsync(Invoice invoice);
}
