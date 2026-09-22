using EquaMeridian.Core.Validation;
using EquaMeridian.DTOs.Documents;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class DocumentRepository : IDocumentRepository
{
    private static readonly HashSet<string> ValidDecisions = new() { "Accepted", "Rejected" };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DocumentRepository(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IEnumerable<Document>> GetByUserAsync(int userId)
        => await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserID == userId)
            .ToListAsync();

    public async Task<int> UploadAsync(int userId, int docTypeId, IFormFile file)
    {
        var validationError = DocumentUploadPolicy.Validate(file);
        if (validationError != null)
            throw new ArgumentException(validationError);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var uploadPath = Path.Combine(
            _env.ContentRootPath, "uploads", "documents", userId.ToString());

        Directory.CreateDirectory(uploadPath);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(fullPath))
            await file.CopyToAsync(stream);

        var relativeUrl = $"/uploads/documents/{userId}/{fileName}";

        var doc = new Document
        {
            DocTypeID = docTypeId,
            DocName = file.FileName,
            FilePath = relativeUrl,
            VerificationStatus = "Pending",
            UserID = userId
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        return doc.DocID;
    }

    public async Task<(bool Success, string Message)> ReplaceAsync(
        int userId, int docId, IFormFile file)
    {
        var validationError = DocumentUploadPolicy.Validate(file);
        if (validationError != null)
            return (false, validationError);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        var doc = await _db.Documents
            .FirstOrDefaultAsync(d => d.DocID == docId && d.UserID == userId);

        if (doc == null) return (false, "Not found.");

        var uploadPath = Path.Combine(
            _env.ContentRootPath, "uploads", "documents", userId.ToString());
        Directory.CreateDirectory(uploadPath);

        var oldFullPath = Path.Combine(_env.ContentRootPath, doc.FilePath.TrimStart('/'));
        if (File.Exists(oldFullPath))
            File.Delete(oldFullPath);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(fullPath))
            await file.CopyToAsync(stream);

        doc.DocName = file.FileName;
        doc.FilePath = $"/uploads/documents/{userId}/{fileName}";
        doc.VerificationStatus = "Pending";
        doc.UploadedDate = AppTime.Now;
        await _db.SaveChangesAsync();

        return (true, "Document successfully replaced.");
    }

    public async Task<(IEnumerable<DocumentReviewListItemDto>, int)> GetForAdminReviewAsync(
        string? status, int? userId, int page, int pageSize)
    {
        var q = _db.Documents
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.DocType)
            .AsQueryable();

        if (status != null)
            q = q.Where(d => d.VerificationStatus == status);

        if (userId.HasValue)
            q = q.Where(d => d.UserID == userId.Value);

        var total = await q.CountAsync();
        var docs = await q
            .OrderBy(d => d.UploadedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (docs.Select(MapToListItemDto), total);
    }

    public async Task<DocumentReviewDetailDto?> GetByIdForAdminAsync(int docId)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.DocType)
            .FirstOrDefaultAsync(d => d.DocID == docId);

        return doc == null ? null : MapToDetailDto(doc);
    }

    public async Task<DocumentReviewResult> ReviewAsync(int docId, ReviewDocumentDto dto, int adminId)
    {
        if (!ValidDecisions.Contains(dto.Decision))
            return new DocumentReviewResult { Success = false, Error = "Decision must be Accepted or Rejected." };

        if (dto.Decision == "Rejected" && string.IsNullOrWhiteSpace(dto.Reason))
            return new DocumentReviewResult { Success = false, Error = "Please provide a reason for rejecting this document." };

        var doc = await _db.Documents
            .Include(d => d.User)
            .Include(d => d.DocType)
            .FirstOrDefaultAsync(d => d.DocID == docId);

        if (doc == null)
            return new DocumentReviewResult { Success = false, Error = "Document not found." };

        if (doc.VerificationStatus != "Pending")
            return new DocumentReviewResult
            {
                Success = false,
                Error = $"Document cannot be reviewed because its current status is '{doc.VerificationStatus}'."
            };

        doc.VerificationStatus = dto.Decision;
        doc.VerifiedByUserID = adminId;
        doc.VerifiedDate = AppTime.Now;
        doc.RejectionReason = dto.Decision == "Rejected" ? dto.Reason!.Trim() : null;

        var accountActivated = false;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            if (dto.Decision == "Accepted"
                && (doc.User.Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
                    || doc.User.Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
                && doc.User.AccountStatus == "Pending")
            {
                await _db.SaveChangesAsync();

                if (await AreRequiredDocumentsApprovedAsync(doc.User.UserID, doc.User.Role))
                {
                    doc.User.AccountStatus = "Active";
                    accountActivated = true;
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        });

        return new DocumentReviewResult
        {
            Success = true,
            Document = MapToDetailDto(doc),
            OwnerEmail = doc.User.Email,
            OwnerName = doc.User.FullName,
            OwnerID = doc.UserID,
            AccountActivated = accountActivated
        };
    }

    public async Task<bool> HasApprovedDocumentAsync(int userId)
        => await _db.Documents
            .AsNoTracking()
            .AnyAsync(d => d.UserID == userId && d.VerificationStatus == "Accepted");

    public async Task<bool> AreRequiredDocumentsApprovedAsync(int userId, string role)
    {
        var requiredTypeIds = await GetRequiredDocTypeIdsForRoleAsync(role);
        if (requiredTypeIds.Count == 0) return true;

        var approvedTypeIds = await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserID == userId && d.VerificationStatus == "Accepted")
            .Select(d => d.DocTypeID)
            .Distinct()
            .ToListAsync();

        return requiredTypeIds.All(t => approvedTypeIds.Contains(t));
    }

    public async Task<List<RequiredDocumentStatusDto>> GetDocumentChecklistAsync(int userId, string role)
    {
        var types = await GetDocTypesForRoleAsync(role);

        var userDocs = await _db.Documents
            .AsNoTracking()
            .Where(d => d.UserID == userId)
            .OrderByDescending(d => d.UploadedDate)
            .ToListAsync();

        var result = new List<RequiredDocumentStatusDto>();
        foreach (var type in types)
        {
            var doc = userDocs.FirstOrDefault(d => d.DocTypeID == type.DocTypeID);
            result.Add(new RequiredDocumentStatusDto
            {
                DocTypeID = type.DocTypeID,
                TypeName = type.TypeName,
                IsRequired = type.IsRequired,
                Status = doc?.VerificationStatus ?? "Missing",
                DocID = doc?.DocID
            });
        }

        return result;
    }

    private async Task<List<int>> GetRequiredDocTypeIdsForRoleAsync(string role)
        => (await GetDocTypesForRoleAsync(role))
            .Where(t => t.IsRequired)
            .Select(t => t.DocTypeID)
            .ToList();

    private async Task<List<DocumentType>> GetDocTypesForRoleAsync(string role)
        => await _db.DocumentTypes
            .AsNoTracking()
            .Where(t => t.AppliesToRole == null || t.AppliesToRole == role)
            .ToListAsync();

    private static DocumentReviewListItemDto MapToListItemDto(Document d) => new()
    {
        DocID = d.DocID,
        DocName = d.DocName,
        DocTypeID = d.DocTypeID,
        DocTypeName = d.DocType.TypeName,
        IsRequired = d.DocType.IsRequired,
        UploadedByUserID = d.UserID,
        UploadedByName = d.User.FullName,
        VerificationStatus = d.VerificationStatus,
        UploadedDate = d.UploadedDate,
        RejectionReason = d.RejectionReason
    };

    private static DocumentReviewDetailDto MapToDetailDto(Document d) => new()
    {
        DocID = d.DocID,
        DocName = d.DocName,
        DocTypeID = d.DocTypeID,
        DocTypeName = d.DocType.TypeName,
        IsRequired = d.DocType.IsRequired,
        UploadedByUserID = d.UserID,
        UploadedByName = d.User.FullName,
        VerificationStatus = d.VerificationStatus,
        UploadedDate = d.UploadedDate,
        RejectionReason = d.RejectionReason,
        FilePath = d.FilePath
    };
}