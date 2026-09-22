using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Categories
{
    public class CategoryDto
    {
        public int CategoryID { get; set; }
        public string Name { get; set; } = string.Empty;
        // How many listings currently reference this category — surfaced so the admin UI can
        // explain up front why a given category can or can't be deleted.
        public int ListingCount { get; set; }
    }

    public class UpsertCategoryDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
