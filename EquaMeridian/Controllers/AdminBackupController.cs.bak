using EquaMeridian.DTOs.DatabaseBackup;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        if (!Directory.Exists(backupDir)) return Ok(new List<BackupFileDto>());

        var files = Directory.GetFiles(backupDir, "*.bak")
            .Concat(Directory.GetFiles(backupDir, "*.json"))
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupFileDto { FileName = f.Name, SizeBytes = f.Length, CreatedAtUtc = f.CreationTimeUtc })
            .ToList();

        return Ok(files);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBackup()
    {
        var dbName = GetDatabaseName();
        var backupDir = GetBackupDirectory();
        Directory.CreateDirectory(backupDir);

        var stamp = AppTime.Now.ToString("yyyyMMdd-HHmmss");
        var bakName = $"{dbName}-{stamp}.bak";
        var bakPath = Path.GetFullPath(Path.Combine(backupDir, bakName));

        Exception? sqlError = null;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "BACKUP DATABASE [" + EscapeIdentifier(dbName) + "] TO DISK = {0} WITH INIT, COPY_ONLY, STATS = 10",
                bakPath);

            var info = new FileInfo(bakPath);
            await _audit.LogAsync(AdminId, "DATABASE_BACKUP_CREATED", $"Backup created: {bakName}",
                AdminId, null, null, Ip);
            return Ok(new BackupFileDto { FileName = bakName, SizeBytes = info.Length, CreatedAtUtc = info.CreationTimeUtc });
        }
        catch (Exception ex)
        {
            sqlError = ex;
            while (sqlError.InnerException != null) sqlError = sqlError.InnerException;
        }

        // Fallback: logical JSON snapshot written by the app process (does not need SQL service write access).
        // Restore still requires a .bak for full DB restore; this guarantees a downloadable backup for demos.
        try
        {
            var jsonName = $"{dbName}-{stamp}.json";
            var jsonPath = Path.GetFullPath(Path.Combine(backupDir, jsonName));
            var snapshot = await BuildLogicalSnapshotAsync();
            await System.IO.File.WriteAllTextAsync(jsonPath, snapshot);

            var info = new FileInfo(jsonPath);
            await _audit.LogAsync(AdminId, "DATABASE_BACKUP_CREATED",
                $"Logical JSON backup created: {jsonName} (SQL BACKUP failed: {sqlError?.Message})",
                AdminId, null, null, Ip);

            return Ok(new BackupFileDto
            {
                FileName = jsonName,
                SizeBytes = info.Length,
                CreatedAtUtc = info.CreationTimeUtc
            });
        }
        catch (Exception fallbackEx)
        {
            return StatusCode(500, new
            {
                message =
                    "Backup failed. SQL Server could not write a .bak file, and the application fallback also failed. " +
                    "Fix SQL backups by setting Backup:Directory in appsettings to an absolute folder the SQL Server " +
                    "service account can write (e.g. C:\\EquaMeridianBackups), and grant the SQL login db_backupoperator. " +
                    "Tried path: " + bakPath,
                path = bakPath,
                sqlDetail = sqlError?.Message,
                fallbackDetail = fallbackEx.Message
            });
        }
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreBackupRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!dto.ConfirmOverride)
            return BadRequest(new { message = "Set confirmOverride to true to proceed. This will overwrite the live database and disconnect other users." });

        var backupDir = GetBackupDirectory();
        var fullPath = Path.Combine(backupDir, Path.GetFileName(dto.FileName));
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Backup file not found." });

        if (fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "JSON logical snapshots cannot be restored into SQL Server automatically. " +
                          "Use a .bak file produced when SQL BACKUP succeeds, or re-import data manually."
            });
        }

        var dbName = GetDatabaseName();
        var masterConnectionString = BuildMasterConnectionString();

        try
        {
            using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();

            await ExecuteNonQueryAsync(connection,
                $"ALTER DATABASE [{EscapeIdentifier(dbName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", timeoutSeconds: 60);

            try
            {
                using var restoreCmd = new SqlCommand(
                    $"RESTORE DATABASE [{EscapeIdentifier(dbName)}] FROM DISK = @path WITH REPLACE", connection)
                {
                    CommandTimeout = 900
                };
                restoreCmd.Parameters.AddWithValue("@path", fullPath);
                await restoreCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await ExecuteNonQueryAsync(connection,
                    $"ALTER DATABASE [{EscapeIdentifier(dbName)}] SET MULTI_USER", timeoutSeconds: 60);
                SqlConnection.ClearAllPools();
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Restore failed. The database was returned to multi-user mode if that step " +
                           "succeeded; verify manually before assuming the app is usable again.",
                detail = ex.Message
            });
        }

        var restoredFileName = Path.GetFileName(dto.FileName);
        await _audit.LogAsync(AdminId, "DATABASE_RESTORED", $"Database restored from backup: {restoredFileName}",
            AdminId, null, null, Ip);

        return Ok(new { message = $"Database restored from {restoredFileName}." });
    }

    private async Task<string> BuildLogicalSnapshotAsync()
    {
        var payload = new
        {
            exportedAtUtc = AppTime.Now,
            database = GetDatabaseName(),
            users = await _db.Users.AsNoTracking().Select(u => new
            {
                u.UserID, u.FullName, u.Email, u.Role, u.CompanyName, u.AccountStatus, u.CreatedDate
            }).ToListAsync(),
            listings = await _db.Listings.AsNoTracking().Select(l => new
            {
                l.ListingID, l.ListingTitle, l.CategoryID, l.SupplierID, l.AvailabilityStatus,
                l.MakeBrand, l.Model, l.Year, l.Location, l.DailyRateZAR, l.AverageRating, l.CreatedDate
            }).ToListAsync(),
            bookings = await _db.Bookings.AsNoTracking().Select(b => new
            {
                b.BookingID, b.ListingID, b.SupplierID, b.ContractorID,
                b.RentalStartDate, b.RentalEndDate, b.Status, b.CreatedDate
            }).ToListAsync(),
            invoices = await _db.Invoices.AsNoTracking().Select(i => new
            {
                i.InvoiceID, i.InvoiceNumber, i.InvoiceDate, i.TotalAmount,
                i.PlatformFeeAmount, i.SupplierPayableAmount, i.PaymentStatus
            }).ToListAsync()
        };

        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql, int timeoutSeconds)
    {
        using var cmd = new SqlCommand(sql, connection) { CommandTimeout = timeoutSeconds };
        await cmd.ExecuteNonQueryAsync();
    }

    private string GetDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(_config.GetConnectionString("DefaultConnection"));
        return builder.InitialCatalog;
    }

    private string BuildMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_config.GetConnectionString("DefaultConnection"))
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Prefer an absolute path SQL Server can write. Default is C:\EquaMeridianBackups
    /// (not under bin\Debug) so the SQL service account is more likely to have access.
    /// </summary>
    private string GetBackupDirectory()
    {
        var configured = _config["Backup:Directory"];
        string dir;
        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
            configured.StartsWith("./", StringComparison.Ordinal) ||
            configured.StartsWith(".\\", StringComparison.Ordinal))
        {
            // Stable absolute default — create on first use
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EquaMeridian", "Backups");
            // On Windows CommonApplicationData is typically C:\ProgramData
            // Also offer a simple root fallback for local dev if ProgramData is locked down
            if (OperatingSystem.IsWindows())
            {
                var simple = @"C:\EquaMeridianBackups";
                try
                {
                    Directory.CreateDirectory(simple);
                    dir = simple;
                }
                catch
                {
                    // keep ProgramData path
                }
            }
        }
        else if (Path.IsPathRooted(configured))
        {
            dir = configured;
        }
        else
        {
            dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        try { Directory.CreateDirectory(dir); } catch { /* listed later if write fails */ }
        return dir;
    }

    private static string EscapeIdentifier(string identifier) => identifier.Replace("]", "]]");
}
