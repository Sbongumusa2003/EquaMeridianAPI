namespace EquaMeridian.DTOs.JobDocuments
{
    public class JobDocumentDto
    {
        public int JobDocumentID { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public long FileSizeBytes { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CanDelete { get; set; }
    }
    public class JobDocumentsPageDto
    {
        public int LeaseAgreementID { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public List<JobDocumentDto> InspectionReports { get; set; } = new();
        public List<JobDocumentDto> JobSitePhotos { get; set; } = new();
        public List<JobDocumentDto> DeliveryDocumentation { get; set; } = new();
        public List<JobDocumentDto> MaintenanceRecords { get; set; } = new();
        public List<JobDocumentDto> OtherDocuments { get; set; } = new();
    }
}
