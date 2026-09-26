using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Documents
{
    public class DocumentReviewListItemDto
    {
        public int DocID { get; set; }
        public string DocName { get; set; } = string.Empty;
        public int DocTypeID { get; set; }
        public string DocTypeName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int UploadedByUserID { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string? RejectionReason { get; set; }
    }
    public class DocumentReviewDetailDto : DocumentReviewListItemDto
    {
        public string FilePath { get; set; } = string.Empty;
    }
    public class ReviewDocumentDto
    {
        [Required]
        public string Decision { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    public class RequiredDocumentStatusDto
    {
        public int DocTypeID { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public string Status { get; set; } = "Missing"; 
        public int? DocID { get; set; }
    }

    public class DocumentReviewResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DocumentReviewDetailDto? Document { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int OwnerID { get; set; }

        public bool AccountActivated { get; set; }
    }

    /// <summary>
    /// Lightweight DTO returned to the owning user for "My Documents".
    /// Avoids serializing EF navigation properties (User / DocType).
    /// </summary>
    public class UserDocumentDto
    {
        public int DocID { get; set; }
        public int DocTypeID { get; set; }
        public string DocName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
        public string? RejectionReason { get; set; }
    }
}
