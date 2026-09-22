using EquaMeridian.DTOs.Invoices;

public interface IInvoiceRepository
{
    Task<GenerateInvoiceResult> GenerateForQuotationAsync(int quotationId, int requestingUserId, string requestingRole);
    Task<GenerateInvoiceResult> GenerateSystemAsync(int quotationId);
    Task<InvoiceDto?> GetByIdAsync(int invoiceId, int userId, string role);
    Task<(IEnumerable<InvoiceListItemDto> Invoices, int TotalCount)> GetAllForUserAsync(
        int userId, string role, int page, int pageSize);
}
