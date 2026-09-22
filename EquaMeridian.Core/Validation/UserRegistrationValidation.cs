using System.Linq;
using System.Text.RegularExpressions;

namespace EquaMeridian.Core.Validation
{
    /// <summary>
    /// Shared rules for new-user registration: names must not contain digits,
    /// and phone numbers must be valid South African formats (0XXXXXXXXX or +27XXXXXXXXX).
    /// Mirrors the frontend validators and is used by AuthService for uniqueness checks.
    /// </summary>
    public static class UserRegistrationValidation
    {
        /// <summary>
        /// SA mobile/landline in local or international form, digits only after optional +27 or leading 0.
        /// Examples: 0821234567, +27821234567
        /// </summary>
        public const string SaPhonePattern = @"^(\+27[0-9]{9}|0[0-9]{9})$";

        /// <summary>FullName / person name: must not contain any digit.</summary>
        public const string NameNoDigitsPattern = @"^[a-zA-Z][a-zA-Z\s\-']*$";

        public const string SaPhoneErrorMessage =
            "Phone number must be a valid South African number with exactly 10 digits (0XXXXXXXXX or +27XXXXXXXXX).";

        public const string NameNoDigitsErrorMessage =
            "Name must contain only letters, spaces, hyphens or apostrophes.";

        private static readonly Regex SaPhoneRegex = new(
            SaPhonePattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Strips common separators and normalises local 0XXXXXXXXX to +27XXXXXXXXX
        /// so uniqueness checks treat both forms as the same number.
        /// Returns null if input is null/whitespace or does not match a valid SA format.
        /// </summary>
        public static string? NormalizeSaPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // Remove spaces, hyphens, parentheses commonly typed by users
            var cleaned = Regex.Replace(phone.Trim(), @"[\s\-\(\)]", "");

            if (!SaPhoneRegex.IsMatch(cleaned))
                return null;

            if (cleaned.StartsWith("0", StringComparison.Ordinal))
                return "+27" + cleaned[1..];

            return cleaned; // already +27XXXXXXXXX
        }

        public static bool IsValidSaPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;
            var cleaned = Regex.Replace(phone.Trim(), @"[\s\-\(\)]", "");
            return SaPhoneRegex.IsMatch(cleaned);
        }

        public static bool IsValidPersonName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return System.Text.RegularExpressions.Regex.IsMatch(name.Trim(), NameNoDigitsPattern);
        }

        public static bool NameContainsDigits(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return name.Any(char.IsDigit);
        }
    }
}
