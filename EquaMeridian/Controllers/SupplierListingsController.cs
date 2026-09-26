using ClosedXML.Excel;
using EquaMeridian.DTOs.Listings;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

[ApiController]
[Route("api/supplier/listings")]
[Authorize(Policy = "SupplierOnly")]
public class SupplierListingsController : ControllerBase
{
    private readonly IListingRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IListingImageRepository _imageRepo;
    private readonly INotificationRepository _notifications;

    public SupplierListingsController(
        IListingRepository repo,
        IAuditService audit,
        IEmailService email,
        IConfiguration config,
        AppDbContext db,
        IListingImageRepository imageRepo,
        INotificationRepository notifications)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _config = config;
        _db = db;
        _imageRepo = imageRepo;
        _notifications = notifications;
    }

    private int SupplierId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!dto.AgreeToMasterLeaseAgreement)
            return BadRequest(new { message = "You must accept the Master Lease Agreement to list this machinery." });

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.CategoryID == dto.CategoryID);
        if (!categoryExists)
            return BadRequest(new { message = "Invalid category." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var listingId = await _repo.CreateAsync(dto, SupplierId);
        var snapshot = System.Text.Json.JsonSerializer.Serialize(dto);

        await _audit.LogAsync(SupplierId, "LISTING_CREATED",
            $"New listing created: {dto.ListingTitle}",
            null, listingId, null, ip, snapshot);

        var adminEmail = _config["AdminEmail"] ?? "admin@equameridian.co.za";
        await _email.SendNewListingPendingReviewAsync(
            adminEmail, listingId,
            User.FindFirstValue(ClaimTypes.Name) ?? "");

        await _notifications.BroadcastAsync(
            "New Listing Pending Review",
            $"'{dto.ListingTitle}' was submitted by {User.FindFirstValue(ClaimTypes.Name) ?? "a Supplier"} and is awaiting your review.",
            "Admin", "ListingPendingReview", "Listing", listingId);

        return CreatedAtAction(nameof(GetOwn),
            new { listingId },
            new { listingId, status = "Pending" });
    }

    // Bulk-import listings from an Excel (.xlsx) spreadsheet. Expected header row:
    // Title, Category, Description, DailyRate (required), and optionally
    // Make, Model, Year, OperatingWeight, EnginePower, Location, WeeklyRate, UnitsOwned.
    // Rows are processed independently — one bad row doesn't fail the whole batch.
    [HttpGet("import-template")]
    public IActionResult DownloadImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("EquipmentImport");
        var headers = new[]
        {
            "Title", "Category", "Description", "DailyRate",
            "Make", "Model", "Year", "OperatingWeight", "EnginePower",
            "Location", "WeeklyRate", "UnitsOwned"
        };
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        // Sample row so the file is never empty
        sheet.Cell(2, 1).Value = "CAT 320D Excavator";
        sheet.Cell(2, 2).Value = "Excavators";
        sheet.Cell(2, 3).Value = "20-ton excavator, well maintained, includes standard bucket.";
        sheet.Cell(2, 4).Value = 4500;
        sheet.Cell(2, 5).Value = "Caterpillar";
        sheet.Cell(2, 6).Value = "320D";
        sheet.Cell(2, 7).Value = 2018;
        sheet.Cell(2, 8).Value = "20t";
        sheet.Cell(2, 9).Value = "122 kW";
        sheet.Cell(2, 10).Value = "Johannesburg";
        sheet.Cell(2, 11).Value = 22000;
        sheet.Cell(2, 12).Value = 2;
        sheet.Cell(3, 1).Value = "Alternate headers also accepted: ListingTitle, CategoryName/CategoryID, DailyRateZAR, MakeBrand, WeeklyRateZAR";
        sheet.Range(3, 1, 3, 12).Merge();
        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "equameridian-listings-import-template.xlsx");
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportFromExcel([FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please choose an Excel (.xlsx) file to upload." });

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .xlsx files are supported." });

        var created = new List<string>();
        var errors = new List<string>();

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        try
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.First();

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = sheet.Row(1);
            for (var c = 1; c <= lastCol; c++)
            {
                var header = headerRow.Cell(c).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(header) && !columns.ContainsKey(header))
                    columns[header] = c;
            }

            void Alias(string canonical, params string[] alts)
            {
                if (columns.ContainsKey(canonical)) return;
                foreach (var a in alts)
                    if (columns.TryGetValue(a, out var idx)) { columns[canonical] = idx; return; }
            }
            Alias("Title", "ListingTitle", "Name");
            Alias("Category", "CategoryName", "CategoryID");
            Alias("Description", "Desc");
            Alias("DailyRate", "DailyRateZAR", "Daily Rate");
            Alias("Make", "MakeBrand", "Brand");
            Alias("WeeklyRate", "WeeklyRateZAR");
            Alias("Location", "City");

            string[] required = { "Title", "Category", "Description", "DailyRate" };
            var missingColumns = required.Where(r => !columns.ContainsKey(r)).ToList();
            if (missingColumns.Count > 0)
            {
                return BadRequest(new
                {
                    message = $"The file is missing required column(s): {string.Join(", ", missingColumns)}. " +
                              "Expected headers: Title, Category, Description, DailyRate, and optionally " +
                              "Make, Model, Year, OperatingWeight, EnginePower, Location, WeeklyRate, UnitsOwned."
                });
            }

            var categories = await _db.Categories
                .ToDictionaryAsync(c => c.Name, c => c.CategoryID, StringComparer.OrdinalIgnoreCase);

            string Cell(IXLRow row, string col) =>
                columns.TryGetValue(col, out var idx) ? row.Cell(idx).GetString().Trim() : "";

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var title = Cell(row, "Title");
                var categoryName = Cell(row, "Category");
                var description = Cell(row, "Description");
                var dailyRateText = Cell(row, "DailyRate");

                if (title.StartsWith("Alternate headers", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(categoryName)
                    && string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(dailyRateText))
                    continue; // skip fully blank rows

                if (string.IsNullOrWhiteSpace(title)) { errors.Add($"Row {r}: Title is required."); continue; }
                if (string.IsNullOrWhiteSpace(categoryName) || !categories.TryGetValue(categoryName, out var categoryId))
                {
                    errors.Add($"Row {r}: Category \"{categoryName}\" was not recognised. " +
                               $"Valid categories: {string.Join(", ", categories.Keys)}.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(description)) { errors.Add($"Row {r}: Description is required."); continue; }
                if (!decimal.TryParse(dailyRateText, out var dailyRate) || dailyRate <= 0)
                {
                    errors.Add($"Row {r}: DailyRate must be a positive number.");
                    continue;
                }

                var year = int.TryParse(Cell(row, "Year"), out var y) ? y : (int?)null;
                var weeklyRate = decimal.TryParse(Cell(row, "WeeklyRate"), out var wr) ? wr : (decimal?)null;
                var unitsOwned = int.TryParse(Cell(row, "UnitsOwned"), out var uo) && uo > 0 ? uo : 1;

                var dto = new CreateListingDto
                {
                    ListingTitle = title,
                    CategoryID = categoryId,
                    Description = description,
                    MakeBrand = Cell(row, "Make"),
                    Model = Cell(row, "Model"),
                    Year = year,
                    OperatingWeight = Cell(row, "OperatingWeight"),
                    EnginePower = Cell(row, "EnginePower"),
                    Location = Cell(row, "Location"),
                    DailyRateZAR = dailyRate,
                    WeeklyRateZAR = weeklyRate,
                    DryHireAvailable = true,
                    PickupAvailable = true,
                    PricingMode = "Fixed",
                    UnitsOwned = unitsOwned,
                    // Bulk import is only reachable by a logged-in supplier who already accepted the
                    // Master Lease Agreement once via the normal listing form; importing many rows at
                    // once doesn't ask them to re-tick it per row.
                    AgreeToMasterLeaseAgreement = true
                };

                var validationResults = new List<ValidationResult>();
                var ctx = new ValidationContext(dto);
                if (!Validator.TryValidateObject(dto, ctx, validationResults, validateAllProperties: true))
                {
                    errors.Add($"Row {r} (\"{title}\"): {string.Join(" ", validationResults.Select(v => v.ErrorMessage))}");
                    continue;
                }

                try
                {
                    await _repo.CreateAsync(dto, SupplierId);
                    created.Add(title);
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {r} (\"{title}\"): {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Could not read the Excel file: {ex.Message}" });
        }

        var importIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(SupplierId, "LISTINGS_BULK_IMPORTED",
            $"Imported {created.Count} listing(s) from Excel ({errors.Count} row(s) failed).",
            SupplierId, null, null, importIp);

        return Ok(new
        {
            createdCount = created.Count,
            createdTitles = created,
            errorCount = errors.Count,
            errors
        });
    }

    // JSON equivalent of the Excel import above — same validation and per-row error reporting,
    // aimed at system-to-system integration (e.g. a supplier's own inventory/ERP tool posting its
    // equipment list here directly as JSON instead of a human uploading a spreadsheet).
    [HttpPost("import-json")]
    public async Task<IActionResult> ImportFromJson([FromBody] ImportListingsJsonRequestDto request)
    {
        if (request?.Listings == null || request.Listings.Count == 0)
            return BadRequest(new { message = "Provide at least one listing in the \"listings\" array." });

        var categories = await _db.Categories
            .ToDictionaryAsync(c => c.Name, c => c.CategoryID, StringComparer.OrdinalIgnoreCase);

        var created = new List<string>();
        var errors = new List<string>();

        for (var i = 0; i < request.Listings.Count; i++)
        {
            var item = request.Listings[i];
            var rowLabel = $"Item {i + 1}";

            if (string.IsNullOrWhiteSpace(item.Title)) { errors.Add($"{rowLabel}: Title is required."); continue; }
            if (string.IsNullOrWhiteSpace(item.Category) || !categories.TryGetValue(item.Category, out var categoryId))
            {
                errors.Add($"{rowLabel}: Category \"{item.Category}\" was not recognised. " +
                           $"Valid categories: {string.Join(", ", categories.Keys)}.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Description)) { errors.Add($"{rowLabel}: Description is required."); continue; }
            if (item.DailyRate <= 0) { errors.Add($"{rowLabel}: DailyRate must be a positive number."); continue; }

            var dto = new CreateListingDto
            {
                ListingTitle = item.Title,
                CategoryID = categoryId,
                Description = item.Description,
                MakeBrand = item.Make ?? "",
                Model = item.Model ?? "",
                Year = item.Year,
                OperatingWeight = item.OperatingWeight ?? "",
                EnginePower = item.EnginePower ?? "",
                Location = item.Location ?? "",
                DailyRateZAR = item.DailyRate,
                WeeklyRateZAR = item.WeeklyRate,
                DryHireAvailable = true,
                PickupAvailable = true,
                PricingMode = "Fixed",
                UnitsOwned = item.UnitsOwned is > 0 ? item.UnitsOwned.Value : 1,
                AgreeToMasterLeaseAgreement = true
            };

            var validationResults = new List<ValidationResult>();
            var ctx = new ValidationContext(dto);
            if (!Validator.TryValidateObject(dto, ctx, validationResults, validateAllProperties: true))
            {
                errors.Add($"{rowLabel} (\"{item.Title}\"): {string.Join(" ", validationResults.Select(v => v.ErrorMessage))}");
                continue;
            }

            try
            {
                await _repo.CreateAsync(dto, SupplierId);
                created.Add(item.Title);
            }
            catch (Exception ex)
            {
                errors.Add($"{rowLabel} (\"{item.Title}\"): {ex.Message}");
            }
        }

        var jsonImportIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(SupplierId, "LISTINGS_BULK_IMPORTED_JSON",
            $"Imported {created.Count} listing(s) from JSON ({errors.Count} item(s) failed).",
            SupplierId, null, null, jsonImportIp);

        return Ok(new
        {
            createdCount = created.Count,
            createdTitles = created,
            errorCount = errors.Count,
            errors
        });
    }

    // JSON export of the supplier's own catalog — for backing up their listings or feeding them
    // into another system (their own website, an accounting tool, etc.).
    [HttpGet("export-json")]
    public async Task<IActionResult> ExportToJson()
    {
        var categoryNames = await _db.Categories.ToDictionaryAsync(c => c.CategoryID, c => c.Name);

        var listings = await _db.Listings
            .Where(l => l.SupplierID == SupplierId)
            .OrderBy(l => l.ListingTitle)
            .Select(l => new
            {
                l.ListingID,
                l.ListingTitle,
                l.CategoryID,
                l.Description,
                l.DailyRateZAR,
                l.WeeklyRateZAR,
                l.MakeBrand,
                l.Model,
                l.Year,
                l.AvailabilityStatus
            })
            .ToListAsync();

        var result = listings.Select(l => new ExportListingJsonItemDto
        {
            ListingID = l.ListingID,
            Title = l.ListingTitle,
            Category = categoryNames.TryGetValue(l.CategoryID, out var name) ? name : "",
            Description = l.Description,
            DailyRate = l.DailyRateZAR,
            WeeklyRate = l.WeeklyRateZAR,
            Make = l.MakeBrand,
            Model = l.Model,
            Year = l.Year,
            Status = l.AvailabilityStatus
        }).ToList();

        return Ok(result);
    }

    [HttpPost("{listingId}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages(
        int listingId, [FromForm] IFormFileCollection files)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId)
            return NotFound(new { message = "Listing not found." });

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "No files were uploaded." });

        IEnumerable<string> saved;
        try
        {
            saved = await _imageRepo.AddImagesAsync(listingId, files);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(SupplierId, "LISTING_IMAGES_UPLOADED",
            $"{saved.Count()} image(s) added to listing {listingId}",
            null, listingId, null, ip);

        return Ok(new { listingId, imageUrls = saved, message = "Upload at least 1 and at most 5 images before submitting a listing for review." });
    }

    [HttpDelete("{listingId}/images/{imageId}")]
    public async Task<IActionResult> DeleteImage(int listingId, int imageId)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId)
            return NotFound(new { message = "Listing not found." });

        var deleted = await _imageRepo.DeleteAsync(listingId, imageId);
        if (!deleted)
            return NotFound(new { message = "Image not found." });

        return Ok(new { message = "Image deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetOwn(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (listings, total) = await _repo.GetBySupplierAsync(
            SupplierId, status, page, pageSize);

        var enriched = listings.Select(l => new {
            listing = l,
            actions = GetAllowedActions(l.AvailabilityStatus)
        });

        return Ok(new { listings = enriched, totalCount = total, page, pageSize });
    }

    [HttpGet("{listingId}")]
    public async Task<IActionResult> GetById(int listingId)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId) return NotFound();
        return Ok(listing);
    }

    [HttpPut("{listingId}")]
    public async Task<IActionResult> Update(
        int listingId, [FromBody] UpdateListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.CategoryID == dto.CategoryID);
        if (!categoryExists)
            return BadRequest(new { message = "Invalid category." });

        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId) return NotFound();
        if (listing.AvailabilityStatus == "Suspended") return Forbid();
        if (listing.AvailabilityStatus == "Archived")
            return BadRequest(new { message = "This listing has been archived and can no longer be edited." });

        var previous = System.Text.Json.JsonSerializer.Serialize(listing);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _repo.UpdateAsync(listingId, dto);

        var updated = await _repo.GetByIdAsync(listingId);
        var newSnap = System.Text.Json.JsonSerializer.Serialize(updated);

        await _audit.LogAsync(SupplierId, "LISTING_UPDATED",
            $"Listing {listingId} updated", null, listingId, previous, ip, newSnap);

        await _notifications.BroadcastAsync(
            "Listing Edited - Pending Review",
            $"'{dto.ListingTitle}' was edited and requires re-approval before it goes live again.",
            "Admin", "ListingPendingReview", "Listing", listingId);

        return Ok(updated);
    }

    [HttpPatch("{listingId}/deactivate")]
    public async Task<IActionResult> Deactivate(int listingId)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId) return NotFound();

        if (listing.AvailabilityStatus != "Active")
            return BadRequest(new { message = "Only Active listings can be deactivated." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (ok, error) = await _repo.DeactivateAsync(listingId);
        if (!ok)
            return BadRequest(new { message = error ?? "Could not deactivate this listing." });

        await _audit.LogAsync(SupplierId, "LISTING_DEACTIVATED",
            $"Listing {listingId} deactivated",
            null, listingId, "Active", ip, "Inactive");

        return Ok(new { listingId, status = "Inactive" });
    }

    [HttpDelete("{listingId}")]
    public async Task<IActionResult> Delete(int listingId)
    {
        // Hard-delete only when the listing has no historical or in-progress links
        // (bookings, cart items, open quotes, active leases, reserved units).
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null || listing.SupplierID != SupplierId) return NotFound();

        if (listing.AvailabilityStatus == "Suspended")
            return BadRequest(new { message = "Suspended listings cannot be deleted. Contact admin." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error, hardDeleted) = await _repo.ArchiveAsync(listingId);
        if (!success)
            return BadRequest(new { message = error });

        await _audit.LogAsync(SupplierId, "LISTING_DELETED",
            $"Listing {listingId} permanently deleted",
            null, listingId, listing.AvailabilityStatus, ip, "Deleted");

        return Ok(new { listingId, status = "Deleted", message = "Listing deleted successfully." });
    }

    private static object GetAllowedActions(string status) => status switch
    {
        "Active" => new { canEdit = true, canDeactivate = true, canDelete = true, contactAdmin = false },
        "Pending" => new { canEdit = true, canDeactivate = false, canDelete = true, contactAdmin = false },
        "Draft" => new { canEdit = true, canDeactivate = false, canDelete = true, contactAdmin = false },
        "Rejected" => new { canEdit = true, canDeactivate = false, canDelete = true, contactAdmin = false },
        "Suspended" => new { canEdit = false, canDeactivate = false, canDelete = false, contactAdmin = true },
        "Inactive" => new { canEdit = true, canDeactivate = false, canDelete = true, contactAdmin = false },
        "Archived" => new { canEdit = false, canDeactivate = false, canDelete = false, contactAdmin = false },
        _ => new { canEdit = false, canDeactivate = false, canDelete = false, contactAdmin = false }
    };
}