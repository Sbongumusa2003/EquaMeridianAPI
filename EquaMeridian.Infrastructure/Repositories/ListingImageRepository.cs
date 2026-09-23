using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class ListingImageRepository : IListingImageRepository
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly Dictionary<string, string> ExtToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };
    private const int MaxImagesPerListing = 5;
    private const long MaxBytesPerFile = 10 * 1024 * 1024;

    private readonly AppDbContext _db;

    public ListingImageRepository(AppDbContext db)
    {
        _db = db;
    }

    public static string PublicUrlFor(int imageId) => $"/api/media/listing-images/{imageId}";

    public async Task<IEnumerable<string>> GetUrlsByListingAsync(int listingId)
        => await _db.ListingImages
            .Where(i => i.ListingID == listingId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => PublicUrlFor(i.ImageID))
            .ToListAsync();

    public const int MinImagesPerListing = 1;
    public const int MaxImagesAllowed = MaxImagesPerListing;

    public async Task<int> CountAsync(int listingId)
        => await _db.ListingImages.CountAsync(i => i.ListingID == listingId);

    public async Task<IEnumerable<string>> AddImagesAsync(
        int listingId, IEnumerable<IFormFile> files)
    {
        var existingCount = await _db.ListingImages
            .CountAsync(i => i.ListingID == listingId);

        if (existingCount >= MaxImagesPerListing)
            throw new InvalidOperationException(
                $"A listing may have at most {MaxImagesPerListing} images. Remove an existing image before uploading more.");

        var saved = new List<string>();
        var order = existingCount;

        foreach (var file in files)
        {
            if (existingCount + saved.Count >= MaxImagesPerListing)
                break;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                continue;

            if (file.Length <= 0 || file.Length > MaxBytesPerFile)
                continue;

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) continue;

            var contentType = file.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream")
                contentType = ExtToContentType.GetValueOrDefault(ext, "application/octet-stream");

            var entity = new ListingImage
            {
                ListingID = listingId,
                // Placeholder until we have ImageID; updated after SaveChanges.
                FilePath = string.Empty,
                Content = bytes,
                ContentType = contentType,
                DisplayOrder = order++,
                UploadedDate = AppTime.Now
            };

            _db.ListingImages.Add(entity);
            await _db.SaveChangesAsync();

            entity.FilePath = PublicUrlFor(entity.ImageID);
            await _db.SaveChangesAsync();

            saved.Add(entity.FilePath);
        }

        return saved;
    }

    public async Task<(byte[] Content, string ContentType)?> GetContentAsync(int imageId)
    {
        var row = await _db.ListingImages
            .AsNoTracking()
            .Where(i => i.ImageID == imageId)
            .Select(i => new { i.Content, i.ContentType })
            .FirstOrDefaultAsync();

        if (row?.Content == null || row.Content.Length == 0)
            return null;

        var ct = string.IsNullOrWhiteSpace(row.ContentType) ? "application/octet-stream" : row.ContentType!;
        return (row.Content, ct);
    }

    public async Task<bool> DeleteAsync(int listingId, int imageId)
    {
        var image = await _db.ListingImages
            .FirstOrDefaultAsync(i => i.ImageID == imageId && i.ListingID == listingId);

        if (image == null) return false;

        _db.ListingImages.Remove(image);
        await _db.SaveChangesAsync();
        return true;
    }
}
