using System.Text;
public static class IcsWriter
{
    public static byte[] GenerateBookingEvent(
        int bookingId, string title, DateTime start, DateTime end, string location, string description)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//EquaMeridian//Booking Calendar//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        sb.Append("BEGIN:VEVENT\r\n");
        sb.Append($"UID:equameridian-booking-{bookingId}@equameridian.co.za\r\n");
        sb.Append($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\n");
        sb.Append($"DTSTART:{start:yyyyMMddTHHmmssZ}\r\n");
        sb.Append($"DTEND:{end:yyyyMMddTHHmmssZ}\r\n");
        sb.Append($"SUMMARY:{Escape(title)}\r\n");
        sb.Append($"LOCATION:{Escape(location)}\r\n");
        sb.Append($"DESCRIPTION:{Escape(description)}\r\n");
        sb.Append("STATUS:CONFIRMED\r\n");
        sb.Append("END:VEVENT\r\n");
        sb.Append("END:VCALENDAR\r\n");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
