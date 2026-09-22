using EquaMeridian.DTOs.Invoices;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _db;
    private readonly IPricingEngine _pricingEngine;
    public InvoiceRepository(AppDbContext db, IPricingEngine pricingEngine)
    {
        _db = db;
        _pricingEngine = pricingEngine;
    }

    public async Task<GenerateInvoiceResult> GenerateForQuotationAsync(int quotationId, int requestingUserId, string requestingRole)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationID == quotationId);

        if (quotation == null)
            return new GenerateInvoiceResult { Success = false, ErrorCode = "NotFound", Error = "Quotation not found." };

        var isAdmin = requestingRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        var isOwningContractor = requestingRole.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
            && quotation.ContractorID == requestingUserId;
        if (!isAdmin && !isOwningContractor)
            return new GenerateInvoiceResult
            {
                Success = false,
                ErrorCode = "Forbidden",
                Error = "You do not have permission to generate an invoice for this quotation."
            };

        return await GenerateCoreAsync(quotationId, isAdmin ? requestingUserId : (int?)null);
    }

    public Task<GenerateInvoiceResult> GenerateSystemAsync(int quotationId) =>
        GenerateCoreAsync(quotationId, null);

    private async Task<GenerateInvoiceResult> GenerateCoreAsync(int quotationId, int? adminId)
    {
        var quotation = await _db.Quotations
            .Include(q => q.Listing)
            .Include(q => q.Supplier)
            .Include(q => q.Contractor)
            .FirstOrDefaultAsync(q => q.QuotationID == quotationId);

        if (quotation == null)
            return new GenerateInvoiceResult { Success = false, ErrorCode = "NotFound", Error = "Quotation not found." };

        var existing = await _db.Invoices.AnyAsync(i => i.QuotationID == quotationId);
        if (existing)
            return new GenerateInvoiceResult
            {
                Success = false,
                ErrorCode = "AlreadyExists",
                Error = "An invoice has already been generated for this quotation."
            };
        if (quotation.Status != "Accepted" && quotation.Status != "Completed")
            return new GenerateInvoiceResult
            {
                Success = false,
                ErrorCode = "NotAccepted",
                Error = "Invoice generation requires an accepted quotation."
            };

        if (quotation.Contractor.AccountStatus != "Active" || quotation.Supplier.AccountStatus != "Active")
            return new GenerateInvoiceResult
            {
                Success = false,
                Error = "Both the Contractor and Supplier accounts must be active to generate an invoice."
            };

        var feeConfig = await _db.FeeConfigurations.OrderByDescending(f => f.UpdatedAt).FirstOrDefaultAsync();
        // Commission is locked in per-listing at the time it was created (see Listing.CommissionRateSnapshot),
        // so an admin changing the platform-wide rate only affects new listings, never existing ones.
        // Falls back to the current platform rate for listings created before this field existed.
        var platformFeePercentage = quotation.Listing.CommissionRateSnapshot ?? feeConfig?.CommissionRate ?? 8m;
        decimal subtotal, discountAmount, deliveryFee, vatRate, vatAmount, totalAmount;

        if (quotation.PriceInclVat.HasValue && quotation.PriceExclVat.HasValue)
        {
            subtotal = quotation.RentalSubtotal ?? (quotation.DailyRateZAR ?? quotation.Listing.DailyRateZAR)
                * quotation.Quantity * Math.Max(1, (quotation.RentalEndDate.Date - quotation.RentalStartDate.Date).Days);
            discountAmount = quotation.DiscountAmount ?? 0m;
            deliveryFee = quotation.DeliveryFee ?? 0m;
            vatRate = quotation.VatRate ?? feeConfig?.VATRate ?? 15m;
            totalAmount = quotation.PriceInclVat.Value;
            vatAmount = quotation.PriceInclVat.Value - quotation.PriceExclVat.Value;
        }
        else
        {
            var rentalDays = Math.Max(1, (quotation.RentalEndDate.Date - quotation.RentalStartDate.Date).Days);
            var dailyRate = quotation.DailyRateZAR ?? quotation.Listing.DailyRateZAR;
            var pricing = await _pricingEngine.CalculateAsync(
                dailyRate, rentalDays, quotation.Quantity, quotation.DeliveryDistanceKm ?? 0, quotation.Listing.CategoryID);

            subtotal = pricing.RentalSubtotal;
            discountAmount = pricing.DiscountAmount;
            deliveryFee = pricing.DeliveryFee;
            vatRate = pricing.VatRate;
            vatAmount = pricing.VatAmount;
            totalAmount = pricing.PriceInclVat;
        }

        var platformFeeAmount = Math.Round(totalAmount * (platformFeePercentage / 100m), 2);
        var supplierPayableAmount = totalAmount - platformFeeAmount;

        var invoiceDate = AppTime.Now;
        var invoiceNumber = await GenerateInvoiceNumberAsync(invoiceDate);

        var invoice = new Invoice
        {
            QuotationID = quotation.QuotationID,
            ContractorID = quotation.ContractorID,
            SupplierID = quotation.SupplierID,
            ListingID = quotation.ListingID,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            DeliveryFee = deliveryFee,
            VATRate = vatRate,
            VATAmount = vatAmount,
            TotalAmount = totalAmount,
            PlatformFeePercentage = platformFeePercentage,
            PlatformFeeAmount = platformFeeAmount,
            SupplierPayableAmount = supplierPayableAmount,
            Currency = "ZAR",
            Status = "Active",
            PaymentStatus = "Pending",
            GeneratedByAdminID = adminId,
            CreatedDate = invoiceDate
        };

        _db.Invoices.Add(invoice);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new GenerateInvoiceResult
            {
                Success = false,
                Error = "Unable to generate invoice. Please try again later."
            };
        }

        return new GenerateInvoiceResult
        {
            Success = true,
            Invoice = MapToDto(invoice, quotation.Listing.ListingTitle, quotation.Contractor.FullName, quotation.Supplier.FullName),
            ContractorID = quotation.ContractorID,
            ContractorEmail = quotation.Contractor.Email,
            ContractorName = quotation.Contractor.FullName,
            SupplierID = quotation.SupplierID,
            SupplierEmail = quotation.Supplier.Email,
            SupplierName = quotation.Supplier.FullName
        };
    }

    public async Task<InvoiceDto?> GetByIdAsync(int invoiceId, int userId, string role)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Listing)
            .Include(i => i.Contractor)
            .Include(i => i.Supplier)
            .Include(i => i.Quotation)
            .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);

        if (invoice == null) return null;

        var isAdmin = role.Equals("admin", StringComparison.OrdinalIgnoreCase);
        var isParty = invoice.ContractorID == userId || invoice.SupplierID == userId;
        if (!isAdmin && !isParty) return null;

        var dto = MapToDto(invoice, invoice.Listing.ListingTitle, invoice.Contractor.FullName, invoice.Supplier.FullName);

        if (dto.PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            if (invoice.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                dto.PaymentStatus = "Cancelled";
            }
            else if (invoice.Quotation?.BookingID is int bid)
            {
                var bookingCancelled = await _db.Bookings.AsNoTracking()
                    .AnyAsync(b => b.BookingID == bid && b.Status == "Cancelled");
                if (bookingCancelled)
                    dto.PaymentStatus = "Cancelled";
            }
        }

        var isContractorViewer = !isAdmin && invoice.ContractorID == userId;
        if (isContractorViewer)
        {
            dto.PlatformFeePercentage = 0;
            dto.PlatformFeeAmount = 0;
            dto.SupplierPayableAmount = 0;
        }

        return dto;
    }

    public async Task<(IEnumerable<InvoiceListItemDto>, int)> GetAllForUserAsync(
        int userId, string role, int page, int pageSize)
    {
        var q = _db.Invoices.AsNoTracking().Include(i => i.Listing).AsQueryable();

        if (!role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            q = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
                ? q.Where(i => i.ContractorID == userId)
                : q.Where(i => i.SupplierID == userId);
        }

        var total = await q.CountAsync();
        var invoices = await q
            .Include(i => i.Quotation)
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var bookingIds = invoices
            .Where(i => i.Quotation?.BookingID != null)
            .Select(i => i.Quotation!.BookingID!.Value)
            .Distinct()
            .ToList();
        var cancelledBookingIds = bookingIds.Count == 0
            ? new HashSet<int>()
            : (await _db.Bookings.AsNoTracking()
                .Where(b => bookingIds.Contains(b.BookingID) && b.Status == "Cancelled")
                .Select(b => b.BookingID)
                .ToListAsync()).ToHashSet();

        return (invoices.Select(i =>
        {
            var paymentStatus = i.PaymentStatus;
            if (i.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                || (i.Quotation?.BookingID != null && cancelledBookingIds.Contains(i.Quotation.BookingID.Value)))
            {
                if (paymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    paymentStatus = "Cancelled";
            }
            return new InvoiceListItemDto
            {
                InvoiceID = i.InvoiceID,
                InvoiceNumber = i.InvoiceNumber,
                ListingTitle = i.Listing.ListingTitle,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                PaymentStatus = paymentStatus
            };
        }), total);
    }

    private async Task<string> GenerateInvoiceNumberAsync(DateTime invoiceDate)
    {
        var datePart = invoiceDate.ToString("yyyyMMdd");
        var prefix = $"INV-{datePart}-";

        var existing = await _db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();
        var maxSeq = 0;
        foreach (var num in existing)
        {
            var tail = num.Length > prefix.Length ? num[prefix.Length..] : "";
            if (int.TryParse(tail, out var n) && n > maxSeq) maxSeq = n;
        }
        return $"{prefix}{(maxSeq + 1).ToString("D4")}";
    }

    private static InvoiceDto MapToDto(Invoice i, string listingTitle, string contractorName, string supplierName) => new()
    {
        InvoiceID = i.InvoiceID,
        QuotationID = i.QuotationID,
        ListingID = i.ListingID,
        ListingTitle = listingTitle,
        ContractorName = contractorName,
        SupplierName = supplierName,
        InvoiceNumber = i.InvoiceNumber,
        InvoiceDate = i.InvoiceDate,
        DueDate = i.DueDate,
        Subtotal = i.Subtotal,
        DiscountAmount = i.DiscountAmount,
        DeliveryFee = i.DeliveryFee,
        VATRate = i.VATRate,
        VATAmount = i.VATAmount,
        TotalAmount = i.TotalAmount,
        PlatformFeePercentage = i.PlatformFeePercentage,
        PlatformFeeAmount = i.PlatformFeeAmount,
        SupplierPayableAmount = i.SupplierPayableAmount,
        Currency = i.Currency,
        Status = i.Status,
        PaymentStatus = i.PaymentStatus,
        PaymentMethod = !string.IsNullOrWhiteSpace(i.PaymentMethod) ? i.PaymentMethod : (i.TotalAmount > 50000m ? "EFT" : "PayFast"),
        HasEftProof = !string.IsNullOrWhiteSpace(i.EftProofPath),
        EftProofFileName = i.EftProofOriginalName,
        BankName = i.Supplier?.BankName,
        BankAccountName = i.Supplier?.BankAccountName,
        BankAccountNumber = i.Supplier?.BankAccountNumber,
        BankBranchCode = i.Supplier?.BankBranchCode,
        BankAccountType = i.Supplier?.BankAccountType
    };
}
