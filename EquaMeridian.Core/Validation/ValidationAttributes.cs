using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EquaMeridian.Validation
{
    /// <summary>Turns a property name such as "ScheduledDate" into "Scheduled date" for user-facing messages.</summary>
    internal static class FieldNames
    {
        public static string Friendly(string? memberName, string? displayName = null)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && displayName != memberName) return displayName!;
            if (string.IsNullOrWhiteSpace(memberName)) return "This field";
            var spaced = Regex.Replace(memberName, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            spaced = Regex.Replace(spaced, @"\s?ZAR$", "").Trim(); // "Daily rate ZAR" -> "Daily rate"
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1).ToLowerInvariant();
        }
    }

    /// <summary>The date must be today or later (South African time). Null is allowed - combine with [Required].</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class NotInPastAttribute : ValidationAttribute
    {
        public NotInPastAttribute() : base("{0} cannot be in the past.") { }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime d && d.Date < AppTime.Now.Date)
                return new ValidationResult(
                    string.Format(CultureInfo.InvariantCulture, ErrorMessageString,
                        FieldNames.Friendly(validationContext.MemberName, validationContext.DisplayName)),
                    new[] { validationContext.MemberName ?? string.Empty });
            return ValidationResult.Success;
        }
    }

    /// <summary>The date must be a real date (not the "0001-01-01" a missing value binds to) between 2000 and 2100.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ValidDateAttribute : ValidationAttribute
    {
        public ValidDateAttribute() : base("Please enter a valid {0}.") { }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime d && (d.Year < 2000 || d.Year > 2100))
                return new ValidationResult(
                    string.Format(CultureInfo.InvariantCulture, ErrorMessageString,
                        FieldNames.Friendly(validationContext.MemberName, validationContext.DisplayName).ToLowerInvariant()),
                    new[] { validationContext.MemberName ?? string.Empty });
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// The value must not be lower than another property of the same object (e.g. an end date must not be
    /// before the start date, or MaxDays must not be below MinDays). Works for any IComparable type.
    /// Set ErrorMessage to override the default text.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class NotBeforeAttribute : ValidationAttribute
    {
        public string OtherProperty { get; }

        public NotBeforeAttribute(string otherProperty) : base("{0} cannot be before {1}.")
        {
            OtherProperty = otherProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null) return ValidationResult.Success;

            var other = validationContext.ObjectType.GetProperty(OtherProperty)?.GetValue(validationContext.ObjectInstance);
            if (other == null) return ValidationResult.Success;

            try
            {
                if (value is IComparable comparable && comparable.CompareTo(other) < 0)
                    return new ValidationResult(
                        string.Format(CultureInfo.InvariantCulture, ErrorMessageString,
                            FieldNames.Friendly(validationContext.MemberName, validationContext.DisplayName),
                            FieldNames.Friendly(OtherProperty).ToLowerInvariant()),
                        new[] { validationContext.MemberName ?? string.Empty });
            }
            catch (ArgumentException)
            {
                // Different types - nothing sensible to compare.
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>A manufacture year: from <see cref="MinYear"/> up to next year (South African time). Null is allowed.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class YearRangeAttribute : ValidationAttribute
    {
        public int MinYear { get; }

        public YearRangeAttribute(int minYear = 1950) : base("Year must be a valid year between {0} and {1}.")
        {
            MinYear = minYear;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is int year)
            {
                var max = AppTime.Now.Year + 1;
                if (year < MinYear || year > max)
                    return new ValidationResult(
                        string.Format(CultureInfo.InvariantCulture, ErrorMessageString, MinYear, max),
                        new[] { validationContext.MemberName ?? string.Empty });
            }
            return ValidationResult.Success;
        }
    }
}
