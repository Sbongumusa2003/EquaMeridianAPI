using EquaMeridian.DTOs.DatabaseBackup;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/admin/backup")]
[Authorize(Policy = "AdminOnly")]
public class AdminBackupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public AdminBackupController(AppDbContext db, IConfiguration config, IAuditService audit)
    {
        _db = db;
        _config = config;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public IActionResult ListBackups()
    {
        var backupDir = GetBackupDirectory();
        if (!Directory.Exists(backupDir))
            return Ok(new List<BackupFileDto>());

        var files = Directory.GetFiles(backupDir, "*.json")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupFileDto
            {
                FileName = f.Name,
                SizeBytes = f.Length,
                CreatedAtUtc = f.CreationTimeUtc
            })
            .ToList();

        return Ok(files);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBackup()
    {
        var backupDir = GetBackupDirectory();
        try
        {
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Cannot create backup directory. Set Backup:Directory in appsettings to a writable path.",
                detail = ex.Message,
                path = backupDir
            });
        }

        var dbName = GetDatabaseName();
        var stamp = AppTime.Now.ToString("yyyyMMdd-HHmmss");
        var jsonName = $"{dbName}-{stamp}.json";
        var jsonPath = Path.Combine(backupDir, jsonName);

        try
        {
            var snapshot = await BuildLogicalSnapshotAsync(dbName);
            await System.IO.File.WriteAllTextAsync(jsonPath, snapshot);

            var info = new FileInfo(jsonPath);
            await _audit.LogAsync(AdminId, "DATABASE_BACKUP_CREATED",
                $"Logical JSON backup created: {jsonName}",
                AdminId, null, null, Ip);

            return Ok(new BackupFileDto
            {
                FileName = jsonName,
                SizeBytes = info.Length,
                CreatedAtUtc = info.CreationTimeUtc
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Backup failed while writing the logical snapshot.",
                detail = ex.Message,
                path = jsonPath
            });
        }
    }

    [HttpPost("restore")]
    public IActionResult Restore([FromBody] RestoreBackupRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!dto.ConfirmOverride)
            return BadRequest(new
            {
                message = "Set confirmOverride to true to proceed. Automatic restore of JSON snapshots is not supported in the cloud deployment."
            });

        var backupDir = GetBackupDirectory();
        var fullPath = Path.Combine(backupDir, Path.GetFileName(dto.FileName));

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Backup file not found." });

        // JSON logical snapshots are intentionally not auto-restored.
        // Full DB restore on managed Postgres (Render) should be done via the
        // provider's backup/restore tools or by importing the JSON manually.
        return BadRequest(new
        {
            message =
                "Automatic restore of JSON logical snapshots is not supported on the cloud Postgres deployment. " +
                "Download the .json file and restore the data manually, or use Render's database backup/restore feature for a full point-in-time recovery."
        });
    }

    private async Task<string> BuildLogicalSnapshotAsync(string databaseName)
    {
        var payload = new
        {
            exportedAtUtc = AppTime.Now,
            database = databaseName,
            users = await _db.Users.AsNoTracking().Select(u => new
            {
                u.UserID,
                u.FullName,
                u.Email,
                u.Role,
                u.CompanyName,
                u.AccountStatus,
                u.CreatedDate
            }).ToListAsync(),
            listings = await _db.Listings.AsNoTracking().Select(l => new
            {
                l.ListingID,
                l.ListingTitle,
                l.CategoryID,
                l.SupplierID,
                l.AvailabilityStatus,
                l.MakeBrand,
                l.Model,
                l.Year,
                l.Location,
                l.DailyRateZAR,
                l.AverageRating,
                l.CreatedDate
            }).ToListAsync(),
            bookings = await _db.Bookings.AsNoTracking().Select(b => new
            {
                b.BookingID,
                b.ListingID,
                b.SupplierID,
                b.ContractorID,
                b.RentalStartDate,
                b.RentalEndDate,
                b.Status,
                b.CreatedDate
            }).ToListAsync(),
            invoices = await _db.Invoices.AsNoTracking().Select(i => new
            {
                i.InvoiceID,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.TotalAmount,
                i.PlatformFeeAmount,
                i.SupplierPayableAmount,
                i.PaymentStatus
            }).ToListAsync()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetDatabaseName()
    {
        var cs = _config.GetConnectionString("DefaultConnection") ?? "";
        // Npgsql format: Host=...;Database=equameridian;...
        foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 &&
                (kv[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                 kv[0].Trim().Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)))
            {
                return kv[1].Trim();
            }
        }
        return "equameridian";
    }

    /// <summary>
    /// On cloud (Render) the filesystem is ephemeral unless a persistent disk is mounted.
    /// Prefer an absolute path under /tmp or a path you configure via Backup:Directory.
    /// </summary>
    private string GetBackupDirectory()
    {
        var configured = _config["Backup:Directory"];

        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
            configured.StartsWith("./") ||
            configured.StartsWith(".\\"))
        {
            // Linux / cloud friendly default
            var dir = Path.Combine(Path.GetTempPath(), "EquaMeridianBackups");
            try { Directory.CreateDirectory(dir); } catch { /* listed later */ }
            return dir;
        }

        if (Path.IsPathRooted(configured))
            return configured;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }
}