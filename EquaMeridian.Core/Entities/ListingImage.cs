public class ListingImage
{
    public int ImageID { get; set; }
    public int ListingID { get; set; }

    /// <summary>
    /// Public relative URL, e.g. /api/media/listing-images/{ImageID}.
    /// Kept for API compatibility; actual bytes live in Content so files survive Render redeploys.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Image binary stored in PostgreSQL (durable across Render restarts).</summary>
    public byte[]? Content { get; set; }

    /// <summary>MIME type, e.g. image/png, image/jpeg, image/webp.</summary>
    public string? ContentType { get; set; }

    public int DisplayOrder { get; set; } = 0;
    public DateTime UploadedDate { get; set; } = AppTime.Now;
    public Listing Listing { get; set; } = null!;
}
