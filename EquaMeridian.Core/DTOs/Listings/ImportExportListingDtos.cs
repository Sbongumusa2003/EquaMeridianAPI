namespace EquaMeridian.DTOs.Listings
{
    // Business case: lets a supplier's own inventory/ERP system push their equipment list into
    // EquaMeridian programmatically (e.g. a nightly sync job) instead of manual entry, using the
    // same category vocabulary as the Excel import.
    public class ImportListingJsonItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public decimal? WeeklyRate { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? OperatingWeight { get; set; }
        public string? EnginePower { get; set; }
        public string? Location { get; set; }
        public int? UnitsOwned { get; set; }
    }

    public class ImportListingsJsonRequestDto
    {
        public List<ImportListingJsonItemDto> Listings { get; set; } = new();
    }

    // Business case: lets a supplier export their own catalog as JSON to back it up or feed it
    // into another system (e.g. their own website, or an accounting/inventory tool).
    public class ExportListingJsonItemDto
    {
        public int ListingID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public decimal? WeeklyRate { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
