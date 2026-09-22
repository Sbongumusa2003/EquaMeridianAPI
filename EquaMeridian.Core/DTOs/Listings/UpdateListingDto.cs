using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EquaMeridian.DTOs.Listings
{
    public class UpdateListingDto : IValidatableObject
    {
        private const int MinYear = 1950;

        [Required]
        [MaxLength(200)]
        public string ListingTitle { get; set; } = string.Empty;

        [Required]
        public int CategoryID { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? MakeBrand { get; set; }

        [MaxLength(100)]
        public string? Model { get; set; }

        public int? Year { get; set; }

        [MaxLength(50)]
        public string? OperatingWeight { get; set; }

        [MaxLength(50)]
        public string? EnginePower { get; set; }

        [MaxLength(200)]
        public string? Location { get; set; }

        [Required]
        [Range(0.01, 10_000_000, ErrorMessage = "Daily rate must be greater than zero and at most 10,000,000.")]
        public decimal DailyRateZAR { get; set; }

        [Range(0.01, 10_000_000, ErrorMessage = "Weekly rate must be greater than zero and at most 10,000,000.")]
        public decimal? WeeklyRateZAR { get; set; }

        public bool DryHireAvailable { get; set; } = true;
        public bool WetHireAvailable { get; set; } = false;

        [Range(0.01, 10_000_000, ErrorMessage = "Wet hire daily rate must be greater than zero and at most 10,000,000.")]
        public decimal? WetDailyRateZAR { get; set; }

        [Range(0.01, 10_000_000, ErrorMessage = "Wet hire weekly rate must be greater than zero and at most 10,000,000.")]
        public decimal? WetWeeklyRateZAR { get; set; }

        public bool PickupAvailable { get; set; } = true;
        public bool DeliveryAvailable { get; set; } = false;

        [Range(0, 10_000_000, ErrorMessage = "Delivery fee must be between 0 and 10,000,000.")]
        public decimal? DeliveryFeeZAR { get; set; }

        [Required]
        [RegularExpression("^(Fixed|QuoteRequired)$", ErrorMessage = "Pricing mode must be 'Fixed' or 'QuoteRequired'.")]
        public string PricingMode { get; set; } = "Fixed";

        [Required]
        [Range(1, 10_000, ErrorMessage = "Units owned must be between 1 and 10,000.")]
        public int UnitsOwned { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var maxYear = DateTime.UtcNow.Year + 1;

            if (Year.HasValue && (Year.Value < MinYear || Year.Value > maxYear))
            {
                yield return new ValidationResult(
                    $"Year must be a valid manufacture year between {MinYear} and {maxYear}.",
                    new[] { nameof(Year) });
            }


            if (!string.IsNullOrWhiteSpace(ListingTitle))
            {
                var t = ListingTitle.Trim();
                if (Regex.IsMatch(t, @"[*$^~`|<>{}\[\]\\]") || !Regex.IsMatch(t, @"[a-zA-Z0-9]"))
                {
                    yield return new ValidationResult(
                        "Listing title contains invalid characters.",
                        new[] { nameof(ListingTitle) });
                }
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                var d = Description;
                if (Regex.IsMatch(d, @"[*$^~`|<>{}\[\]\\]") || !Regex.IsMatch(d, @"[a-zA-Z0-9]"))
                {
                    yield return new ValidationResult(
                        "Description contains invalid characters.",
                        new[] { nameof(Description) });
                }
            }

            if (!string.IsNullOrWhiteSpace(Location))
            {
                var loc = Location.Trim();
                if (!Regex.IsMatch(loc, @"^[a-zA-Z0-9][a-zA-Z0-9 .,\-/'()]*$"))
                {
                    yield return new ValidationResult(
                        "Location contains invalid characters.",
                        new[] { nameof(Location) });
                }
            }

            if (!string.IsNullOrWhiteSpace(MakeBrand))
            {
                var mb = MakeBrand.Trim();
                if (Regex.IsMatch(mb, @"^-\d+(\.\d+)?$") || !Regex.IsMatch(mb, @"^[a-zA-Z0-9][a-zA-Z0-9 .\-/]*$"))
                {
                    yield return new ValidationResult(
                        "Make / brand contains invalid characters.",
                        new[] { nameof(MakeBrand) });
                }
            }

            if (!string.IsNullOrWhiteSpace(Model))
            {
                var md = Model.Trim();
                if (Regex.IsMatch(md, @"^-\d+(\.\d+)?$") || !Regex.IsMatch(md, @"^[a-zA-Z0-9][a-zA-Z0-9 .\-/]*$"))
                {
                    yield return new ValidationResult(
                        "Model contains invalid characters.",
                        new[] { nameof(Model) });
                }
            }

            // Positive measurement: 30000, 30,000 kg, 122 kW, 12.5t
            const string measurementPattern = @"^\d{1,3}([ ,]?\d{3})*([.,]\d+)?(\s*[a-zA-Z]+)?$";

            if (!string.IsNullOrWhiteSpace(OperatingWeight))
            {
                var w = OperatingWeight.Trim();
                if (w.StartsWith("-", StringComparison.Ordinal) || !Regex.IsMatch(w, measurementPattern))
                {
                    yield return new ValidationResult(
                        "Enter a valid operating weight (e.g. 30000 kg).",
                        new[] { nameof(OperatingWeight) });
                }
            }

            if (!string.IsNullOrWhiteSpace(EnginePower))
            {
                var p = EnginePower.Trim();
                if (p.StartsWith("-", StringComparison.Ordinal) || !Regex.IsMatch(p, measurementPattern))
                {
                    yield return new ValidationResult(
                        "Enter a valid engine power (e.g. 122 kW).",
                        new[] { nameof(EnginePower) });
                }
            }

            if (!DryHireAvailable && !WetHireAvailable)
            {
                yield return new ValidationResult(
                    "At least one of dry hire or wet hire must be available.",
                    new[] { nameof(DryHireAvailable), nameof(WetHireAvailable) });
            }

            if (WetHireAvailable && WetDailyRateZAR is null or <= 0)
            {
                yield return new ValidationResult(
                    "A wet hire daily rate is required when wet hire is available.",
                    new[] { nameof(WetDailyRateZAR) });
            }

            if (!PickupAvailable && !DeliveryAvailable)
            {
                yield return new ValidationResult(
                    "At least one of pickup or delivery must be available.",
                    new[] { nameof(PickupAvailable), nameof(DeliveryAvailable) });
            }

            if (DeliveryAvailable && DeliveryFeeZAR is null or < 0)
            {
                yield return new ValidationResult(
                    "A delivery (establishment) fee is required when delivery is available.",
                    new[] { nameof(DeliveryFeeZAR) });
            }
        }
    }

    public class UpdateListingStatusDto
    {
        [Required]
        public string NewStatus { get; set; } = string.Empty;
        public string? SuspensionReason { get; set; }
    }
}
