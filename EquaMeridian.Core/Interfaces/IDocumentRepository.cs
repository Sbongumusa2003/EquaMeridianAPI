using EquaMeridian.DTOs.Documents;
using Microsoft.AspNetCore.Http;

public interface IDocumentRepository
{
    Task<IEnumerable<Document>> GetByUserAsync(int userId);
    Task<int> UploadAsync(int userId, int docTypeId, IFormFile file);
    Task<(bool Success, string Message)> ReplaceAsync(int userId, int docId, IFormFile file);
    Task<(IEnumerable<DocumentReviewListItemDto> Documents, int TotalCount)> GetForAdminReviewAsync(
        string? status, int? userId, int page, int pageSize);
    Task<DocumentReviewDetailDto?> GetByIdForAdminAsync(int docId);
    Task<DocumentReviewResult> ReviewAsync(int docId, ReviewDocumentDto dto, int adminId);
    Task<bool> HasApprovedDocumentAsync(int userId);
    Task<bool> AreRequiredDocumentsApprovedAsync(int userId, string role);
    Task<List<RequiredDocumentStatusDto>> GetDocumentChecklistAsync(int userId, string role);
}