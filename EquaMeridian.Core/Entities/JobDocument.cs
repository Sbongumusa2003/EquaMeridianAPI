public class JobDocument
{
    public int JobDocumentID { get; set; }
    public int LeaseAgreementID { get; set; }
    public LeaseAgreement LeaseAgreement { get; set; } = null!;
    public string DocumentType { get; set; } = "Other";
    public string DocumentName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int UploadedByUserID { get; set; }
    public User UploadedByUser { get; set; } = null!;
    public DateTime UploadedDate { get; set; } = AppTime.Now;
    public string Status { get; set; } = "Active";
}
