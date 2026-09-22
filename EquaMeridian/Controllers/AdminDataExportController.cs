using EquaMeridian.DTOs.DataExport;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

/// <summary>
/// Req 3.8: every export of platform data requires a selected business-justification
/// reason before it is generated, and every export writes an audit-log entry recording
/// who exported, when, the reason, and the scope/filters used (POPIA accountability).
/// </summary>
[ApiController]
[Route("api/admin/data-export")]
[Authorize(Policy = "AdminOnly")]
public class AdminDataExportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AdminDataExportController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("listings")]
    public async Task<IActionResult> ExportListings(
        [FromQuery] string format = "json",
        [FromQuery] string? reason = null,
        [FromQuery] string? reasonDetails = null,
        [FromQuery] string? status = null)
    {
        var validation = ValidateReason(reason, reasonDetails);
        if (validation != null) return validation;

        var query = _db.Listings.AsNoTracking().Include(l => l.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.AvailabilityStatus == status);

        var listings = await query
            .Select(l => new ListingExportDto
            {
                ListingID = l.ListingID,
                ListingTitle = l.ListingTitle,
                CategoryName = _db.Categories.Where(c => c.CategoryID == l.CategoryID).Select(c => c.Name).FirstOrDefault() ?? "Uncategorized",
                AvailabilityStatus = l.AvailabilityStatus,
                MakeBrand = l.MakeBrand,
                Model = l.Model,
                Year = l.Year,
                Location = l.Location,
                DailyRateZAR = l.DailyRateZAR,
                SupplierName = l.Supplier.FullName,
                AverageRating = l.AverageRating,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        await LogExportAsync("Listings", reason!, reasonDetails, format, new { status }, listings.Count);

        return format.Equals("xml", StringComparison.OrdinalIgnoreCase)
            ? ExportXml(new ListingExportWrapper { Listings = listings }, "listings")
            : ExportJson(listings, "listings");
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> ExportBookings(
        [FromQuery] string format = "json",
        [FromQuery] string? reason = null,
        [FromQuery] string? reasonDetails = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var validation = ValidateReason(reason, reasonDetails);
        if (validation != null) return validation;

        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Listing).Include(b => b.Supplier).Include(b => b.Contractor)
            .AsQueryable();
        if (fromDate.HasValue) query = query.Where(b => b.RentalStartDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(b => b.RentalEndDate <= toDate.Value);

        var bookings = await query
            .Select(b => new BookingExportDto
            {
                BookingID = b.BookingID,
                ListingTitle = b.Listing.ListingTitle,
                SupplierName = b.Supplier.FullName,
                ContractorName = b.Contractor.FullName,
                RentalStartDate = b.RentalStartDate,
                RentalEndDate = b.RentalEndDate,
                Status = b.Status,
                CreatedDate = b.CreatedDate
            })
            .ToListAsync();

        await LogExportAsync("Bookings", reason!, reasonDetails, format, new { fromDate, toDate }, bookings.Count);

        return format.Equals("xml", StringComparison.OrdinalIgnoreCase)
            ? ExportXml(new BookingExportWrapper { Bookings = bookings }, "bookings")
            : ExportJson(bookings, "bookings");
    }

    [HttpGet("users")]
    public async Task<IActionResult> ExportUsers(
        [FromQuery] string format = "json",
        [FromQuery] string? reason = null,
        [FromQuery] string? reasonDetails = null,
        [FromQuery] string? role = null)
    {
        var validation = ValidateReason(reason, reasonDetails);
        if (validation != null) return validation;

        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role.ToLower() == role.ToLower());

        var users = await query
            .Select(u => new UserExportDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                CompanyName = u.CompanyName,
                AccountStatus = u.AccountStatus,
                CreatedDate = u.CreatedDate
            })
            .ToListAsync();

        // Req 3.8 / POPIA: exporting personal data (emails, names) is exactly the kind of
        // action this audit trail exists for, so this is logged the same as any other export.
        await LogExportAsync("Users", reason!, reasonDetails, format, new { role }, users.Count);

        return format.Equals("xml", StringComparison.OrdinalIgnoreCase)
            ? ExportXml(new UserExportWrapper { Users = users }, "users")
            : ExportJson(users, "users");
    }

    [HttpGet("financials")]
    public async Task<IActionResult> ExportFinancials(
        [FromQuery] string format = "json",
        [FromQuery] string? reason = null,
        [FromQuery] string? reasonDetails = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? paymentStatus = null)
    {
        var validation = ValidateReason(reason, reasonDetails);
        if (validation != null) return validation;

        var query = _db.Invoices.AsNoTracking()
            .Include(i => i.Listing).Include(i => i.Supplier).Include(i => i.Contractor)
            .AsQueryable();
        if (fromDate.HasValue) query = query.Where(i => i.InvoiceDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(i => i.InvoiceDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(i => i.PaymentStatus == paymentStatus);

        var records = await query
            .Select(i => new FinancialExportDto
            {
                InvoiceID = i.InvoiceID,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                ListingTitle = i.Listing.ListingTitle,
                SupplierName = i.Supplier.FullName,
                ContractorName = i.Contractor.FullName,
                Subtotal = i.Subtotal,
                DiscountAmount = i.DiscountAmount,
                DeliveryFee = i.DeliveryFee,
                VATRate = i.VATRate,
                VATAmount = i.VATAmount,
                TotalAmount = i.TotalAmount,
                PlatformFeeAmount = i.PlatformFeeAmount,
                SupplierPayableAmount = i.SupplierPayableAmount,
                PaymentStatus = i.PaymentStatus
            })
            .ToListAsync();

        await LogExportAsync("Financials", reason!, reasonDetails, format,
            new { fromDate, toDate, paymentStatus }, records.Count);

        return format.Equals("xml", StringComparison.OrdinalIgnoreCase)
            ? ExportXml(new FinancialExportWrapper { Records = records }, "financials")
            : ExportJson(records, "financials");
    }

    private IActionResult? ValidateReason(string? reason, string? reasonDetails)
    {
        if (!DataExportReasons.IsValid(reason))
            return BadRequest(new
            {
                message = "A business-justification reason is required before this export can be generated.",
                allowedReasons = DataExportReasons.Allowed
            });

        if (reason == "Other" && string.IsNullOrWhiteSpace(reasonDetails))
            return BadRequest(new { message = "Please provide details when selecting 'Other' as the export reason." });

        return null;
    }

    private async Task LogExportAsync(string dataset, string reason, string? reasonDetails, string format, object filters, int recordCount)
    {
        var scope = JsonSerializer.Serialize(new { dataset, format, recordCount, filters });
        await _audit.LogAsync(null, "DATA_EXPORTED",
            $"Exported {recordCount} {dataset} record(s) as {format.ToUpper()}. Reason: {reason}" +
            (reason == "Other" ? $" ({reasonDetails})" : ""),
            AdminId, null, null, Ip, scope);
    }

    private FileContentResult ExportJson<T>(T data, string name)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"{name}-export-{AppTime.Now:yyyyMMdd-HHmmss}.json");
    }

    private FileContentResult ExportXml<T>(T data, string name) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, data);
        return File(stream.ToArray(), "application/xml", $"{name}-export-{AppTime.Now:yyyyMMdd-HHmmss}.xml");
    }
}
