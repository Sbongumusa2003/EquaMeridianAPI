using EquaMeridian.DTOs.Reviews;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static EquaMeridian.DTOs.Reviews.ReviewDto;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _db;
    private readonly IContentModerationService _moderation;
    private readonly IAuditService _audit;

    public ReviewRepository(AppDbContext db, IContentModerationService moderation, IAuditService audit)
    {
        _db = db;
        _moderation = moderation;
        _audit = audit;
    }

 
    public async Task<ReviewActionResult> CreateAsync(int contractorId, CreateReviewDto dto)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .FirstOrDefaultAsync(b => b.BookingID == dto.BookingID && b.ContractorID == contractorId);

        if (booking == null)
            return new ReviewActionResult { Success = false, ErrorType = "NotFound", Error = "Booking not found." };

        if (booking.Status != "Completed")
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "Conflict",
                Error = "A review can only be left for a completed booking."
            };

        var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.BookingID == dto.BookingID);
        if (alreadyReviewed)
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "Conflict",
                Error = "A review already exists for this booking. You can edit it from My Reviews."
            };

        var validationError = await ValidateContent(
            dto.OverallRating, dto.AspectRatings, dto.Title, dto.ReviewText,
            contractorId, dto.BookingID, null);
        if (validationError != null)
            return new ReviewActionResult { Success = false, ErrorType = "Validation", Error = validationError };

        if (!dto.ConfirmedGenuine)
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "Validation",
                Error = "Please confirm the review reflects your genuine experience."
            };

        var review = new Review
        {
            BookingID = booking.BookingID,
            MachineryID = booking.ListingID,
            SupplierID = booking.SupplierID,
            ContractorID = contractorId,
            OverallRating = dto.OverallRating,
            AspectRatingsJson = SerializeAspects(dto.AspectRatings),
            Title = dto.Title,
            ReviewText = dto.ReviewText,
            Status = "Published",
            CreatedAt = AppTime.Now
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        await RecalculateListingAggregateAsync(booking.ListingID);

        var contractor = await _db.Users.FirstOrDefaultAsync(u => u.UserID == contractorId);

        return new ReviewActionResult
        {
            Success = true,
            Review = MapToDto(review, contractor?.FullName ?? string.Empty, booking.Listing.ListingTitle, editWindowDays: null),
            SupplierID = booking.SupplierID,
            SupplierEmail = booking.Supplier.Email,
            SupplierName = booking.Supplier.FullName,
            Machinery = booking.Listing.ListingTitle
        };
    }

    public async Task<ReviewsPageDto> GetForListingAsync(
        int listingId, int page, int pageSize, string? sortBy, int? starFilter)
    {
        var summaryRows = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.MachineryID == listingId && r.Status == "Published")
            .Select(r => new { r.OverallRating, r.AspectRatingsJson })
            .ToListAsync();
        var summary = BuildSummary(summaryRows.Select(r => (r.OverallRating, r.AspectRatingsJson)));

        var q = _db.Reviews
            .AsNoTracking()
            .Where(r => r.MachineryID == listingId && r.Status == "Published");

        if (starFilter.HasValue)
            q = q.Where(r => r.OverallRating == starFilter.Value);

        q = (sortBy ?? "recent").ToLowerInvariant() switch
        {
            "highest" => q.OrderByDescending(r => r.OverallRating).ThenByDescending(r => r.CreatedAt),
            "lowest" => q.OrderBy(r => r.OverallRating).ThenByDescending(r => r.CreatedAt),
            _ => q.OrderByDescending(r => r.CreatedAt)
        };

        var total = await q.CountAsync();
        var pageItems = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new { Review = r, ContractorName = r.Contractor.FullName, ListingTitle = r.Machinery.ListingTitle })
            .ToListAsync();

        return new ReviewsPageDto
        {
            Summary = summary,
            Reviews = pageItems
                .Select(x => MapToDto(x.Review, x.ContractorName, x.ListingTitle, editWindowDays: null))
                .ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ReviewDto>> GetMineAsync(int contractorId, int editWindowDays)
    {
        var reviews = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.Machinery)
            .Where(r => r.ContractorID == contractorId && r.Status == "Published")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var contractor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == contractorId);

        return reviews.Select(r =>
            MapToDto(r, contractor?.FullName ?? string.Empty, r.Machinery.ListingTitle, editWindowDays));
    }

    public async Task<ReviewActionResult> UpdateAsync(
        int reviewId, int contractorId, UpdateReviewDto dto, int editWindowDays)
    {
        var review = await _db.Reviews
            .Include(r => r.Machinery)
            .Include(r => r.Supplier)
            .Include(r => r.Contractor)
            .FirstOrDefaultAsync(r => r.ReviewID == reviewId && r.ContractorID == contractorId);

        if (review == null)
            return new ReviewActionResult { Success = false, ErrorType = "NotFound", Error = "Review not found." };

        if (review.Status != "Published")
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "Conflict",
                Error = "This review can no longer be edited."
            };

        if ((AppTime.Now - review.CreatedAt).TotalDays > editWindowDays)
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "Conflict",
                Error = "The edit window for this review has closed."
            };

        var validationError = await ValidateContent(
            dto.OverallRating, dto.AspectRatings, dto.Title, dto.ReviewText,
            contractorId, null, reviewId);
        if (validationError != null)
            return new ReviewActionResult { Success = false, ErrorType = "Validation", Error = validationError };

        review.OverallRating = dto.OverallRating;
        review.AspectRatingsJson = SerializeAspects(dto.AspectRatings);
        review.Title = dto.Title;
        review.ReviewText = dto.ReviewText;
        review.IsEdited = true;
        review.EditedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        await RecalculateListingAggregateAsync(review.MachineryID);

        return new ReviewActionResult
        {
            Success = true,
            Review = MapToDto(review, review.Contractor.FullName, review.Machinery.ListingTitle, editWindowDays),
            SupplierID = review.SupplierID,
            SupplierEmail = review.Supplier.Email,
            SupplierName = review.Supplier.FullName,
            Machinery = review.Machinery.ListingTitle
        };
    }

    public async Task<ReviewActionResult> DeleteAsync(int reviewId, int contractorId)
    {
        var review = await _db.Reviews
            .Include(r => r.Machinery)
            .Include(r => r.Supplier)
            .FirstOrDefaultAsync(r => r.ReviewID == reviewId && r.ContractorID == contractorId);

        if (review == null || review.Status != "Published")
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "NotFound",
                Error = "This review could not be found or has already been removed."
            };

        review.Status = "Deleted";
        review.DeletedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        await RecalculateListingAggregateAsync(review.MachineryID);

        return new ReviewActionResult
        {
            Success = true,
            SupplierID = review.SupplierID,
            SupplierEmail = review.Supplier.Email,
            SupplierName = review.Supplier.FullName,
            Machinery = review.Machinery.ListingTitle
        };
    }

    public async Task<AdminReviewsPageDto> GetAllForAdminAsync(
        int page, int pageSize, string? status, int? listingId, int? starFilter, string? search)
    {
        var q = _db.Reviews.AsNoTracking().AsQueryable();

        q = string.IsNullOrWhiteSpace(status) ? q.Where(r => r.Status != "Deleted") : q.Where(r => r.Status == status);

        if (listingId.HasValue) q = q.Where(r => r.MachineryID == listingId.Value);
        if (starFilter.HasValue) q = q.Where(r => r.OverallRating == starFilter.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(r => r.Title.Contains(term) || r.ReviewText.Contains(term)
                           || r.Contractor.FullName.Contains(term) || r.Machinery.ListingTitle.Contains(term));
        }

        q = q.OrderByDescending(r => r.CreatedAt);

        var total = await q.CountAsync();
        var pageItems = await q
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                Review = r,
                ContractorName = r.Contractor.FullName,
                SupplierName = r.Supplier.FullName,
                ListingTitle = r.Machinery.ListingTitle
            })
            .ToListAsync();

        return new AdminReviewsPageDto
        {
            Reviews = pageItems.Select(x =>
            {
                var dto = MapToDto(x.Review, x.ContractorName, x.ListingTitle, editWindowDays: null);
                dto.Status = x.Review.Status;
                dto.SupplierDisplayName = x.SupplierName;
                return dto;
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReviewActionResult> AdminDeleteAsync(int reviewId, int adminId, string reason)
    {
        var review = await _db.Reviews
            .Include(r => r.Machinery)
            .Include(r => r.Supplier)
            .FirstOrDefaultAsync(r => r.ReviewID == reviewId);

        if (review == null || review.Status == "Deleted")
            return new ReviewActionResult
            {
                Success = false,
                ErrorType = "NotFound",
                Error = "This review could not be found or has already been removed."
            };

        review.Status = "Deleted";
        review.DeletedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        await RecalculateListingAggregateAsync(review.MachineryID);

        return new ReviewActionResult
        {
            Success = true,
            ContractorID = review.ContractorID,
            SupplierEmail = review.Supplier.Email,
            SupplierName = review.Supplier.FullName,
            Machinery = review.Machinery.ListingTitle
        };
    }

    private async Task RecalculateListingAggregateAsync(int listingId)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.ListingID == listingId);
        if (listing == null) return;

        var published = await _db.Reviews
            .Where(r => r.MachineryID == listingId && r.Status == "Published")
            .Select(r => r.OverallRating)
            .ToListAsync();

        listing.ReviewCount = published.Count;
        listing.AverageRating = published.Count == 0
            ? 0m
            : Math.Round((decimal)published.Average(), 2);

        await _db.SaveChangesAsync();
    }

    private static string? ValidateFields(int overallRating, Dictionary<string, int>? aspectRatings, string title, string reviewText)
    {
        if (overallRating is < 1 or > 5)
            return "Please select an overall rating between 1 and 5 stars.";

        if (string.IsNullOrWhiteSpace(title))
            return "A review title is required.";

        if (string.IsNullOrWhiteSpace(reviewText))
            return "Review text is required.";

        if (reviewText.Length > 500)
            return "Review text must not exceed 500 characters.";

        if (aspectRatings != null && aspectRatings.Values.Any(v => v is < 1 or > 5))
            return "Aspect ratings must be between 1 and 5.";

        return null;
    }

    private async Task<string?> ValidateContent(
        int overallRating, Dictionary<string, int>? aspectRatings, string title, string reviewText,
        int contractorId, int? bookingId, int? reviewId)
    {
        var fieldError = ValidateFields(overallRating, aspectRatings, title, reviewText);
        if (fieldError != null) return fieldError;

        if (!await _moderation.IsCleanAsync(title, reviewText))
        {
            await _audit.LogAsync(contractorId, "Review_Flagged_Profanity",
                bookingId.HasValue
                    ? $"Review submission for booking #{bookingId} was blocked and flagged: contains blocked language."
                    : $"Edit to review #{reviewId} was blocked and flagged: contains blocked language.",
                null, null, null, null);

            return "Your review could not be published because it contains language that isn't allowed. It has been flagged and was not posted.";
        }

        return null;
    }

    private static string? SerializeAspects(Dictionary<string, int>? aspectRatings)
    {
        if (aspectRatings == null || aspectRatings.Count == 0) return null;

        var recognised = aspectRatings
            .Where(kv => ReviewAspects.All.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return recognised.Count == 0 ? null : JsonSerializer.Serialize(recognised);
    }

    private static Dictionary<string, int> DeserializeAspects(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, int>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }

    private static RatingSummaryDto BuildSummary(IEnumerable<(int OverallRating, string? AspectRatingsJson)> publishedReviews)
    {
        var reviews = publishedReviews as ICollection<(int OverallRating, string? AspectRatingsJson)>
            ?? publishedReviews.ToList();

        var summary = new RatingSummaryDto
        {
            ReviewCount = reviews.Count,
            AverageRating = reviews.Count == 0
                ? 0
                : Math.Round(reviews.Average(r => r.OverallRating), 2),
            StarDistribution = Enumerable.Range(1, 5).ToDictionary(s => s, _ => 0)
        };

        foreach (var r in reviews)
            summary.StarDistribution[r.OverallRating]++;

        var aspectValues = new Dictionary<string, List<int>>();
        foreach (var r in reviews)
        {
            foreach (var kv in DeserializeAspects(r.AspectRatingsJson))
            {
                if (!aspectValues.TryGetValue(kv.Key, out var list))
                    aspectValues[kv.Key] = list = new List<int>();
                list.Add(kv.Value);
            }
        }

        summary.AspectAverages = aspectValues.ToDictionary(
            kv => kv.Key, kv => Math.Round(kv.Value.Average(), 2));

        return summary;
    }

    private static ReviewDto MapToDto(Review r, string reviewerName, string listingTitle, int? editWindowDays)
    {
        var dto = new ReviewDto
        {
            ReviewID = r.ReviewID,
            BookingID = r.BookingID,
            MachineryID = r.MachineryID,
            ListingTitle = listingTitle,
            ContractorID = r.ContractorID,
            ReviewerDisplayName = reviewerName,
            OverallRating = r.OverallRating,
            AspectRatings = DeserializeAspects(r.AspectRatingsJson),
            Title = r.Title,
            ReviewText = r.ReviewText,
            IsEdited = r.IsEdited,
            CreatedAt = r.CreatedAt,
            EditedAt = r.EditedAt
        };

        if (editWindowDays.HasValue)
        {
            var daysElapsed = (AppTime.Now - r.CreatedAt).TotalDays;
            var remaining = editWindowDays.Value - (int)Math.Floor(daysElapsed);
            dto.CanEdit = remaining > 0;
            dto.CanDelete = true;
            dto.EditWindowDaysRemaining = Math.Max(0, remaining);
        }

        return dto;
    }
}
