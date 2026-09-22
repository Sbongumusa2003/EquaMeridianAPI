public class Document
{
    public int DocID { get; set; }
    public int DocTypeID { get; set; }
    public string DocName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
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