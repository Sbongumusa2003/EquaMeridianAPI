namespace EquaMeridian.DTOs.Listings
{
    public class BrowseQuery
    {
        public string? Search { get; set; }
        public int? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Location { get; set; }
        public string? MakeBrand { get; set; }
        public string? Model { get; set; }
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public bool? WetHire { get; set; }
        public bool? DryHire { get; set; }
        public bool? DeliveryAvailable { get; set; }
        public bool? PickupAvailable { get; set; }
        public decimal? MinRating { get; set; }
        public string? PricingMode { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public int? SupplierId { get; set; }
        public string Sort { get; set; } = "relevance";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public class BrowseFacetValue
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BrowseFacetsDto
    {
        public List<BrowseFacetValue> Categories { get; set; } = new();
        public List<BrowseFacetValue> Makes { get; set; } = new();
        public List<BrowseFacetValue> Locations { get; set; } = new();
        public List<BrowseFacetValue> PricingModes { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int TotalActive { get; set; }
    }

    public class BrowseResultDto
    {
        public IEnumerable<ListingDto> Listings { get; set; } = Array.Empty<ListingDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public BrowseFacetsDto Facets { get; set; } = new();
    }

    public class SupplierStorefrontDto
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? LocationSummary { get; set; }
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int ActiveListings { get; set; }
        public bool Verified { get; set; }
        public IEnumerable<ListingDto> Listings { get; set; } = Array.Empty<ListingDto>();
    }

    public class DeliveryQuoteDto
    {
        public decimal DistanceKm { get; set; }
        public decimal FeeZAR { get; set; }
        public string Method { get; set; } = "Supplier Delivery";
        public string Notes { get; set; } = string.Empty;
    }
}
