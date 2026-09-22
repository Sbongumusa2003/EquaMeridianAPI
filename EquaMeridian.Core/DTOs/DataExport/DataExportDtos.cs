using System.Linq;
using System.Xml.Serialization;

namespace EquaMeridian.DTOs.DataExport
{
    [XmlRoot("Listing")]
    public class ListingExportDto
    {
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string AvailabilityStatus { get; set; } = string.Empty;
        public string? MakeBrand { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? Location { get; set; }
        public decimal DailyRateZAR { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal AverageRating { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    [XmlRoot("Booking")]
    public class BookingExportDto
    {
        public int BookingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    [XmlRoot("ListingExport")]
    public class ListingExportWrapper
    {
        [XmlElement("Listing")]
        public List<ListingExportDto> Listings { get; set; } = new();
    }

    [XmlRoot("BookingExport")]
    public class BookingExportWrapper
    {
        [XmlElement("Booking")]
        public List<BookingExportDto> Bookings { get; set; } = new();
    }

    [XmlRoot("User")]
    public class UserExportDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string AccountStatus { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    [XmlRoot("UserExport")]
    public class UserExportWrapper
    {
        [XmlElement("User")]
        public List<UserExportDto> Users { get; set; } = new();
    }

    [XmlRoot("FinancialRecord")]
    public class FinancialExportDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public decimal SupplierPayableAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    [XmlRoot("FinancialExport")]
    public class FinancialExportWrapper
    {
        [XmlElement("FinancialRecord")]
        public List<FinancialExportDto> Records { get; set; } = new();
    }
    public static class DataExportReasons
    {
        public static readonly string[] Allowed =
        {
            "RegulatoryAudit", "FinancialReconciliation", "DisputeInvestigation", "Other"
        };

        public static bool IsValid(string? reason) => reason != null && Allowed.Contains(reason);
    }
}
