using EquaMeridian.DTOs.Bookings;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class BookingRepository : IBookingRepository
{
    private static readonly HashSet<string> ValidReturnReasons = new()
    {
        "Lease period ending", "Early return", "Equipment fault"
    };

    private static readonly HashSet<string> ValidConditions = new()
    {
        "Good", "Minor wear", "Damaged"
    };

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPaymentSyncService _paymentSync;
    public BookingRepository(AppDbContext db, IConfiguration config, IPaymentSyncService paymentSync)
    { _db = db; _config = config; _paymentSync = paymentSync; }

    public async Task<BookingsPageDto> GetAllForUserAsync(
        int userId, string role, int page, int pageSize,
        string? search = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var isContractor = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase);

        var q = _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Delivery)
            .Include(b => b.Supplier)
            .Include(b => b.Contractor)
            .Include(b => b.ReturnRequest)
            .AsNoTracking()
            .AsQueryable();

        q = isContractor
            ? q.Where(b => b.ContractorID == userId)
            : q.Where(b => b.SupplierID == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(b =>
                b.Listing.ListingTitle.Contains(search) ||
                b.Supplier.FullName.Contains(search) ||
                b.Contractor.FullName.Contains(search));
        }

        if (dateFrom.HasValue)
            q = q.Where(b => b.RentalEndDate >= dateFrom.Value);
        if (dateTo.HasValue)
            q = q.Where(b => b.RentalStartDate <= dateTo.Value);

        var all = await q.OrderByDescending(b => b.CreatedDate).ToListAsync();
        var bookingIds = all.Select(b => b.BookingID).ToList();
        var awaitingPaymentIds = all.Where(b => b.Status == "AwaitingPayment").Select(b => b.BookingID).ToList();
        if (awaitingPaymentIds.Count > 0)
        {
            var pendingInvoices = await _db.Invoices
                .Include(i => i.Quotation)
                .Where(i => i.Quotation.BookingID != null && awaitingPaymentIds.Contains(i.Quotation.BookingID.Value))
                .ToListAsync();

            foreach (var invoice in pendingInvoices)
            {
                var synced = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);
                if (synced.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    var b = all.First(x => x.BookingID == invoice.Quotation.BookingID);
                    b.Status = "Confirmed";
                }
            }
        }

        var bookingIdsWithOpenDispute = await _db.Disputes
            .Where(d => d.BookingID != null && bookingIds.Contains(d.BookingID!.Value)
                && (d.Status == "Open" || d.Status == "Under Review"))
            .Select(d => d.BookingID!.Value)
            .ToListAsync();
        var reviewInfoByBooking = await _db.Reviews
            .Where(r => bookingIds.Contains(r.BookingID) && r.Status == "Published")
            .Select(r => new { r.BookingID, r.ReviewID, r.CreatedAt })
            .ToListAsync();
        var reviewMap = reviewInfoByBooking.ToDictionary(r => r.BookingID, r => (r.ReviewID, r.CreatedAt));
        var editWindowDays = _config.GetValue<int?>("Reviews:EditWindowDays") ?? 30;

        var mapped = all
            .Select(b => MapToListItemDto(
                b, role, bookingIdsWithOpenDispute.Contains(b.BookingID),
                reviewMap.TryGetValue(b.BookingID, out var reviewInfo) ? reviewInfo : null,
                editWindowDays))
            .ToList();

        if (!string.IsNullOrWhiteSpace(status))
            mapped = mapped.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = mapped.Count;
        var page_ = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new BookingsPageDto
        {
            Bookings = page_,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            SummaryCards = BuildSummaryCards(all, mapped, isContractor)
        };
    }

    public async Task<BookingsPageDto> GetAllForAdminAsync(
        int page, int pageSize, string? search = null, string? status = null)
    {
        var q = _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Contractor)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(b =>
                b.Listing.ListingTitle.Contains(search) ||
                b.Supplier.FullName.Contains(search) ||
                b.Contractor.FullName.Contains(search));
        }

        var all = await q.OrderByDescending(b => b.CreatedDate).ToListAsync();

        var mapped = all.Select(b => new BookingListItemDto
        {
            BookingID = b.BookingID,
            Machinery = b.Listing.ListingTitle,
            SupplierName = b.Supplier?.FullName ?? string.Empty,
            ContractorName = b.Contractor?.FullName ?? string.Empty,
            RentalStartDate = b.RentalStartDate,
            RentalEndDate = b.RentalEndDate,
            DeliveryAddress = b.DeliveryAddress,
            Status = ComputeDisplayStatus(b),
            CanViewAddress = true
        }).ToList();

        if (!string.IsNullOrWhiteSpace(status))
            mapped = mapped.Where(m => m.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = mapped.Count;
        var paged = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new BookingsPageDto
        {
            Bookings = paged,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            SummaryCards = BuildSummaryCards(all, mapped, isContractor: false)
        };
    }

    public async Task<DeliveryDetailDto?> GetDeliveryDetailAsync(int bookingId, int userId, string role)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Contractor)
            .Include(b => b.Delivery)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingID == bookingId
                && (b.SupplierID == userId || b.ContractorID == userId));

        if (booking == null) return null;
        var hasOpenDispute = await _db.Disputes.AnyAsync(d =>
            d.BookingID == bookingId && (d.Status == "Open" || d.Status == "Under Review"));

        return MapToDetailDto(booking, hasOpenDispute);
    }

    public async Task<bool> UpdateDeliveryAddressAsync(int bookingId, int contractorId, string newAddress)
    {
        var booking = await _db.Bookings
            .Include(b => b.Delivery)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.ContractorID == contractorId);

        if (booking == null || booking.Status == "Cancelled" || booking.Delivery?.Status == "Delivered")
            return false;

        booking.DeliveryAddress = newAddress;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ConfirmDeliveryResult> ConfirmDeliveryAsync(int bookingId, int contractorId, ConfirmDeliveryDto dto)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Delivery)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.ContractorID == contractorId);

        if (booking == null)
            return new ConfirmDeliveryResult { Success = false, Error = "Booking not found." };

        if (booking.Delivery?.Status == "Delivered")
            return new ConfirmDeliveryResult { Success = false, Error = "This delivery has already been confirmed." };

        // Pickup/collect path requires supplier to mark Ready For Pickup first.
        // Supplier-delivery path may confirm from Confirmed (Ready step is optional).
        var isPickupPath = IsPickupFulfillment(
            booking.Delivery?.Method,
            dto.DeliveryMethod,
            booking.DeliveryAddress);

        if (isPickupPath)
        {
            if (booking.Status != "Ready For Pickup")
                return new ConfirmDeliveryResult
                {
                    Success = false,
                    Error = booking.Status == "Confirmed"
                        ? "The supplier must mark this booking as Ready for Pickup before you can confirm collection."
                        : $"Delivery cannot be confirmed because the booking status is '{booking.Status}'."
                };
        }
        else
        {
            if (booking.Status != "Confirmed" && booking.Status != "Ready For Pickup")
                return new ConfirmDeliveryResult
                {
                    Success = false,
                    Error = $"Delivery cannot be confirmed because the booking status is '{booking.Status}'."
                };
        }

        if (!string.IsNullOrWhiteSpace(dto.DeliveryMethod) && !DeliveryMethods.All.Contains(dto.DeliveryMethod))
            return new ConfirmDeliveryResult
            {
                Success = false,
                Error = $"Delivery method must be one of: {string.Join(", ", DeliveryMethods.All)}."
            };

        if (booking.Delivery == null)
        {
            booking.Delivery = new Delivery
            {
                BookingID = booking.BookingID,
                Method = dto.DeliveryMethod ?? DeliveryMethods.SupplierDelivery
            };
            _db.Deliveries.Add(booking.Delivery);
        }
        else if (!string.IsNullOrWhiteSpace(dto.DeliveryMethod))
        {
            booking.Delivery.Method = dto.DeliveryMethod;
        }

        booking.Delivery.Status = "Delivered";
        booking.Delivery.DeliveryDate = AppTime.Now;
        var outcome = string.IsNullOrWhiteSpace(dto.Outcome) ? "Pass"
            : (dto.Outcome.Equals("Fail", StringComparison.OrdinalIgnoreCase) ? "Fail" : "Pass");
        var checklist = string.IsNullOrWhiteSpace(dto.ChecklistData)
            ? (dto.Notes ?? "Handover confirmed by contractor.")
            : dto.ChecklistData;
        var handover = new BookingConditionInspection
        {
            BookingID = booking.BookingID,
            InspectionType = "Handover",
            CompletedByUserID = contractorId,
            ChecklistData = checklist,
            PhotoUrls = string.IsNullOrWhiteSpace(dto.PhotoUrls) ? null : dto.PhotoUrls,
            Outcome = outcome,
            Notes = dto.Notes ?? checklist,
            DamageDescription = dto.DamageDescription,
            SupplierAcknowledged = false,
            ContractorAcknowledged = true,
            CompletedDate = AppTime.Now
        };
        _db.BookingConditionInspections.Add(handover);
        await _db.SaveChangesAsync();
        booking.HandoverInspectionID = handover.BookingConditionInspectionID;

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Delivered",
            ChangedByUserID = contractorId,
            ChangedByRole = "Contractor",
            Notes = dto.DeliveryMethod,
            CreatedDate = AppTime.Now
        });

        await _db.SaveChangesAsync();

        return new ConfirmDeliveryResult
        {
            Success = true,
            Delivery = MapToDetailDto(booking),
            SupplierID = booking.SupplierID,
            SupplierEmail = booking.Supplier.Email,
            SupplierName = booking.Supplier.FullName
        };
    }

    public async Task<ReturnRequestResult> RequestReturnAsync(int bookingId, int contractorId, RequestReturnDto dto)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Delivery)
            .Include(b => b.ReturnRequest)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.ContractorID == contractorId);

        if (booking == null)
            return new ReturnRequestResult { Success = false, Error = "Booking not found." };
        if (ComputeDisplayStatus(booking) != "In Progress")
            return new ReturnRequestResult
            {
                Success = false,
                Error = "This booking has already been updated. Please refresh and try again."
            };
        if (booking.ReturnRequest != null && booking.ReturnRequest.Status == "Open")
            return new ReturnRequestResult
            {
                Success = false,
                Error = "A return request is already open for this booking."
            };
        if (booking.Delivery?.Method == DeliveryMethods.ContractorPickup)
            return new ReturnRequestResult
            {
                Success = false,
                Error = "This booking was fulfilled by pickup, so there's no return request to make — " +
                        "please return the machinery to the supplier directly. They'll confirm the return once it arrives."
            };

        if (!ValidReturnReasons.Contains(dto.ReturnReason))
            return new ReturnRequestResult { Success = false, Error = "Please select a valid return reason." };

        if (dto.PreferredPickupDate.Date < AppTime.Now.Date)
            return new ReturnRequestResult { Success = false, Error = "Pickup date is required and cannot be in the past." };

        var earlyReturnFeeApplies = dto.PreferredPickupDate.Date < booking.RentalEndDate.Date;

        var returnRequest = new ReturnRequest
        {
            BookingID = booking.BookingID,
            ContractorID = contractorId,
            Reason = dto.ReturnReason,
            PreferredPickupDate = dto.PreferredPickupDate,
            TimeWindow = dto.PickupTimeWindow,
            PickupLocation = dto.PickupLocation,
            Notes = dto.Notes,
            EarlyReturnFeeApplies = earlyReturnFeeApplies,
            RequestedAt = AppTime.Now,
            Status = "Open"
        };

        _db.ReturnRequests.Add(returnRequest);
        booking.Status = "Return Requested";

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Return Requested",
            ChangedByUserID = contractorId,
            ChangedByRole = "Contractor",
            Notes = dto.ReturnReason,
            CreatedDate = AppTime.Now
        });

        await _db.SaveChangesAsync();

        return new ReturnRequestResult
        {
            Success = true,
            ReturnRequestID = returnRequest.ReturnRequestID,
            EarlyReturnFeeApplies = earlyReturnFeeApplies,
            SupplierID = booking.SupplierID,
            SupplierEmail = booking.Supplier.Email,
            SupplierName = booking.Supplier.FullName,
            Machinery = booking.Listing.ListingTitle
        };
    }

    public async Task<ConfirmReturnResult> ConfirmReturnAsync(
        int bookingId, int supplierId, ConfirmReturnDto dto, IReadOnlyList<string> photoEvidencePaths)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Contractor)
            .Include(b => b.ReturnRequest)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.SupplierID == supplierId);

        if (booking == null)
            return new ConfirmReturnResult { Success = false, Error = "Booking not found." };

        if (booking.Status != "Return Requested" && booking.Status != "Ready For Return Pickup")
            return new ConfirmReturnResult
            {
                Success = false,
                Error = "This booking has already been updated. Please refresh and try again."
            };

        if (!ValidConditions.Contains(dto.Condition))
            return new ConfirmReturnResult { Success = false, Error = "Please select a valid machinery condition." };

        if (string.IsNullOrWhiteSpace(dto.InspectionNotes))
            return new ConfirmReturnResult { Success = false, Error = "Inspection notes are required." };

        if (dto.Condition == "Damaged")
        {
            if (string.IsNullOrWhiteSpace(dto.DamageDescription))
                return new ConfirmReturnResult { Success = false, Error = "A damage description is required for Damaged returns." };
            if (dto.EstimatedRepairCost is not > 0)
                return new ConfirmReturnResult { Success = false, Error = "Estimated repair cost is required for Damaged returns." };
            if (photoEvidencePaths.Count == 0)
                return new ConfirmReturnResult { Success = false, Error = "Photo evidence is required for Damaged returns." };
        }

        var passed = dto.Condition != "Damaged";
        var qty = Math.Max(1, booking.Quantity);

        var returnInspection = new BookingConditionInspection
        {
            BookingID = booking.BookingID,
            InspectionType = "Return",
            CompletedByUserID = supplierId,
            ChecklistData = dto.InspectionNotes,
            PhotoUrls = photoEvidencePaths.Count > 0 ? string.Join(",", photoEvidencePaths) : null,
            Outcome = passed ? "Pass" : "Fail",
            Notes = dto.InspectionNotes,
            DamageDescription = dto.DamageDescription,
            EstimatedRepairCost = dto.EstimatedRepairCost,
            SupplierAcknowledged = true,
            ContractorAcknowledged = false,
            CompletedDate = AppTime.Now
        };
        _db.BookingConditionInspections.Add(returnInspection);
        await _db.SaveChangesAsync();

        booking.Status = "Completed";
        booking.ConditionOnReturn = dto.Condition;
        booking.ReturnConfirmedAt = AppTime.Now;
        booking.OffHireDateTime = AppTime.Now;
        booking.ReturnInspectionID = returnInspection.BookingConditionInspectionID;

        if (booking.ReturnRequest != null)
            booking.ReturnRequest.Status = "Completed";
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.ListingID == booking.ListingID)
                      ?? booking.Listing;
        ReturnHireUnits(listing, qty, toAvailable: passed);

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Completed",
            ChangedByUserID = supplierId,
            ChangedByRole = "Supplier",
            Notes = dto.InspectionNotes,
            CreatedDate = AppTime.Now
        });

        int? depositDeductionId = null;
        if (!passed)
        {
            var deduction = new DepositDeduction
            {
                BookingID = booking.BookingID,
                DamageDescription = dto.DamageDescription!,
                EstimatedRepairCost = dto.EstimatedRepairCost!.Value,
                PhotoEvidenceUrls = string.Join(",", photoEvidencePaths),
                Status = "PendingReview",
                CreatedDate = AppTime.Now
            };
            _db.DepositDeductions.Add(deduction);
            await _db.SaveChangesAsync();
            depositDeductionId = deduction.DepositDeductionID;
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        return new ConfirmReturnResult
        {
            Success = true,
            DepositDeductionID = depositDeductionId,
            ContractorID = booking.ContractorID,
            ContractorEmail = booking.Contractor.Email,
            ContractorName = booking.Contractor.FullName,
            Machinery = booking.Listing.ListingTitle,
            Condition = dto.Condition
        };
    }
    private static string ComputeDisplayStatus(Booking b)
    {
        if (b.Status == "Cancelled") return "Cancelled";
        if (b.Status == "AwaitingSignature") return "Awaiting Lease Signature";
        if (b.Status == "AwaitingPayment") return "Awaiting Payment";
        if (b.Status == "Completed") return "Completed";
        if (b.Status == "Ready For Return Pickup") return "Ready For Return Pickup";
        if (b.Status == "Return Requested") return "Return Requested";
        if (b.Delivery?.Status == "Delivered") return "In Progress";
        if (b.Status == "Ready For Pickup") return "Ready For Pickup";
        return "Awaiting Delivery";
    }

    private static readonly (string Stage, string Label)[] TrackingTemplate =
    {
        ("Confirmed", "Booking Confirmed"),
        ("Ready For Pickup", "Ready For Pickup"),
        ("Delivered", "Delivered To You"),
        ("Return Requested", "Return Requested"),
        ("Ready For Return Pickup", "Ready For Return Pickup"),
        ("Completed", "Returned To Supplier")
    };

    public async Task<MarkReadyResult> MarkReadyForPickupAsync(int bookingId, int supplierId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Contractor)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.SupplierID == supplierId);

        if (booking == null)
            return new MarkReadyResult { Success = false, Error = "Booking not found." };

        if (booking.Status != "Confirmed")
            return new MarkReadyResult
            {
                Success = false,
                Error = $"Booking cannot be marked ready because its status is '{booking.Status}'."
            };

        booking.Status = "Ready For Pickup";

        // Real history row only — no backdated Ready rows from ConfirmDelivery.
        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Ready For Pickup",
            ChangedByUserID = supplierId,
            ChangedByRole = "Supplier",
            CreatedDate = AppTime.Now
        });

        await _db.SaveChangesAsync();

        return new MarkReadyResult
        {
            Success = true,
            ContractorID = booking.ContractorID,
            ContractorEmail = booking.Contractor.Email,
            ContractorName = booking.Contractor.FullName,
            Machinery = booking.Listing.ListingTitle
        };
    }
    public async Task<MarkReadyResult> MarkReadyForReturnPickupAsync(int bookingId, int supplierId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Contractor)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId && b.SupplierID == supplierId);

        if (booking == null)
            return new MarkReadyResult { Success = false, Error = "Booking not found." };

        if (booking.Status != "Return Requested")
            return new MarkReadyResult
            {
                Success = false,
                Error = $"Booking cannot be marked ready for return pickup because its status is '{booking.Status}'."
            };

        booking.Status = "Ready For Return Pickup";

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Ready For Return Pickup",
            ChangedByUserID = supplierId,
            ChangedByRole = "Supplier",
            CreatedDate = AppTime.Now
        });

        await _db.SaveChangesAsync();

        return new MarkReadyResult
        {
            Success = true,
            ContractorID = booking.ContractorID,
            ContractorEmail = booking.Contractor.Email,
            ContractorName = booking.Contractor.FullName,
            Machinery = booking.Listing.ListingTitle
        };
    }

    public async Task<BookingTrackingDto?> GetTrackingAsync(int bookingId, int userId, string role)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Delivery)
            .Include(b => b.ReturnRequest)
            .Include(b => b.StatusHistory)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingID == bookingId
                && (b.SupplierID == userId || b.ContractorID == userId));

        if (booking == null) return null;
        if (booking.Status == "AwaitingPayment")
        {
            var invoice = await _db.Invoices
                .Include(i => i.Quotation)
                .FirstOrDefaultAsync(i => i.Quotation.BookingID == booking.BookingID);

            if (invoice != null)
            {
                var synced = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);
                if (synced.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                    booking.Status = "Confirmed";
            }
        }

        var historyByStage = booking.StatusHistory
            .GroupBy(h => h.Stage)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.CreatedDate).First());

        var currentRank = booking.Status switch
        {
            "Completed" => 5,
            "Ready For Return Pickup" => 4,
            "Return Requested" => 3,
            _ when booking.Delivery?.Status == "Delivered" => 2,
            "Ready For Pickup" => 1,
            "AwaitingSignature" or "AwaitingPayment" => -1,
            _ => 0
        };

        var isPickup = IsPickupFulfillment(booking.Delivery?.Method, null, booking.DeliveryAddress);

        var dto = new BookingTrackingDto
        {
            BookingID = booking.BookingID,
            Machinery = booking.Listing.ListingTitle,
            CurrentStatus = booking.Status,
            IsCancelled = booking.Status == "Cancelled"
        };

        for (var rank = 0; rank < TrackingTemplate.Length; rank++)
        {
            var (stage, defaultLabel) = TrackingTemplate[rank];
            var label = ResolveTrackingLabel(stage, defaultLabel, isPickup);
            historyByStage.TryGetValue(stage, out var historyRow);
            DateTime? timestamp = historyRow?.CreatedDate ?? stage switch
            {
                "Confirmed" => booking.CreatedDate,
                "Delivered" => booking.Delivery?.DeliveryDate,
                "Return Requested" => booking.ReturnRequest?.RequestedAt,
                "Completed" => booking.ReturnConfirmedAt,
                _ => null
            };

            dto.Stages.Add(new TrackingStageDto
            {
                Stage = stage,
                Label = label,
                Timestamp = timestamp,
                Notes = historyRow?.Notes,
                IsComplete = !dto.IsCancelled && rank <= currentRank,
                IsCurrent = !dto.IsCancelled && rank == currentRank && currentRank < 5
            });
        }

        return dto;
    }

    private static BookingListItemDto MapToListItemDto(
        Booking b, string role, bool hasOpenDispute,
        (int ReviewID, DateTime CreatedAt)? reviewInfo, int editWindowDays)
    {
        var isContractor = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase);
        var displayStatus = ComputeDisplayStatus(b);
        var canLeaveReview = isContractor && displayStatus == "Completed" && reviewInfo == null;
        var canEditReview = isContractor && reviewInfo != null
            && (AppTime.Now - reviewInfo.Value.CreatedAt).TotalDays <= editWindowDays;
        var canDeleteReview = isContractor && reviewInfo != null;

        return new BookingListItemDto
        {
            BookingID = b.BookingID,
            Machinery = b.Listing.ListingTitle,
            SupplierName = b.Supplier?.FullName ?? string.Empty,
            ContractorName = b.Contractor?.FullName ?? string.Empty,
            RentalStartDate = b.RentalStartDate,
            RentalEndDate = b.RentalEndDate,
            DeliveryAddress = b.DeliveryAddress,
            Status = displayStatus,
            CanViewAddress = b.Status == "Confirmed" || b.Status == "Ready For Pickup",
            // Pickup path: only after Ready For Pickup. Supplier delivery: from Confirmed (Awaiting Delivery) or Ready.
            CanConfirmDelivery = isContractor
                && (displayStatus == "Ready For Pickup"
                    || (displayStatus == "Awaiting Delivery"
                        && !IsPickupFulfillment(b.Delivery?.Method, null, b.DeliveryAddress))),
            CanUpdateAddress = isContractor
                && displayStatus is "Awaiting Delivery" or "Ready For Pickup",
            CanRequestReturn = isContractor && displayStatus == "In Progress",
            CanMarkReadyForPickup = !isContractor && displayStatus == "Awaiting Delivery",
            CanMarkReadyForReturnPickup = !isContractor && displayStatus == "Return Requested",
            CanConfirmReturn = !isContractor && displayStatus is "Return Requested" or "Ready For Return Pickup",
            CanRaiseDispute = !hasOpenDispute
                && displayStatus is "In Progress" or "Return Requested" or "Ready For Return Pickup" or "Completed",
            CanLeaveReview = canLeaveReview,
            CanEditReview = canEditReview,
            CanDeleteReview = canDeleteReview,
            ReviewID = reviewInfo?.ReviewID
        };
    }

    private static BookingSummaryCardsDto BuildSummaryCards(
        List<Booking> all, List<BookingListItemDto> mapped, bool isContractor)
    {
        var completed = all.Where(b => ComputeDisplayStatus(b) == "Completed").ToList();

        return new BookingSummaryCardsDto
        {
            ActiveBookings = mapped.Count(m => m.Status is "Awaiting Delivery" or "Ready For Pickup"
                or "In Progress" or "Return Requested" or "Ready For Return Pickup"),
            AwaitingYourAction = mapped.Count(m => m.CanConfirmDelivery || m.CanConfirmReturn
                || m.CanMarkReadyForPickup || m.CanMarkReadyForReturnPickup),
            CompletedBookings = completed.Count,
            TotalLeasedToDate = completed.Sum(b =>
                Math.Max(1, (b.RentalEndDate.Date - b.RentalStartDate.Date).Days) * b.Listing.DailyRateZAR)
        };
    }

    private static readonly HashSet<string> CancellableStatuses = new()
    {
        "AwaitingSignature", "AwaitingPayment", "Confirmed"
    };

    public async Task<CancelBookingResult> CancelAsync(int bookingId, int userId, string role, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new CancelBookingResult { Success = false, Error = "Please provide a reason for cancelling." };

        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Supplier)
            .Include(b => b.Contractor)
            .Include(b => b.Delivery)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId);

        if (booking == null)
            return new CancelBookingResult { Success = false, Error = "Booking not found." };

        var isContractorOwner = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase) && booking.ContractorID == userId;
        var isSupplierOwner = role.Equals("Supplier", StringComparison.OrdinalIgnoreCase) && booking.SupplierID == userId;
        if (!isContractorOwner && !isSupplierOwner)
            return new CancelBookingResult { Success = false, Error = "You do not have access to this booking." };

        if (booking.Status == "Cancelled")
            return new CancelBookingResult { Success = false, Error = "This booking is already cancelled." };
        if (booking.Status == "Completed")
            return new CancelBookingResult { Success = false, Error = "This booking has already been completed and can't be cancelled." };
        if (booking.Delivery != null)
            return new CancelBookingResult
            {
                Success = false,
                Error = "This booking has already progressed past cancellation (machinery is being prepared, delivered, or returned). " +
                        "Please raise a dispute instead if there's an issue."
            };
        if (!CancellableStatuses.Contains(booking.Status))
            return new CancelBookingResult
            {
                Success = false,
                Error = "This booking has already progressed past cancellation (machinery is being prepared, delivered, or returned). " +
                        "Please raise a dispute instead if there's an issue."
            };

        var cancelledByRole = isContractorOwner ? "Contractor" : "Supplier";
        var feeApplies = (booking.RentalStartDate - AppTime.Now).TotalHours < 48;

        booking.Status = "Cancelled";
        booking.CancelledAt = AppTime.Now;
        booking.CancelledByUserID = userId;
        booking.CancellationReason = reason;
        booking.CancellationFeeApplies = feeApplies;

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = "Cancelled",
            ChangedByUserID = userId,
            ChangedByRole = cancelledByRole,
            Notes = feeApplies
                ? $"Cancelled by {cancelledByRole.ToLower()}: {reason} (within 48-hour cancellation window)"
                : $"Cancelled by {cancelledByRole.ToLower()}: {reason}",
            CreatedDate = AppTime.Now
        });
        if (booking.Delivery != null && booking.Delivery.Status != "Cancelled")
            booking.Delivery.Status = "Cancelled";

        var lease = await _db.LeaseAgreements.FirstOrDefaultAsync(l => l.BookingID == booking.BookingID);
        if (lease != null && lease.Status != "Cancelled")
        {
            lease.Status = "Cancelled";
            lease.UpdatedDate = AppTime.Now;
            lease.UpdatedByUserID = userId;
        }
        var quotation = await _db.Quotations.FirstOrDefaultAsync(q => q.BookingID == booking.BookingID)
            ?? await _db.Quotations.FirstOrDefaultAsync(q =>
                q.ListingID == booking.ListingID
                && q.ContractorID == booking.ContractorID
                && q.RentalStartDate.Date == booking.RentalStartDate.Date);
        var refundRequestCreated = false;
        if (quotation != null)
        {
            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.QuotationID == quotation.QuotationID);
            if (invoice != null)
            {
                invoice.PaymentStatus = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);

                if (invoice.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    if (invoice.Status != "Cancelled")
                        invoice.Status = "Cancelled";

                    var hasOpenRefund = await _db.Refunds.AnyAsync(r => r.InvoiceID == invoice.InvoiceID && r.Status == "Pending");
                    if (!hasOpenRefund)
                    {
                        _db.Refunds.Add(new Refund
                        {
                            InvoiceID = invoice.InvoiceID,
                            Amount = invoice.TotalAmount,
                            RequestedByUserID = userId,
                            Reason = $"Booking #{booking.BookingID} cancelled by {cancelledByRole.ToLower()}: {reason}",
                            Status = "Pending",
                            CreatedDate = AppTime.Now
                        });
                        refundRequestCreated = true;
                    }
                }
                else if (invoice.Status != "Cancelled")
                {
                    invoice.Status = "Cancelled";
                    invoice.PaymentStatus = "Cancelled";
                }
            }

            if (quotation.Status != "Cancelled" && quotation.Status != "Rejected")
            {
                quotation.Status = "Cancelled";
                quotation.RejectedDate = AppTime.Now;
                quotation.RejectionReason = $"Booking #{booking.BookingID} cancelled by {cancelledByRole.ToLower()}: {reason}";
            }
        }

        var cancelledQty = Math.Max(1, booking.Quantity);
        var listingToRestock = await _db.Listings.FirstOrDefaultAsync(l => l.ListingID == booking.ListingID)
                               ?? booking.Listing;
        ReturnHireUnits(listingToRestock, cancelledQty, toAvailable: true);

        await _db.SaveChangesAsync();
        var otherPartyIsContractor = isSupplierOwner;
        return new CancelBookingResult
        {
            Success = true,
            OtherPartyUserID = otherPartyIsContractor ? booking.ContractorID : booking.SupplierID,
            OtherPartyEmail = otherPartyIsContractor ? booking.Contractor.Email : booking.Supplier.Email,
            OtherPartyName = otherPartyIsContractor ? booking.Contractor.FullName : booking.Supplier.FullName,
            CancelledByRole = cancelledByRole,
            Machinery = booking.Listing.ListingTitle,
            CancellationFeeApplies = feeApplies,
            RefundRequestCreated = refundRequestCreated
        };
    }

    private static DeliveryDetailDto MapToDetailDto(Booking b, bool hasOpenDispute = false) => new()
    {
        BookingID = b.BookingID,
        SupplierID = b.SupplierID,
        ContractorID = b.ContractorID,
        Machinery = b.Listing.ListingTitle,
        SupplierName = b.Supplier?.FullName ?? string.Empty,
        ContractorName = b.Contractor?.FullName ?? string.Empty,
        RentalStartDate = b.RentalStartDate,
        RentalEndDate = b.RentalEndDate,
        DeliveryAddress = b.DeliveryAddress,
        BookingStatus = b.Status,
        DeliveryStatus = b.Status == "Cancelled"
            ? "Cancelled"
            : (b.Delivery?.Status ?? "Pending"),
        HasDelivery = b.Delivery != null,
        DeliveryMethod = FormatFulfillmentMethod(b.Delivery?.Method, b.DeliveryAddress),
        DeliveryDate = b.Delivery?.DeliveryDate,
        CanRaiseDispute = !hasOpenDispute
            && ComputeDisplayStatus(b) is "In Progress" or "Return Requested" or "Ready For Return Pickup" or "Completed"
    };

    private static void ReturnHireUnits(Listing? listing, int qty, bool toAvailable)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsReserved = Math.Max(0, listing.UnitsReserved - qty);
        if (toAvailable)
        {
            listing.UnitsAvailable = Math.Max(0, listing.UnitsAvailable + qty);
            if (listing.AvailabilityStatus != "Suspended" && listing.AvailabilityStatus != "Pending Review")
                listing.AvailabilityStatus = "Active";
        }
        else
        {
            listing.UnitsUnderMaintenance += qty;
        }
    }


    /// <summary>
    /// Tracker labels by fulfillment:
    /// - Pickup/collect: Ready For Collection → Collected → Ready For Return Collection
    /// - Supplier delivery: Ready For Delivery → Delivered To You → Ready For Return Pickup
    /// </summary>
    private static string ResolveTrackingLabel(string stage, string defaultLabel, bool isPickup)
    {
        if (isPickup)
        {
            return stage switch
            {
                "Ready For Pickup" => "Ready For Collection",
                "Delivered" => "Collected",
                "Ready For Return Pickup" => "Ready For Return Collection",
                "Completed" => "Returned To Supplier",
                _ => defaultLabel
            };
        }

        return stage switch
        {
            "Ready For Pickup" => "Ready For Delivery",
            "Delivered" => "Delivered To You",
            "Ready For Return Pickup" => "Ready For Return Pickup",
            "Completed" => "Returned To Supplier",
            _ => defaultLabel
        };
    }

    /// <summary>
    /// True when fulfillment is contractor pickup/collection (not supplier delivery).
    /// Uses stored delivery method, optional confirm DTO method, or address hint ("pickup"/"collect").
    /// </summary>
    private static bool IsPickupFulfillment(string? storedMethod, string? dtoMethod, string? deliveryAddress)
    {
        static bool LooksLikePickup(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim();
            return v.Contains("pickup", StringComparison.OrdinalIgnoreCase)
                || v.Contains("pick-up", StringComparison.OrdinalIgnoreCase)
                || v.Contains("collect", StringComparison.OrdinalIgnoreCase)
                || v.Equals(DeliveryMethods.ContractorPickup, StringComparison.OrdinalIgnoreCase);
        }

        if (LooksLikePickup(storedMethod) || LooksLikePickup(dtoMethod))
            return true;
        if (LooksLikePickup(deliveryAddress))
            return true;
        return false;
    }

    private static string FormatFulfillmentMethod(string? storedMethod, string? deliveryAddress)
    {
        // Address wins when it clearly indicates contractor pickup (e.g. "Pickup: Pretoria").
        var address = (deliveryAddress ?? string.Empty).Trim();
        if (address.Contains("pickup", StringComparison.OrdinalIgnoreCase)
            || address.Contains("collect", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("Pickup:", StringComparison.OrdinalIgnoreCase))
            return "Pick-up";

        var raw = (storedMethod ?? string.Empty).Trim();
        if (raw.Length == 0)
            raw = address;

        if (raw.Contains("pickup", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("Pickup:", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("collect", StringComparison.OrdinalIgnoreCase)
            || raw.Equals(DeliveryMethods.ContractorPickup, StringComparison.OrdinalIgnoreCase))
            return "Pick-up";

        if (raw.Contains("deliver", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Supplier Delivery", StringComparison.OrdinalIgnoreCase)
            || raw.Equals(DeliveryMethods.SupplierDelivery, StringComparison.OrdinalIgnoreCase))
            return "Delivery";

        if (!string.IsNullOrWhiteSpace(address))
            return "Delivery";

        return "—";
    }
}