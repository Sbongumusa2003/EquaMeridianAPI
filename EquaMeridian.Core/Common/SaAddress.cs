using System.ComponentModel.DataAnnotations;
using System.Text;

/// <summary>
/// Structured South African physical address used across listings (depot),
/// quotations, bookings, cart items and returns. Always pair with a formatted
/// single-line string (Location / DeliveryAddress) for PDFs and distance lookup.
/// </summary>
public class SaAddress
{
    public static readonly string[] Provinces =
    {
        "Eastern Cape",
        "Free State",
        "Gauteng",
        "KwaZulu-Natal",
        "Limpopo",
        "Mpumalanga",
        "Northern Cape",
        "North West",
        "Western Cape"
    };

    [StringLength(120)]
    public string? Street { get; set; }

    [StringLength(80)]
    public string? Suburb { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(40)]
    public string? Province { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Street)
        && string.IsNullOrWhiteSpace(Suburb)
        && string.IsNullOrWhiteSpace(City)
        && string.IsNullOrWhiteSpace(Province)
        && string.IsNullOrWhiteSpace(PostalCode);

    /// <summary>
    /// Builds a single-line SA-style address for storage in legacy string columns,
    /// PDFs, SMS and distance services.
    /// </summary>
    public string ToFormatted()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Street)) parts.Add(Street.Trim());
        if (!string.IsNullOrWhiteSpace(Suburb)) parts.Add(Suburb.Trim());
        if (!string.IsNullOrWhiteSpace(City)) parts.Add(City.Trim());
        if (!string.IsNullOrWhiteSpace(Province)) parts.Add(Province.Trim());
        if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode.Trim());
        return string.Join(", ", parts);
    }

    public static SaAddress FromParts(string? street, string? suburb, string? city, string? province, string? postalCode)
        => new()
        {
            Street = NullIfEmpty(street),
            Suburb = NullIfEmpty(suburb),
            City = NullIfEmpty(city),
            Province = NullIfEmpty(province),
            PostalCode = NullIfEmpty(postalCode)
        };

    /// <summary>
    /// Best-effort parse of a legacy one-line address into structured parts.
    /// Handles "Street, Suburb, City, Province, 2196" and city-only values.
    /// </summary>
    public static SaAddress Parse(string? formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted))
            return new SaAddress();

        var text = formatted.Trim();
        if (text.StartsWith("Pickup:", StringComparison.OrdinalIgnoreCase))
            text = text["Pickup:".Length..].Trim();

        var tokens = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return new SaAddress();

        // Single token → treat as city (common legacy case e.g. "Johannesburg")
        if (tokens.Length == 1)
            return new SaAddress { City = tokens[0] };

        var result = new SaAddress();
        var last = tokens[^1];
        var idx = tokens.Length - 1;

        // Trailing postal code (4 digits SA)
        if (last.Length >= 3 && last.Length <= 5 && last.All(char.IsDigit))
        {
            result.PostalCode = last;
            idx--;
            if (idx < 0) return result;
            last = tokens[idx];
        }

        // Province match
        var province = Provinces.FirstOrDefault(p =>
            p.Equals(last, StringComparison.OrdinalIgnoreCase)
            || last.Equals(p.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
        if (province != null)
        {
            result.Province = province;
            idx--;
        }

        if (idx >= 0)
        {
            result.City = tokens[idx];
            idx--;
        }

        if (idx >= 0)
        {
            result.Suburb = tokens[idx];
            idx--;
        }

        if (idx >= 0)
            result.Street = string.Join(", ", tokens.Take(idx + 1));

        return result;
    }

    public static string Format(string? street, string? suburb, string? city, string? province, string? postalCode)
        => FromParts(street, suburb, city, province, postalCode).ToFormatted();

    /// <summary>
    /// Validates a delivery (or depot) address. Street + City required; province recommended.
    /// </summary>
    public (bool Ok, string? Error) Validate(bool requireStreet = true, bool requireProvince = true)
    {
        if (requireStreet && string.IsNullOrWhiteSpace(Street))
            return (false, "Street address is required.");
        if (string.IsNullOrWhiteSpace(City))
            return (false, "City / town is required.");
        if (requireProvince && string.IsNullOrWhiteSpace(Province))
            return (false, "Province is required.");
        if (!string.IsNullOrWhiteSpace(PostalCode))
        {
            var pc = PostalCode.Trim();
            if (pc.Length < 3 || pc.Length > 5 || !pc.All(char.IsDigit))
                return (false, "Postal code must be 3–5 digits.");
        }
        if (!string.IsNullOrWhiteSpace(Province)
            && !Provinces.Any(p => p.Equals(Province.Trim(), StringComparison.OrdinalIgnoreCase)))
            return (false, "Please select a valid South African province.");
        return (true, null);
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>DTO shape shared by API requests that accept a structured SA address.</summary>
public class SaAddressDto
{
    [StringLength(120)]
    public string? Street { get; set; }

    [StringLength(80)]
    public string? Suburb { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(40)]
    public string? Province { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    public SaAddress ToModel() => SaAddress.FromParts(Street, Suburb, City, Province, PostalCode);

    public static SaAddressDto FromModel(SaAddress a) => new()
    {
        Street = a.Street,
        Suburb = a.Suburb,
        City = a.City,
        Province = a.Province,
        PostalCode = a.PostalCode
    };

    public static SaAddressDto? FromFormatted(string? formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted)) return null;
        return FromModel(SaAddress.Parse(formatted));
    }
}
