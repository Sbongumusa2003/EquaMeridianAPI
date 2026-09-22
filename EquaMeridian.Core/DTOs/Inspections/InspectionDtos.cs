using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Inspections
{
    public class InspectionListItemDto
    {
        public int InspectionID { get; set; }
        public int ListingID { get; set; }
        public string MachineryTitle { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Outcome { get; set; }
    }
    public class MachineryOptionDto
    {
        public int ListingID { get; set; }
        public string MachineryTitle { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class RequestInspectionDto
    {
        [Required]
        public int ListingID { get; set; }

        [Required]
        public DateTime ScheduledDate { get; set; }
    }

    public class InspectionRequestResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public InspectionListItemDto? Inspection { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }

    public class InspectionOutcomeDto : InspectionListItemDto
    {
        public string? Notes { get; set; }
    }

    public class ConfirmOutcomeDto
    {
        [Required]
        public string Outcome { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    public class InspectionConfirmResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public InspectionOutcomeDto? Inspection { get; set; }
        public int RequesterID { get; set; }
        public int SupplierID { get; set; }
        public string RequesterEmail { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }
}
