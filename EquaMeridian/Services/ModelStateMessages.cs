using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Builds the single friendly message returned for a 400 caused by model binding / validation.
/// Validation attributes already carry friendly text; the technical messages ASP.NET produces when a value has the
/// wrong data type ("The JSON value could not be converted to System.Decimal. Path: $.dailyRate | LineNumber...")
/// are rewritten to "Please enter a valid value for daily rate."
/// </summary>
public static class ModelStateMessages
{
    private const string Fallback = "Some of the information you entered is not valid. Please check it and try again.";

    private static readonly Regex Technical = new(
        @"could not be converted|is not valid for|is invalid|The input was not valid|Path: \$|LineNumber|BytePositionInLine|Unexpected character|is an invalid start of a value|expected a|non-empty request body|Error converting value|Could not convert",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Build(ModelStateDictionary modelState)
    {
        var messages = new List<string>();
        foreach (var entry in modelState)
        {
            foreach (var error in entry.Value?.Errors ?? new ModelErrorCollection())
            {
                var msg = Friendly(entry.Key, error);
                if (!string.IsNullOrWhiteSpace(msg) && !messages.Contains(msg)) messages.Add(msg);
            }
        }
        return messages.Count > 0 ? string.Join(" ", messages) : Fallback;
    }

    private static string Friendly(string key, ModelError error)
    {
        var raw = string.IsNullOrWhiteSpace(error.ErrorMessage) ? error.Exception?.Message : error.ErrorMessage;
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var field = Humanize(key);

        var missing = Regex.Match(raw, @"A value for the '(?<n>[^']+)' parameter or property was not provided");
        if (missing.Success)
        {
            var name = Humanize(missing.Groups["n"].Value);
            return name.Length == 0
                ? "Please fill in the required information and try again."
                : $"Please provide {name}.";
        }

        if (error.Exception != null || Technical.IsMatch(raw))
        {
            if (raw.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase) || field.Length == 0)
                return "Please fill in the required information and try again.";
            return $"Please enter a valid value for {field}.";
        }

        return raw;
    }

    /// <summary>"$.dailyRateZAR" / "dto.DailyRateZAR" / "Listings[0].Year" -> "daily rate" / "daily rate" / "year".</summary>
    internal static string Humanize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var k = Regex.Replace(key, @"\[\d+\]", "");
        k = k.Substring(k.LastIndexOf('.') + 1).Trim('$', ' ');
        if (k.Equals("dto", StringComparison.OrdinalIgnoreCase) || k.Length == 0) return string.Empty;
        k = Regex.Replace(k, @"ZAR$", "");
        var spaced = Regex.Replace(k, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ").Trim();
        return spaced.ToLowerInvariant();
    }
}
