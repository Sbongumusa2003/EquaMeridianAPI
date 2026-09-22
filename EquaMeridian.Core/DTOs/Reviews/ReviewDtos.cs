using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Reviews
{
    public static class ReviewAspects
    {
        public const string MachineryCondition = "MachineryCondition";
        public const string Reliability = "Reliability";
        public const string Communication = "Communication";
        public const string ValueForMoney = "ValueForMoney";

        public static readonly HashSet<string> All = new()
        {
            MachineryCondition, Reliability, Communication, ValueForMoney
        };
    }
    public class CreateReviewDto
    {
        [Required]
        public int BookingID { get; set; }

        [Required]
        [Range(1, 5)]
        public int OverallRating { get; set; }
        public Dictionary<string, int>? AspectRatings { get; set; }

        [Required]
        [StringLength(150, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string ReviewText { get; set; } = string.Empty;
        [Required]
        public bool ConfirmedGenuine { get; set; }
    }

    public class UpdateReviewDto
    {
        [Required]
        [Range(1, 5)]
        public int OverallRating { get; set; }

        public Dictionary<string, int>? AspectRatings { get; set; }

        [Required]
        [StringLength(150, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string ReviewText { get; set; } = string.Empty;
    }

    public class ReviewDto
    {
        public int ReviewID { get; set; }
        public int BookingID { get; set; }
        public int MachineryID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public int ContractorID { get; set; }
        public string ReviewerDisplayName { get; set; } = string.Empty;
        public int OverallRating { get; set; }
        public Dictionary<string, int> AspectRatings { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string ReviewText { get; set; } = string.Empty;
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public int EditWindowDaysRemaining { get; set; }
        public string Status { get; set; } = "Published";
        public string SupplierDisplayName { get; set; } = string.Empty;

        public class RatingSummaryDto
        {
            public double AverageRating { get; set; }
            public int ReviewCount { get; set; }
            public Dictionary<int, int> StarDistribution { get; set; } = new();
            public Dictionary<string, double> AspectAverages { get; set; } = new();
        }
        public class ReviewsPageDto
        {
            public RatingSummaryDto Summary { get; set; } = new();
            public IEnumerable<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }

        public class ReviewActionResult
        {
            public bool Success { get; set; }
            public string? ErrorType { get; set; }
            public string? Error { get; set; }

            public ReviewDto? Review { get; set; }
            public int ContractorID { get; set; }
            public int SupplierID { get; set; }
            public string SupplierEmail { get; set; } = string.Empty;
            public string SupplierName { get; set; } = string.Empty;
            public string Machinery { get; set; } = string.Empty;
        }

        public class AdminReviewsPageDto
        {
            public IEnumerable<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }
    }

    public class AdminDeleteReviewDto
    {
        [Required]
        [StringLength(300, MinimumLength = 1)]
        public string Reason { get; set; } = string.Empty;
    }
}