using Microsoft.AspNetCore.Http;

public interface IListingImageRepository
{
    Task<IEnumerable<string>> GetUrlsByListingAsync(int listingId);
    Task<int> CountAsync(int listingId);
    Task<IEnumerable<string>> AddImagesAsync(int listingId, IEnumerable<IFormFile> files);
    Task<bool> DeleteAsync(int listingId, int imageId);
    Task<(byte[] Content, string ContentType)?> GetContentAsync(int imageId);
}
