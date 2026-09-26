using EquaMeridian.DTOs.Documents;
using Microsoft.AspNetCore.Http;

public interface IDocumentRepository
{
    Task<IEnumerable<Document>> GetByUserAsync(int userId);
    Task<IEnumerable<UserDocumentDto>> GetByUserDtoAsync(int userId);
    Task<int> UploadAsync(int userId, int docTypeId, IFormFile file);
    /// <summary>
    /// Writes the file to disk and builds a Document entity without calling SaveChanges.
    /// Use inside an existing EF transaction (e.g. supplier registration) so a single
    /// SaveChanges can commit user + documents together.
    /// </summary>
    Task<(Document Entity, string FullPath)> PrepareUploadAsync(int userId, int docTypeId, IFormFile file);
    Task<(bool Success, string Message)> ReplaceAsync(int userId, int docId, IFormFile file);
    Task<(IEnumerable<DocumentReviewListItemDto> Documents, int TotalCount)> GetForAdminReviewAsync(
        string? status, int? userId, int page, int pageSize);
    Task<DocumentReviewDetailDto?> GetByIdForAdminAsync(int docId);
    Task<DocumentReviewResult> ReviewAsync(int docId, ReviewDocumentDto dto, int adminId);
    Task<bool> HasApprovedDocumentAsync(int userId);
    Task<bool> AreRequiredDocumentsApprovedAsync(int userId, string role);
    Task<List<RequiredDocumentStatusDto>> GetDocumentChecklistAsync(int userId, string role);

    /// <summary>
    /// Returns the stored file bytes for admin viewing/download, regardless of owner.
    /// Falls back to reading from disk (ContentRoot + FilePath) for legacy rows with no Content.
    /// </summary>
    Task<(byte[] Content, string ContentType, string DocName)?> GetContentForAdminAsync(int docId);

    /// <summary>
    /// Same as <see cref="GetContentForAdminAsync"/> but scoped to the owning user, for
    /// the supplier's own "my documents" view.
    /// </summary>
    Task<(byte[] Content, string ContentType, string DocName)?> GetContentForOwnerAsync(int userId, int docId);
}