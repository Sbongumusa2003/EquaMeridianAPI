using EquaMeridian.DTOs.Listings;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class ListingRepository : IListingRepository
{
    private readonly AppDbContext _db;
    public ListingRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<ListingDto>, int)> GetAllAsync(
        string? search, int? category, string? status, int page, int pageSize,
        decimal? minPrice = null, decimal? maxPrice = null, string? location = null,
        int? serviceAreaId = null)
    {
        var q = _db.Listings
            .Include(l => l.Supplier)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(l => l.ListingTitle.Contains(search) ||
                             l.Supplier.FullName.Contains(search));

        if (category.HasValue) q = q.Where(l => l.CategoryID == category.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.AvailabilityStatus.ToLower() == status.ToLower());
        if (minPrice.HasValue) q = q.Where(l => l.DailyRateZAR >= minPrice.Value);
        if (maxPrice.HasValue) q = q.Where(l => l.DailyRateZAR <= maxPrice.Value);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(l => l.Location != null && l.Location.Contains(location));

        if (serviceAreaId.HasValue)
        {
            var areaName = await _db.ServiceAreas.AsNoTracking()
                .Where(s => s.ServiceAreaID == serviceAreaId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();

            var supplierIdsInArea = await _db.Set<UserServiceArea>().AsNoTracking()
                .Where(u => u.ServiceAreaID == serviceAreaId.Value)
                .Select(u => u.UserID)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(areaName))
            {
                q = q.Where(l =>
                    (l.Location != null && l.Location.Contains(areaName))
                    || supplierIdsInArea.Contains(l.SupplierID));
            }
            else if (supplierIdsInArea.Count > 0)
            {
                q = q.Where(l => supplierIdsInArea.Contains(l.SupplierID));
            }
        }

        var total = await q.CountAsync();
        var listings = await q
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        var ids = listings.Select(l => l.ListingID).ToList();
        var imageMap = await BuildImageMapAsync(ids);

        return (listings.Select(l => MapToDto(l, imageMap)), total);
    }

    public async Task<ListingDto?> GetByIdAsync(int id)
    {
        var listing = await _db.Listings
            .Include(l => l.Supplier)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ListingID == id);

        if (listing == null) return null;

        var imageMap = await BuildImageMapAsync(new[] { id });
        return MapToDto(listing, imageMap);
    }

    public async Task<IEnumerable<ListingDto>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        var listings = await _db.Listings
            .Include(l => l.Supplier)
            .Where(l => idList.Contains(l.ListingID))
            .AsNoTracking()
            .ToListAsync();

        var imageMap = await BuildImageMapAsync(idList);
        return listings.Select(l => MapToDto(l, imageMap));
    }

    public async Task<(bool Success, string? Error)> UpdateStatusAsync(int id, string status)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l == null) return (false, "Listing not found.");

        // Admin cannot take a listing offline while a booking process is open.
        if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Suspended", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
        {
            var processError = await GetListingBookingProcessBlockReasonAsync(id);
            if (processError != null)
                return (false, processError);
        }

        l.AvailabilityStatus = status;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(IEnumerable<ListingDto>, int)> GetBySupplierAsync(
        int supplierId, string? status, int page, int pageSize)
    {
        var q = _db.Listings
            .Include(l => l.Supplier)
            .Where(l => l.SupplierID == supplierId)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.AvailabilityStatus.ToLower() == status.ToLower());

        var total = await q.CountAsync();
        var listings = await q
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        var ids = listings.Select(l => l.ListingID).ToList();
        var imageMap = await BuildImageMapAsync(ids);

        return (listings.Select(l => MapToDto(l, imageMap)), total);
    }

    public async Task<int> CreateAsync(CreateListingDto dto, int supplierId)
    {
        var isDuplicate = await _db.Listings.AnyAsync(l =>
            l.SupplierID == supplierId &&
            l.ListingTitle.ToLower() == dto.ListingTitle.ToLower());

        var currentCommissionRate = await _db.FeeConfigurations
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => (decimal?)f.CommissionRate)
            .FirstOrDefaultAsync();

        var listing = new Listing
        {
            ListingTitle = dto.ListingTitle,
            CategoryID = dto.CategoryID,
            Description = dto.Description,
            MakeBrand = dto.MakeBrand,
            Model = dto.Model,
            Year = dto.Year,
            OperatingWeight = dto.OperatingWeight,
            EnginePower = dto.EnginePower,
            Location = dto.Location,
            DailyRateZAR = dto.DailyRateZAR,
            WeeklyRateZAR = dto.WeeklyRateZAR,
            CommissionRateSnapshot = currentCommissionRate,
            DryHireAvailable = dto.DryHireAvailable,
            WetHireAvailable = dto.WetHireAvailable,
            WetDailyRateZAR = dto.WetDailyRateZAR,
            WetWeeklyRateZAR = dto.WetWeeklyRateZAR,
            PickupAvailable = dto.PickupAvailable,
            DeliveryAvailable = dto.DeliveryAvailable,
            DeliveryFeeZAR = dto.DeliveryFeeZAR,
            AvailabilityStatus = "Pending",
            SupplierID = supplierId,
            DuplicateFlag = isDuplicate,
            CreatedDate = AppTime.Now,
            PricingMode = dto.PricingMode,
            UnitsOwned = dto.UnitsOwned,
            UnitsAvailable = dto.UnitsOwned,
            UnitsReserved = 0,
            UnitsUnderMaintenance = 0,
            MasterLeaseAgreementAccepted = dto.AgreeToMasterLeaseAgreement,
            MasterLeaseAgreementAcceptedDate = dto.AgreeToMasterLeaseAgreement ? AppTime.Now : null
        };
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        return listing.ListingID;
    }

    public async Task UpdateAsync(int id, UpdateListingDto dto)
    {
        var l = await _db.Listings.FindAsync(id) ?? throw new KeyNotFoundException();

        l.ListingTitle = dto.ListingTitle;
        l.CategoryID = dto.CategoryID;
        l.Description = dto.Description;
        l.MakeBrand = dto.MakeBrand;
        l.Model = dto.Model;
        l.Year = dto.Year;
        l.OperatingWeight = dto.OperatingWeight;
        l.EnginePower = dto.EnginePower;
        l.Location = dto.Location;
        l.DailyRateZAR = dto.DailyRateZAR;
        l.WeeklyRateZAR = dto.WeeklyRateZAR;
        l.DryHireAvailable = dto.DryHireAvailable;
        l.WetHireAvailable = dto.WetHireAvailable;
        l.WetDailyRateZAR = dto.WetDailyRateZAR;
        l.WetWeeklyRateZAR = dto.WetWeeklyRateZAR;
        l.PickupAvailable = dto.PickupAvailable;
        l.DeliveryAvailable = dto.DeliveryAvailable;
        l.DeliveryFeeZAR = dto.DeliveryFeeZAR;
        l.PricingMode = dto.PricingMode;
        var unitsDelta = dto.UnitsOwned - l.UnitsOwned;
        if (unitsDelta != 0)
        {
            l.UnitsOwned = dto.UnitsOwned;
            l.UnitsAvailable = Math.Max(0, l.UnitsAvailable + unitsDelta);
        }
        l.AvailabilityStatus = "Pending";
        l.AdminReviewNotes = null;

        await _db.SaveChangesAsync();
    }

    public async Task<(bool Success, string? Error)> DeactivateAsync(int id)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l == null) return (false, "Listing not found.");

        var processError = await GetListingBookingProcessBlockReasonAsync(id);
        if (processError != null)
            return (false, processError);

        l.AvailabilityStatus = "Inactive";
        l.DeactivatedDate = AppTime.Now;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Returns an error message when the listing is tied to an open booking process
    /// (or cart/lease/quote), otherwise null.
    /// </summary>
    public async Task<string?> GetListingBookingProcessBlockReasonAsync(int listingId)
    {
        var hasOpenBooking = await _db.Bookings.AnyAsync(b =>
            b.ListingID == listingId && b.Status != "Completed" && b.Status != "Cancelled");
        if (hasOpenBooking)
            return "This listing has bookings still in progress and cannot be deactivated or removed until those bookings are completed or cancelled.";

        var l = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.ListingID == listingId);
        if (l != null && l.UnitsReserved > 0)
            return "This listing has reserved units (cart or active booking) and cannot be deactivated until those reservations are released.";

        var inCart = await _db.CartItems.AnyAsync(c => c.ListingID == listingId);
        if (inCart)
            return "This listing is in one or more contractor carts and cannot be deactivated until it is removed from those carts.";

        var hasActiveLease = await _db.LeaseAgreements.AnyAsync(la =>
            la.ListingID == listingId && la.Status != "Completed" && la.Status != "Cancelled");
        if (hasActiveLease)
            return "This listing has an active or in-progress lease agreement and cannot be deactivated yet.";

        var hasOpenQuote = await _db.Quotations.AnyAsync(q =>
            q.ListingID == listingId && q.Status != "Rejected" && q.Status != "Expired" && q.Status != "Cancelled");
        if (hasOpenQuote)
            return "This listing has an open quotation and cannot be deactivated until the quote is closed.";

        return null;
    }

    public async Task<(bool Success, string? Error, bool HardDeleted)> ArchiveAsync(int id)
    {
        var l = await _db.Listings.FindAsync(id) ?? throw new KeyNotFoundException();

        // Active process: units currently reserved (cart soft-reservation or active hire)
        if (l.UnitsReserved > 0)
            return (false, "This listing has reserved units (cart or active booking) and cannot be deleted until those reservations are released.", false);

        // In someone's cart (checkout process has begun)
        var inCart = await _db.CartItems.AnyAsync(c => c.ListingID == id);
        if (inCart)
            return (false, "This listing is in one or more contractor carts and cannot be deleted until it is removed from those carts.", false);

        // Active / in-progress lease
        var hasActiveLease = await _db.LeaseAgreements.AnyAsync(la =>
            la.ListingID == id && la.Status != "Completed" && la.Status != "Cancelled");
        if (hasActiveLease)
            return (false, "This listing has an active or in-progress lease agreement and cannot be deleted yet.", false);

        // Any booking history (completed or not) — preserve audit trail
        var hasEverHadBooking = await _db.Bookings.AnyAsync(b => b.ListingID == id);
        if (hasEverHadBooking)
            return (false, "This listing has booking history and cannot be permanently deleted. Deactivate it instead if you no longer want it listed.", false);

        // Open quotations linked to this listing
        var hasOpenQuote = await _db.Quotations.AnyAsync(q =>
            q.ListingID == id && q.Status != "Rejected" && q.Status != "Expired" && q.Status != "Cancelled");
        if (hasOpenQuote)
            return (false, "This listing has an open quotation and cannot be deleted until the quote is closed.", false);

        _db.Listings.Remove(l);
        await _db.SaveChangesAsync();
        return (true, null, true);
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(int id, int adminId)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l == null) return (false, "Listing not found.");

        var imageCount = await _db.ListingImages.CountAsync(i => i.ListingID == id);
        if (imageCount < 1)
            return (false, "This listing has no photos. The supplier must upload at least 1 image (up to 5) before it can be approved.");

        l.AvailabilityStatus = "Active";
        l.AdminReviewNotes = null;
        l.LastReviewedByAdminID = adminId;
        l.LastReviewedDate = AppTime.Now;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RejectAsync(int id, string reason, int adminId)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l == null) return false;

        l.AvailabilityStatus = "Rejected";
        l.AdminReviewNotes = reason;
        l.LastReviewedByAdminID = adminId;
        l.LastReviewedDate = AppTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RequestChangesAsync(int id, string comments, int adminId)
    {
        var l = await _db.Listings.FindAsync(id);
        if (l == null) return false;
        l.AvailabilityStatus = "Draft";
        l.AdminReviewNotes = comments;
        l.LastReviewedByAdminID = adminId;
        l.LastReviewedDate = AppTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }
    private async Task<Dictionary<int, List<ListingImageDto>>> BuildImageMapAsync(IEnumerable<int> listingIds)
    {
        var ids = listingIds.ToList();

        var rows = await _db.ListingImages
            .Where(i => ids.Contains(i.ListingID))
            .OrderBy(i => i.ListingID)
            .ThenBy(i => i.DisplayOrder)
            .Select(i => new { i.ListingID, i.ImageID, i.FilePath })
            .ToListAsync();

        return rows
            .GroupBy(r => r.ListingID)
            .ToDictionary(g => g.Key, g => g.Select(r => new ListingImageDto { ImageID = r.ImageID, Url = r.FilePath }).ToList());
    }

    private static ListingDto MapToDto(Listing l, Dictionary<int, List<ListingImageDto>> imageMap)
    {
        var images = imageMap.TryGetValue(l.ListingID, out var found) ? found : new List<ListingImageDto>();

        return new ListingDto
        {
            ListingID = l.ListingID,
            ListingTitle = l.ListingTitle,
            CategoryID = l.CategoryID,
            AvailabilityStatus = l.AvailabilityStatus,
            Description = l.Description,
            MakeBrand = l.MakeBrand,
            Model = l.Model,
            Year = l.Year,
            OperatingWeight = l.OperatingWeight,
            EnginePower = l.EnginePower,
            Location = l.Location,
            DailyRateZAR = l.DailyRateZAR,
            WeeklyRateZAR = l.WeeklyRateZAR,
            DryHireAvailable = l.DryHireAvailable,
            WetHireAvailable = l.WetHireAvailable,
            WetDailyRateZAR = l.WetDailyRateZAR,
            WetWeeklyRateZAR = l.WetWeeklyRateZAR,
            PickupAvailable = l.PickupAvailable,
            DeliveryAvailable = l.DeliveryAvailable,
            DeliveryFeeZAR = l.DeliveryFeeZAR,
            CreatedDate = l.CreatedDate,
            DuplicateFlag = l.DuplicateFlag,
            SupplierID = l.SupplierID,
            SupplierName = l.Supplier?.FullName ?? string.Empty,
            ImageUrls = images.Select(i => i.Url).ToList(),
            Images = images,
            AverageRating = l.AverageRating,
            ReviewCount = l.ReviewCount,
            PricingMode = l.PricingMode,
            UnitsOwned = l.UnitsOwned,
            UnitsAvailable = l.UnitsAvailable,
            UnitsReserved = l.UnitsReserved,
            UnitsUnderMaintenance = l.UnitsUnderMaintenance,
            IsArchived = l.IsArchived,
            AdminReviewNotes = l.AdminReviewNotes,
            LastReviewedDate = l.LastReviewedDate
        };
    }

    public async Task<EquaMeridian.DTOs.Listings.SupplierStorefrontDto?> GetSupplierStorefrontAsync(
        int supplierId, int page = 1, int pageSize = 12)
    {
        var supplier = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == supplierId
                && u.Role.ToLower() == "supplier"
                && u.AccountStatus == "Active");
        if (supplier == null) return null;

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : Math.Min(pageSize, 48);

        var activeQuery = _db.Listings
            .Include(l => l.Supplier)
            .AsNoTracking()
            .Where(l => l.SupplierID == supplierId
                        && l.AvailabilityStatus == "Active"
                        && !l.IsArchived);

        var activeCount = await activeQuery.CountAsync();

        var listings = await activeQuery
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var imageMap = await BuildImageMapAsync(listings.Select(l => l.ListingID));
        decimal avgRating = 0;
        int reviewCount = 0;
        if (activeCount > 0)
        {
            var rated = await _db.Listings.AsNoTracking()
                .Where(l => l.SupplierID == supplierId
                            && l.AvailabilityStatus == "Active"
                            && !l.IsArchived
                            && l.ReviewCount > 0)
                .Select(l => new { l.AverageRating, l.ReviewCount })
                .ToListAsync();
            reviewCount = rated.Sum(r => r.ReviewCount);
            if (reviewCount > 0)
                avgRating = rated.Sum(r => r.AverageRating * r.ReviewCount) / reviewCount;
        }

        var locationSummary = await _db.Listings.AsNoTracking()
            .Where(l => l.SupplierID == supplierId
                        && l.AvailabilityStatus == "Active"
                        && !l.IsArchived
                        && l.Location != null
                        && l.Location != "")
            .Select(l => l.Location!)
            .Distinct()
            .Take(3)
            .ToListAsync();

        return new EquaMeridian.DTOs.Listings.SupplierStorefrontDto
        {
            SupplierID = supplier.UserID,
            SupplierName = supplier.FullName,
            CompanyName = supplier.CompanyName,
            LocationSummary = locationSummary.Count > 0 ? string.Join(" · ", locationSummary) : null,
            AverageRating = Math.Round(avgRating, 1),
            ReviewCount = reviewCount,
            ActiveListings = activeCount,
            Verified = supplier.AccountStatus == "Active",
            Listings = listings.Select(l => MapToDto(l, imageMap)).ToList()
        };
    }
}
