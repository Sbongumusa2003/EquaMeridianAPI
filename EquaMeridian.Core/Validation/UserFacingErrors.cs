namespace EquaMeridian.Validation
{
    /// <summary>
    /// Decides which exception messages are safe and useful to show a user. Messages that our own code wrote
    /// on purpose ("This booking can no longer be cancelled.") are shown; messages thrown by the .NET runtime
    /// or Entity Framework ("Sequence contains no elements") are replaced with a friendly fallback.
    /// </summary>
    public static class UserFacingErrors
    {
        public static bool IsFrameworkThrown(Exception ex)
        {
            var ns = ex.TargetSite?.DeclaringType?.Namespace ?? string.Empty;
            return ns.StartsWith("System", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft", StringComparison.Ordinal);
        }

        /// <summary>The exception's own message when it is a deliberate business-rule message, otherwise the fallback.</summary>
        public static string MessageOrFallback(Exception ex, string fallback) =>
            ex is InvalidOperationException
            && !IsFrameworkThrown(ex)
            && !string.IsNullOrWhiteSpace(ex.Message)
                ? ex.Message
                : fallback;
    }
}
