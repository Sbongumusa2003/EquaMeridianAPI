using EquaMeridian.DTOs.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Policy = "AdminOnly")]
public class AdminAuditLogController : ControllerBase
{
    private readonly IAuditLogRepository _repo;
    public AdminAuditLogController(IAuditLogRepository repo) => _repo = repo;

    private string GeneratedBy
    {
        get
        {
            var name = User.FindFirstValue(ClaimTypes.Name)
                       ?? User.FindFirstValue("name")
                       ?? User.FindFirstValue(ClaimTypes.Email);
            return string.IsNullOrWhiteSpace(name) ? "EquaMeridian Admin Portal" : name;
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? eventType,
        [FromQuery] int? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var (logs, total) = await _repo.SearchAsync(search, eventType, userId, from, to, page, pageSize);
        return Ok(new { logs, totalCount = total, page, pageSize });
    }

    [HttpGet("event-types")]
    public async Task<IActionResult> GetEventTypes() => Ok(await _repo.GetDistinctEventTypesAsync());

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() => Ok(await _repo.GetDistinctUsersAsync());

    [HttpGet("{auditId}")]
    public async Task<IActionResult> GetById(int auditId)
    {
        var log = await _repo.GetByIdAsync(auditId);
        return log == null ? NotFound() : Ok(log);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? search, [FromQuery] string? eventType, [FromQuery] int? userId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        List<AuditLogListItemDto> rows;
        try
        {
            rows = await _repo.GetForExportAsync(search, eventType, userId, from, to);
        }
        catch
        {
            return StatusCode(500, new { message = "Unable to generate export. Please try again later." });
        }

        var csv = BuildCsv(rows);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"audit-log-{AppTime.Now:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] string? search, [FromQuery] string? eventType, [FromQuery] int? userId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        List<AuditLogListItemDto> rows;
        try
        {
            rows = await _repo.GetForExportAsync(search, eventType, userId, from, to);
        }
        catch
        {
            return StatusCode(500, new { message = "Unable to generate export. Please try again later." });
        }

        var subtitle = BuildSubtitle(search, eventType, userId, from, to, rows.Count);
        var doc = new PdfReportEngine("Audit Log", subtitle, GeneratedBy);

        // KPI summary strip
        var success = rows.Count(r => string.Equals(r.Status, "Success", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(r.Status, "Info", StringComparison.OrdinalIgnoreCase));
        var warnings = rows.Count(r => string.Equals(r.Status, "Warning", StringComparison.OrdinalIgnoreCase));
        var failures = rows.Count(r => string.Equals(r.Status, "Failure", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(r.Status, "Error", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(r.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        var distinctUsers = rows.Select(r => r.UserID).Where(id => id.HasValue).Distinct().Count();
        var distinctTypes = rows.Select(r => r.EventType).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Count();

        doc.AddKpis(
            new PdfKpi("Total Events", rows.Count.ToString("N0", CultureInfo.InvariantCulture)),
            new PdfKpi("Event Types", distinctTypes.ToString(CultureInfo.InvariantCulture)),
            new PdfKpi("Actors", distinctUsers.ToString(CultureInfo.InvariantCulture)),
            new PdfKpi("Issues", (warnings + failures).ToString(CultureInfo.InvariantCulture)));

        if (rows.Count > 0)
        {
            var topType = rows.GroupBy(r => r.EventType)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .First();
            var insight =
                $"Most frequent event: {topType.Type} ({topType.Count} occurrence(s)). " +
                (failures > 0
                    ? $"{failures} failure/error event(s) require review."
                    : "No failure/error status events in this export window.");
            doc.AddInsight("Audit snapshot", insight);

            // Ranking of event types (horizontal bars) — management-style view
            var typeRank = rows.GroupBy(r => r.EventType)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();
            if (typeRank.Count > 0)
            {
                doc.AddHorizontalBarChart(
                    "Top event types",
                    typeRank.Select(g => g.Key).ToList(),
                    typeRank.Select(g => (decimal)g.Count()).ToList(),
                    valuePrefix: "");
            }
        }
        else
        {
            doc.AddInsight("Audit snapshot", "No audit events match the current filters.");
        }

        // Structured table — control-friendly list of events
        var tableRows = new List<PdfTableRow>();
        foreach (var r in rows)
        {
            tableRows.Add(new PdfTableRow(new[]
            {
                r.AuditID.ToString(CultureInfo.InvariantCulture),
                r.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(r.EventTime) ? "" : r.EventTime,
                Truncate(r.EventType, 28),
                Truncate(r.UserName ?? "System", 18),
                Truncate(r.Status, 10),
                Truncate(r.Description, 48)
            }));
        }

        // Column widths must sum to content width (~528 with default margins)
        doc.AddTable(
            $"Event log ({rows.Count})",
            new[] { "ID", "Date", "Time", "Event", "User", "Status", "Description" },
            new[] { 36.0, 68.0, 48.0, 110.0, 90.0, 52.0, 124.0 },
            new[] { true, false, false, false, false, false, false },
            tableRows,
            emptyMessage: "No audit events match the current search or filter criteria.");

        doc.AddNote("Audit trail is append-only. Filters applied to this export are shown in the subtitle. " +
                    "Status values reflect the outcome recorded for each action.");

        var bytes = doc.Build();
        return File(bytes, "application/pdf", $"audit-log-{AppTime.Now:yyyyMMdd-HHmmss}.pdf");
    }

    private static string BuildSubtitle(
        string? search, string? eventType, int? userId,
        DateTime? from, DateTime? to, int count)
    {
        var parts = new List<string>();
        if (from.HasValue || to.HasValue)
        {
            var a = from?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "start";
            var b = to?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "latest";
            parts.Add($"{a} - {b}");
        }
        else
        {
            parts.Add("Full history - no date filter");
        }

        if (!string.IsNullOrWhiteSpace(eventType))
            parts.Add($"Event: {eventType}");
        if (userId.HasValue)
            parts.Add($"User ID: {userId.Value}");
        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"Search: \"{Truncate(search, 40)}\"");

        parts.Add($"{count} record(s)");
        return string.Join("  |  ", parts);
    }

    private static string BuildCsv(IEnumerable<AuditLogListItemDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Audit_Log_ID,Event_Type,Description,UserID,User_Name,User_Email,Date,Time,Status");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.AuditID.ToString(CultureInfo.InvariantCulture),
                CsvField(r.EventType),
                CsvField(r.Description),
                r.UserID?.ToString(CultureInfo.InvariantCulture) ?? "",
                CsvField(r.UserName),
                CsvField(r.UserEmail),
                r.EventDate.ToString("yyyy-MM-dd"),
                CsvField(r.EventTime),
                CsvField(r.Status)));
        }

        return sb.ToString();
    }

    private static string CsvField(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max] + "...";
    }
}
