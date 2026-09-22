using EquaMeridian.DTOs.JobDocuments;
using Microsoft.AspNetCore.Http;

public interface IJobDocumentRepository
{
    Task<JobDocumentsPageDto?> GetForAgreementAsync(
        int leaseAgreementId, int userId, string role, string? documentType, string? search);
    Task<JobDocument?> GetDocumentAsync(int jobDocumentId, int userId, string role);
    Task<JobDocumentDto?> UploadAsync(int leaseAgreementId, int userId, string role,
        string documentType, IFormFile file);
}
