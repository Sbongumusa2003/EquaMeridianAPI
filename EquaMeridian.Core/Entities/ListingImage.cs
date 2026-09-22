public class ListingImage
{
    public int ImageID { get; set; }
    public int ListingID { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public DateTime UploadedDate { get; set; } = AppTime.Now;
    public Listing Listing { get; set; } = null!;
}