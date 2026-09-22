using EquaMeridian.DTOs.Inspections;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class InspectionRepository : IInspectionRepository
{
    private static readonly HashSet<string> ValidOutcomes = new() { "Pass", "Fail" };

    private readonly AppDbContext _db;
    public InspectionRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<InspectionListItemDto>, int)> GetAllAsync(
        string? status, int page, int pageSize)
    {
        var q = _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);

        var total = await q.CountAsync();
        var inspections = await q
            .OrderByDescending(i => i.ScheduledDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (inspections.Select(MapToListItemDto), total);
    }

    public async Task<InspectionListItemDto?> GetByIdAsync(int inspectionId)
    {
        var inspection = await _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId);

        return inspection == null ? null : MapToListItemDto(inspection);
    }

    public async Task<IEnumerable<MachineryOptionDto>> GetAvailableMachineryAsync()
    {
        return await _db.Listings
            .Include(l => l.Supplier)
            .OrderBy(l => l.ListingTitle)
            .Select(l => new MachineryOptionDto
            {
                ListingID = l.ListingID,
                MachineryTitle = l.ListingTitle,
                Company = l.Supplier.CompanyName ?? l.Supplier.FullName,
                Status = l.AvailabilityStatus
            })
            .ToListAsync();
    }

    public async Task<InspectionRequestResult> RequestAsync(RequestInspectionDto dto, int adminId)
    {
        var listing = await _db.Listings
            .Include(l => l.Supplier)
            .FirstOrDefaultAsync(l => l.ListingID == dto.ListingID);

        if (listing == null)
            return new InspectionRequestResult { Success = false, Error = "Machinery listing not found." };

        if (dto.ScheduledDate.Date < AppTime.Now.Date)
            return new InspectionRequestResult { Success = false, Error = "Inspection date must be in the future." };

        var inspection = new Inspection
        {
            ListingID = dto.ListingID,
            RequestedByAdminID = adminId,
            ScheduledDate = dto.ScheduledDate,
            Status = "Requested",
            RequestedDate = AppTime.Now
        };

        _db.Inspections.Add(inspection);
        await _db.SaveChangesAsync();

        inspection.Listing = listing;

        return new InspectionRequestResult
        {
            Success = true,
            Inspection = MapToListItemDto(inspection),
            SupplierID = listing.SupplierID,
            SupplierEmail = listing.Supplier.Email,
            SupplierName = listing.Supplier.FullName
        };
    }

    public async Task<(IEnumerable<InspectionListItemDto>, int)> GetAllForSupplierAsync(
        int supplierId, string? status, int page, int pageSize)
    {
        var q = _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .Where(i => i.Listing.SupplierID == supplierId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);

        var total = await q.CountAsync();
        var inspections = await q
            .OrderByDescending(i => i.ScheduledDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (inspections.Select(MapToListItemDto), total);
    }

    public async Task<InspectionOutcomeDto?> GetForOutcomeAsync(int inspectionId, int supplierId)
    {
        var inspection = await _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId && i.Listing.SupplierID == supplierId);

        return inspection == null ? null : MapToOutcomeDto(inspection);
    }

    public async Task<InspectionOutcomeDto?> GetForOutcomeAsAdminAsync(int inspectionId)
    {
        var inspection = await _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId);
        return inspection == null ? null : MapToOutcomeDto(inspection);
    }

    public async Task<InspectionConfirmResult> ConfirmOutcomeAsAdminAsync(int inspectionId, int adminId, ConfirmOutcomeDto dto)
    {
        if (!ValidOutcomes.Contains(dto.Outcome))
            return new InspectionConfirmResult { Success = false, Error = "Outcome must be Pass or Fail." };

        var inspection = await _db.Inspections
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .Include(i => i.RequestedByAdmin)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId);

        if (inspection == null)
            return new InspectionConfirmResult { Success = false, Error = "Inspection not found." };

        if (inspection.Status != "Requested")
            return new InspectionConfirmResult
            {
                Success = false,
                Error = $"Inspection outcome cannot be confirmed because its current status is '{inspection.Status}'."
            };

        inspection.Outcome = dto.Outcome;
        inspection.Notes = dto.Notes;
        inspection.Status = "Completed";
        inspection.OutcomeConfirmedDate = AppTime.Now;
        await _db.SaveChangesAsync();

        return new InspectionConfirmResult
        {
            Success = true,
            Inspection = MapToOutcomeDto(inspection),
            RequesterID = inspection.RequestedByAdminID,
            SupplierID = inspection.Listing.SupplierID,
            RequesterEmail = inspection.RequestedByAdmin.Email,
            RequesterName = inspection.RequestedByAdmin.FullName,
            SupplierEmail = inspection.Listing.Supplier.Email,
            SupplierName = inspection.Listing.Supplier.FullName
        };
    }

    public async Task<InspectionOutcomeDto?> GetForOutcomeAsContractorAsync(int inspectionId, int contractorId)
    {
        var inspection = await _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId && i.RequestedByAdminID == contractorId);
        return inspection == null ? null : MapToOutcomeDto(inspection);
    }

    public async Task<InspectionConfirmResult> ConfirmOutcomeAsContractorAsync(int inspectionId, int contractorId, ConfirmOutcomeDto dto)
    {
        if (!ValidOutcomes.Contains(dto.Outcome))
            return new InspectionConfirmResult { Success = false, Error = "Outcome must be Pass or Fail." };

        var inspection = await _db.Inspections
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .Include(i => i.RequestedByAdmin)
            .FirstOrDefaultAsync(i => i.InspectionID == inspectionId && i.RequestedByAdminID == contractorId);

        if (inspection == null)
            return new InspectionConfirmResult { Success = false, Error = "Inspection not found." };

        if (inspection.Status != "Requested")
            return new InspectionConfirmResult
            {
                Success = false,
                Error = $"Inspection outcome cannot be confirmed because its current status is '{inspection.Status}'."
            };

        inspection.Outcome = dto.Outcome;
        inspection.Notes = dto.Notes;
        inspection.Status = "Completed";
        inspection.OutcomeConfirmedDate = AppTime.Now;
        await _db.SaveChangesAsync();

        return new InspectionConfirmResult
        {
            Success = true,
            Inspection = MapToOutcomeDto(inspection),
            RequesterID = inspection.RequestedByAdminID,
            SupplierID = inspection.Listing.SupplierID,
            RequesterEmail = inspection.RequestedByAdmin.Email,
            RequesterName = inspection.RequestedByAdmin.FullName,
            SupplierEmail = inspection.Listing.Supplier.Email,
            SupplierName = inspection.Listing.Supplier.FullName
        };
    }

    public async Task<IEnumerable<MachineryOptionDto>> GetActiveMachineryAsync()
    {
        return await _db.Listings
            .Include(l => l.Supplier)
            .Where(l => l.AvailabilityStatus == "Active")
            .OrderBy(l => l.ListingTitle)
            .Select(l => new MachineryOptionDto
            {
                ListingID = l.ListingID,
                MachineryTitle = l.ListingTitle,
                Company = l.Supplier.CompanyName ?? l.Supplier.FullName,
                Status = l.AvailabilityStatus
            })
            .ToListAsync();
    }

    public async Task<(IEnumerable<InspectionListItemDto>, int)> GetAllForContractorAsync(
        int contractorId, string? status, int page, int pageSize)
    {
        var q = _db.Inspections
            .AsNoTracking()
            .Include(i => i.Listing).ThenInclude(l => l.Supplier)
            .Where(i => i.RequestedByAdminID == contractorId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);

        var total = await q.CountAsync();
        var inspections = await q
            .OrderByDescending(i => i.ScheduledDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (inspections.Select(MapToListItemDto), total);
    }

    private static InspectionListItemDto MapToListItemDto(Inspection i) => new()
    {
        InspectionID = i.InspectionID,
        ListingID = i.ListingID,
        MachineryTitle = i.Listing.ListingTitle,
        SupplierName = i.Listing.Supplier.CompanyName ?? i.Listing.Supplier.FullName,
        ScheduledDate = i.ScheduledDate,
        Status = i.Status,
        Outcome = i.Outcome
    };

    private static InspectionOutcomeDto MapToOutcomeDto(Inspection i) => new()
    {
        InspectionID = i.InspectionID,
        ListingID = i.ListingID,
        MachineryTitle = i.Listing.ListingTitle,
        SupplierName = i.Listing.Supplier.CompanyName ?? i.Listing.Supplier.FullName,
        ScheduledDate = i.ScheduledDate,
        Status = i.Status,
        Outcome = i.Outcome,
        Notes = i.Notes
    };
}
