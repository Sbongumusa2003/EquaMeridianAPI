
public static class AppTime
{
    public const string TimeZoneIdWindows = "South Africa Standard Time";
    public const string TimeZoneIdIana = "Africa/Johannesburg";
    public const string DisplayLabel = "SAST";

    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static TimeZoneInfo SouthAfrica => Zone;
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime ToSouthAfrica(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(value, Zone);
        if (value.Kind == DateTimeKind.Local)
            return TimeZoneInfo.ConvertTime(value, Zone);
        return value;
    }

    public static string Format(DateTime value, string format = "dd MMM yyyy, HH:mm")
        => ToSouthAfrica(value).ToString(format, System.Globalization.CultureInfo.InvariantCulture) + " " + DisplayLabel;

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? TimeZoneIdWindows : TimeZoneIdIana);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? TimeZoneIdIana : TimeZoneIdWindows);
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "SAST", TimeSpan.FromHours(2), "South Africa Standard Time", "South Africa Standard Time");
            }
        }
    }
}
