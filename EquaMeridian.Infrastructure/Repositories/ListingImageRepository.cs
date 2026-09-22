using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class ListingImageRepository : IListingImageRepository
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const int MaxImagesPerListing = 5;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ListingImageRepository(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IEnumerable<string>> GetUrlsByListingAsync(int listingId)
        => await _db.ListingImages
            .Where(i => i.ListingID == listingId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.FilePath)
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

        var uploadPath = Path.Combine(
            _env.ContentRootPath, "uploads", "listings", listingId.ToString());

        Directory.CreateDirectory(uploadPath);

        foreach (var file in files)
        {
            if (existingCount + saved.Count >= MaxImagesPerListing)
                break;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                continue;

            if (file.Length > 10 * 1024 * 1024)
                continue;

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadPath, fileName);
            var relativeUrl = $"/uploads/listings/{listingId}/{fileName}";

            using (var stream = File.Create(fullPath))
                await file.CopyToAsync(stream);

            _db.ListingImages.Add(new ListingImage
            {
                ListingID = listingId,
                FilePath = relativeUrl,
                DisplayOrder = order++,
                UploadedDate = AppTime.Now
            });

            saved.Add(relativeUrl);
        }

        if (saved.Count > 0)
            await _db.SaveChangesAsync();

        return saved;
    }

    public async Task<bool> DeleteAsync(int listingId, int imageId)
    {
        var image = await _db.ListingImages
            .FirstOrDefaultAsync(i => i.ImageID == imageId && i.ListingID == listingId);

        if (image == null) return false;
        var relativePath = image.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_env.ContentRootPath, relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        _db.ListingImages.Remove(image);
        await _db.SaveChangesAsync();
        return true;
    }
}