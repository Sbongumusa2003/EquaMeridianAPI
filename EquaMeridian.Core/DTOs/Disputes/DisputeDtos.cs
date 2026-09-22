using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Disputes
{
    public class DisputeListItemDto
    {
        public int DisputeID { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int? ListingID { get; set; }
        public string? ListingTitle { get; set; }
        public string ReasonCategory { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RaisedDate { get; set; }
    }

    public class DisputeDetailDto : DisputeListItemDto
    {
        public string ContractorContact { get; set; } = string.Empty;
        public string SupplierContact { get; set; } = string.Empty;
        public int? BookingID { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public string ComplaintDescription { get; set; } = string.Empty;
        public string? DesiredResolution { get; set; }
        public List<string> EvidenceUrls { get; set; } = new();
        public string? ResolutionType { get; set; }
        public string? ResolutionNotes { get; set; }
        public DateTime? ResolvedDate { get; set; }
    }

    public class ResolveDisputeDto
    {
        [Required]
        public string ResolutionOutcome { get; set; } = string.Empty;

        [Required]
        public string ResolutionNotes { get; set; } = string.Empty;

        public decimal? RefundAmount { get; set; }
    }

    public class DisputeResolutionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DisputeDetailDto? Dispute { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public int? CreatedRefundId { get; set; }
        public int ContractorID { get; set; }
        public string ContractorEmail { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }

    public class RaiseDisputeDto
    {
        [Required]
        public string DisputeCategory { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string DesiredResolution { get; set; } = string.Empty;
    }

    public class RaiseDisputeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DisputeDetailDto? Dispute { get; set; }
        public int? RespondentID { get; set; }
        public string RespondentEmail { get; set; } = string.Empty;
        public string RespondentName { get; set; } = string.Empty;
        public string ComplainantName { get; set; } = string.Empty;
    }
}
