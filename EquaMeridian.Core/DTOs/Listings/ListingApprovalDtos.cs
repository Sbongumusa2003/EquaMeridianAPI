using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Listings
{
    public class RejectListingDto
    {
        [Required(ErrorMessage = "A rejection reason is required.")]
        [MinLength(5, ErrorMessage = "Please provide a more specific rejection reason.")]
        public string Reason { get; set; } = string.Empty;
    }

    public class RequestListingChangesDto
    {
        [Required(ErrorMessage = "Comments describing the requested changes are required.")]
        [MinLength(5, ErrorMessage = "Please provide more specific comments for the Supplier.")]
        public string Comments { get; set; } = string.Empty;
    }
}
