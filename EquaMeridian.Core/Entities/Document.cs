public class Document
{
    public int DocID { get; set; }
    public int DocTypeID { get; set; }
    public string DocName { get; set; } = string.Empty;

    /// <summary>
    /// Logical/public path, e.g. /api/admin/documents/{DocID}/file.
    /// Kept for API compatibility; actual bytes live in Content so files survive Render redeploys.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>File binary stored in PostgreSQL (durable across Render restarts).</summary>
    public byte[]? Content { get; set; }

    /// <summary>MIME type, e.g. application/pdf, image/png, image/jpeg.</summary>
    public string? ContentType { get; set; }

    public string VerificationStatus { get; set; } = "Pending";
    public int UserID { get; set; }
    public DateTime UploadedDate { get; set; } = AppTime.Now;
    public User User { get; set; } = null!;
    public DocumentType DocType { get; set; } = null!;
    public int? VerifiedByUserID { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? RejectionReason { get; set; }
}

public class DocumentType
{
    public int DocTypeID { get; set; }
    public string TypeName { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public string? AppliesToRole { get; set; }
}