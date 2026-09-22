using EquaMeridian.DTOs.JobDocuments;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class JobDocumentRepository : IJobDocumentRepository
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx" };
    private static readonly HashSet<string> ValidDocumentTypes = new()
    {
        "InspectionReport", "JobSitePhoto", "DeliveryDocumentation", "MaintenanceRecord", "Other"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public JobDocumentRepository(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<JobDocumentsPageDto?> GetForAgreementAsync(
        int leaseAgreementId, int userId, string role, string? documentType, string? search)
    {
        var agreement = await _db.LeaseAgreements.FirstOrDefaultAsync(a => a.LeaseAgreementID == leaseAgreementId);
        if (agreement == null) return null;

        if (!HasAccess(agreement, userId, role)) return null;
        if (agreement.Status != "Active" && agreement.Status != "Completed") return null;

        var q = _db.JobDocuments
            .AsNoTracking()
            .Include(d => d.UploadedByUser)
            .Where(d => d.LeaseAgreementID == leaseAgreementId && d.Status == "Active")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(documentType) && ValidDocumentTypes.Contains(documentType))
            q = q.Where(d => d.DocumentType == documentType);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => d.DocumentName.Contains(search));

        var docs = await q.OrderByDescending(d => d.UploadedDate).ToListAsync();

        var page = new JobDocumentsPageDto
        {
            LeaseAgreementID = agreement.LeaseAgreementID,
            AgreementNumber = agreement.AgreementNumber
        };

        var canDelete = role.Equals("admin", StringComparison.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            var dto = new JobDocumentDto
            {
                JobDocumentID = doc.JobDocumentID,
                DocumentType = doc.DocumentType,
                DocumentName = doc.DocumentName,
                UploadedByName = doc.UploadedByUser.FullName,
                UploadedDate = doc.UploadedDate,
                FileSizeBytes = doc.FileSizeBytes,
                Status = doc.Status,
                CanDelete = canDelete || doc.UploadedByUserID == userId
            };

            switch (doc.DocumentType)
            {
                case "InspectionReport": page.InspectionReports.Add(dto); break;
                case "JobSitePhoto": page.JobSitePhotos.Add(dto); break;
                case "DeliveryDocumentation": page.DeliveryDocumentation.Add(dto); break;
                case "MaintenanceRecord": page.MaintenanceRecords.Add(dto); break;
                default: page.OtherDocuments.Add(dto); break;
            }
        }

        return page;
    }

    public async Task<JobDocument?> GetDocumentAsync(int jobDocumentId, int userId, string role)
    {
        var doc = await _db.JobDocuments
            .Include(d => d.LeaseAgreement)
            .FirstOrDefaultAsync(d => d.JobDocumentID == jobDocumentId && d.Status == "Active");

        if (doc == null) return null;

        return HasAccess(doc.LeaseAgreement, userId, role) ? doc : null;
    }

    public async Task<JobDocumentDto?> UploadAsync(
        int leaseAgreementId, int userId, string role, string documentType, IFormFile file)
    {
        var agreement = await _db.LeaseAgreements.FirstOrDefaultAsync(a => a.LeaseAgreementID == leaseAgreementId);
        if (agreement == null || !HasAccess(agreement, userId, role)) return null;

        var type = ValidDocumentTypes.Contains(documentType) ? documentType : "Other";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException("Unsupported file format. Please upload PDF, JPG, PNG, DOCX, or XLSX.");

        var uploadPath = Path.Combine(_env.ContentRootPath, "uploads", "job-documents", leaseAgreementId.ToString());
        Directory.CreateDirectory(uploadPath);

        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadPath, storedFileName);

        using (var stream = File.Create(fullPath))
            await file.CopyToAsync(stream);

        var doc = new JobDocument
        {
            LeaseAgreementID = leaseAgreementId,
            DocumentType = type,
            DocumentName = file.FileName,
            FilePath = fullPath,
            FileSizeBytes = file.Length,
            UploadedByUserID = userId,
            UploadedDate = AppTime.Now,
            Status = "Active"
        };

        _db.JobDocuments.Add(doc);
        await _db.SaveChangesAsync();

        var uploaderName = await _db.Users.Where(u => u.UserID == userId).Select(u => u.FullName).FirstOrDefaultAsync();

        return new JobDocumentDto
        {
            JobDocumentID = doc.JobDocumentID,
            DocumentType = doc.DocumentType,
            DocumentName = doc.DocumentName,
            UploadedByName = uploaderName ?? string.Empty,
            UploadedDate = doc.UploadedDate,
            FileSizeBytes = doc.FileSizeBytes,
            Status = doc.Status,
            CanDelete = true
        };
    }

    private static bool HasAccess(LeaseAgreement agreement, int userId, string role)
    {
        if (role.Equals("admin", StringComparison.OrdinalIgnoreCase)) return true;
        return agreement.ContractorID == userId || agreement.SupplierID == userId;
    }
}
