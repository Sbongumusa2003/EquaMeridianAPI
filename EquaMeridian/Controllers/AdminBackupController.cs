using EquaMeridian.DTOs.DatabaseBackup;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/admin/backup")]
[Authorize(Policy = "AdminOnly")]
public class AdminBackupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;
    private readonly ILogger<AdminBackupController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public AdminBackupController(
        AppDbContext db,
        IConfiguration config,
        IAuditService audit,
        ILogger<AdminBackupController> logger)
    {
        _db = db;
        _config = config;
        _audit = audit;
        _logger = logger;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ── List ──────────────────────────────────────────────────────────────

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

    // ── Create ────────────────────────────────────────────────────────────

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
                message = "Cannot create backup directory. Set Backup:Directory (or Backup__Directory) to a writable path.",
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
            _logger.LogError(ex, "Backup failed writing {Path}", jsonPath);
            return StatusCode(500, new
            {
                message = "Backup failed while writing the logical snapshot.",
                detail = ex.Message,
                path = jsonPath
            });
        }
    }

    // ── Download ──────────────────────────────────────────────────────────

    [HttpGet("{fileName}")]
    public IActionResult Download(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid backup file name." });

        var fullPath = Path.Combine(GetBackupDirectory(), safeName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Backup file not found." });

        var bytes = System.IO.File.ReadAllBytes(fullPath);
        return File(bytes, "application/json", safeName);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> Delete(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(GetBackupDirectory(), safeName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Backup file not found." });

        System.IO.File.Delete(fullPath);
        await _audit.LogAsync(AdminId, "DATABASE_BACKUP_DELETED",
            $"Backup deleted: {safeName}", AdminId, null, null, Ip);
        return Ok(new { message = $"Deleted {safeName}." });
    }

    // ── Restore (local JSON logical restore) ──────────────────────────────

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreBackupRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!dto.ConfirmOverride)
            return BadRequest(new
            {
                message = "Set confirmOverride to true to proceed. This overwrites operational data in the live database."
            });

        var backupDir = GetBackupDirectory();
        var fullPath = Path.Combine(backupDir, Path.GetFileName(dto.FileName));

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Backup file not found." });

        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json logical snapshots can be restored by this endpoint." });

        string json;
        try
        {
            json = await System.IO.File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Could not read backup file.", detail = ex.Message });
        }

        LogicalSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<LogicalSnapshot>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Backup file is not valid JSON.", detail = ex.Message });
        }

        if (snapshot == null)
            return BadRequest(new { message = "Backup file is empty or invalid." });

        // Version 1 backups only had a subset of fields — still restorable for those tables.
        try
        {
            await RestoreSnapshotAsync(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed for {File}", dto.FileName);
            return StatusCode(500, new
            {
                message = "Restore failed. The database may be in a partial state — create a fresh backup after fixing the error, or re-seed.",
                detail = ex.Message
            });
        }

        var restoredFileName = Path.GetFileName(dto.FileName);
        await _audit.LogAsync(AdminId, "DATABASE_RESTORED",
            $"Database restored from logical backup: {restoredFileName}",
            AdminId, null, null, Ip);

        return Ok(new
        {
            message = $"Database restored from {restoredFileName}.",
            note = "Users may need to sign in again. Password hashes and roles were restored from the snapshot."
        });
    }

    // ── Snapshot build ────────────────────────────────────────────────────

    private async Task<string> BuildLogicalSnapshotAsync(string databaseName)
    {
        // Strip navigation collections so JSON stays acyclic and restoreable.
        var users = await _db.Users.AsNoTracking().ToListAsync();
        foreach (var u in users)
        {
            u.Listings = new List<Listing>();
            u.AuditLogs = new List<AuditLog>();
            u.UserServiceAreas = new List<UserServiceArea>();
        }

        var listings = await _db.Listings.AsNoTracking().ToListAsync();
        foreach (var l in listings)
        {
            // Clear navigations if present
        }

        var snapshot = new LogicalSnapshot
        {
            Version = 2,
            ExportedAtUtc = AppTime.Now,
            Database = databaseName,
            Users = users,
            Categories = await _db.Categories.AsNoTracking().ToListAsync(),
            DocumentTypes = await _db.DocumentTypes.AsNoTracking().ToListAsync(),
            Provinces = await _db.Provinces.AsNoTracking().ToListAsync(),
            Cities = await _db.Cities.AsNoTracking().ToListAsync(),
            Suburbs = await _db.Suburbs.AsNoTracking().ToListAsync(),
            Roles = await _db.Roles.AsNoTracking().ToListAsync(),
            Permissions = await _db.Permissions.AsNoTracking().ToListAsync(),
            RolePermissions = await _db.RolePermissions.AsNoTracking().ToListAsync(),
            ServiceAreas = await _db.ServiceAreas.AsNoTracking().ToListAsync(),
            UserServiceAreas = await _db.UserServiceAreas.AsNoTracking().ToListAsync(),
            Listings = listings,
            ListingImages = await _db.ListingImages.AsNoTracking().ToListAsync(),
            Documents = await _db.Documents.AsNoTracking().ToListAsync(),
            FeeConfigurations = await _db.FeeConfigurations.AsNoTracking().ToListAsync(),
            DiscountTiers = await _db.DiscountTiers.AsNoTracking().ToListAsync(),
            Bookings = await _db.Bookings.AsNoTracking().ToListAsync(),
            Quotations = await _db.Quotations.AsNoTracking().ToListAsync(),
            Invoices = await _db.Invoices.AsNoTracking().ToListAsync(),
            Reviews = await _db.Reviews.AsNoTracking().ToListAsync(),
            Notifications = await _db.Notifications.AsNoTracking().ToListAsync(),
            Disputes = await _db.Disputes.AsNoTracking().ToListAsync(),
            Refunds = await _db.Refunds.AsNoTracking().ToListAsync(),
            Payouts = await _db.Payouts.AsNoTracking().ToListAsync(),
            Campaigns = await _db.Campaigns.AsNoTracking().ToListAsync(),
            BlockedTerms = await _db.BlockedTerms.AsNoTracking().ToListAsync(),
            TimerConfigurations = await _db.TimerConfigurations.AsNoTracking().ToListAsync(),
            CartItems = await _db.CartItems.AsNoTracking().ToListAsync(),
            WishlistItems = await _db.WishlistItems.AsNoTracking().ToListAsync()
        };

        return JsonSerializer.Serialize(snapshot, JsonOpts);
    }

    // ── Snapshot restore ──────────────────────────────────────────────────

    private async Task RestoreSnapshotAsync(LogicalSnapshot snapshot)
    {
        // Disable retry strategy for a multi-statement transaction.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Wipe operational tables (children first is handled by CASCADE).
            // Reference / catalogue tables are also cleared so restore is consistent.
            await TruncateAllAsync();

            // Insert in dependency order. Identity values are preserved via explicit IDs.
            await InsertRangeAsync(snapshot.Categories);
            await InsertRangeAsync(snapshot.DocumentTypes);
            await InsertRangeAsync(snapshot.Provinces);
            await InsertRangeAsync(snapshot.Roles);
            await InsertRangeAsync(snapshot.Permissions);
            await InsertRangeAsync(snapshot.ServiceAreas);
            await InsertRangeAsync(snapshot.Users);
            await InsertRangeAsync(snapshot.RolePermissions);
            await InsertRangeAsync(snapshot.Cities);
            await InsertRangeAsync(snapshot.Suburbs);
            await InsertRangeAsync(snapshot.UserServiceAreas);
            await InsertRangeAsync(snapshot.Listings);
            await InsertRangeAsync(snapshot.ListingImages);
            await InsertRangeAsync(snapshot.Documents);
            await InsertRangeAsync(snapshot.FeeConfigurations);
            await InsertRangeAsync(snapshot.DiscountTiers);
            await InsertRangeAsync(snapshot.Bookings);
            await InsertRangeAsync(snapshot.Quotations);
            await InsertRangeAsync(snapshot.Invoices);
            await InsertRangeAsync(snapshot.Reviews);
            await InsertRangeAsync(snapshot.Notifications);
            await InsertRangeAsync(snapshot.Disputes);
            await InsertRangeAsync(snapshot.Refunds);
            await InsertRangeAsync(snapshot.Payouts);
            await InsertRangeAsync(snapshot.Campaigns);
            await InsertRangeAsync(snapshot.BlockedTerms);
            await InsertRangeAsync(snapshot.TimerConfigurations);
            await InsertRangeAsync(snapshot.CartItems);
            await InsertRangeAsync(snapshot.WishlistItems);

            // Keep sequences in sync with restored max IDs (PostgreSQL).
            await ResetSequencesAsync();

            await tx.CommitAsync();
        });
    }

    private async Task TruncateAllAsync()
    {
        // Single CASCADE truncate keeps FK order simple on Postgres.
        // Quote identifiers to match EF's default PascalCase table names.
        const string sql = """
            TRUNCATE TABLE
                "WishlistItems",
                "CartItems",
                "BookingConditionInspections",
                "BookingStatusHistories",
                "DepositDeductions",
                "ReturnRequests",
                "JobDocuments",
                "LeaseAgreements",
                "Deliveries",
                "OtpCodes",
                "PasswordReset",
                "Messages",
                "Threads",
                "Notifications",
                "Reviews",
                "Refunds",
                "Disputes",
                "Payouts",
                "Invoices",
                "Quotations",
                "Inspections",
                "Bookings",
                "ListingImages",
                "Listings",
                "Documents",
                "Campaigns",
                "DiscountTiers",
                "FeeConfigurations",
                "BlockedTerms",
                "TimerConfigurations",
                "UserServiceAreas",
                "RolePermissions",
                "AuditLogs",
                "Users",
                "Suburbs",
                "Cities",
                "Provinces",
                "ServiceAreas",
                "Roles",
                "Permissions",
                "DocumentTypes",
                "Categories"
            RESTART IDENTITY CASCADE;
            """;

        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            // Some tables may not exist in older schemas — fall back to per-table truncate.
            _logger.LogWarning(ex, "Bulk TRUNCATE failed; falling back to per-table truncate.");
            foreach (var table in new[]
            {
                "WishlistItems", "CartItems", "Notifications", "Reviews", "Refunds", "Disputes",
                "Payouts", "Invoices", "Quotations", "Bookings", "ListingImages", "Listings",
                "Documents", "Campaigns", "DiscountTiers", "FeeConfigurations", "BlockedTerms",
                "TimerConfigurations", "UserServiceAreas", "RolePermissions", "AuditLogs",
                "Users", "Suburbs", "Cities", "Provinces", "ServiceAreas", "Roles", "Permissions",
                "DocumentTypes", "Categories", "OtpCodes", "PasswordReset", "Messages", "Threads",
                "Inspections", "Deliveries", "LeaseAgreements", "JobDocuments", "ReturnRequests",
                "DepositDeductions", "BookingConditionInspections", "BookingStatusHistories"
            })
            {
                try
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        $"TRUNCATE TABLE \"{table}\" RESTART IDENTITY CASCADE;");
                }
                catch
                {
                    /* table may not exist */
                }
            }
        }
    }

    private async Task InsertRangeAsync<T>(List<T>? items) where T : class
    {
        if (items == null || items.Count == 0) return;

        // Detach navigation references so EF inserts the scalar FKs only.
        foreach (var item in items)
        {
            foreach (var nav in _db.Entry(item).Navigations)
            {
                nav.CurrentValue = null;
            }
        }

        await _db.Set<T>().AddRangeAsync(items);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task ResetSequencesAsync()
    {
        // Best-effort sequence realignment for Postgres identity columns.
        const string sql = """
            DO $$
            DECLARE r RECORD;
            BEGIN
              FOR r IN
                SELECT c.relname AS table_name, a.attname AS column_name
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = c.oid
                JOIN pg_attrdef d ON d.adrelid = c.oid AND d.adnum = a.attnum
                WHERE c.relkind = 'r'
                  AND n.nspname = 'public'
                  AND pg_get_expr(d.adbin, d.adrelid) LIKE 'nextval%'
                  AND a.attnum > 0
                  AND NOT a.attisdropped
              LOOP
                EXECUTE format(
                  'SELECT setval(pg_get_serial_sequence(%L, %L), COALESCE((SELECT MAX(%I) FROM %I), 1))',
                  r.table_name, r.column_name, r.column_name, r.table_name
                );
              END LOOP;
            END $$;
            """;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reset PostgreSQL sequences after restore.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string GetDatabaseName()
    {
        var cs = _config.GetConnectionString("DefaultConnection") ?? "";
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

    private string GetBackupDirectory()
    {
        var configured = _config["Backup:Directory"];

        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
            configured.StartsWith("./") ||
            configured.StartsWith(".\\"))
        {
            var dir = Path.Combine(Path.GetTempPath(), "EquaMeridianBackups");
            try { Directory.CreateDirectory(dir); } catch { /* listed later */ }
            return dir;
        }

        if (Path.IsPathRooted(configured))
            return configured;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    /// <summary>Versioned logical snapshot used for local backup/restore.</summary>
    private sealed class LogicalSnapshot
    {
        public int Version { get; set; } = 2;
        public DateTime ExportedAtUtc { get; set; }
        public string Database { get; set; } = "";

        public List<User>? Users { get; set; }
        public List<Category>? Categories { get; set; }
        public List<DocumentType>? DocumentTypes { get; set; }
        public List<Province>? Provinces { get; set; }
        public List<City>? Cities { get; set; }
        public List<Suburb>? Suburbs { get; set; }
        public List<Role>? Roles { get; set; }
        public List<Permission>? Permissions { get; set; }
        public List<RolePermission>? RolePermissions { get; set; }
        public List<ServiceArea>? ServiceAreas { get; set; }
        public List<UserServiceArea>? UserServiceAreas { get; set; }
        public List<Listing>? Listings { get; set; }
        public List<ListingImage>? ListingImages { get; set; }
        public List<Document>? Documents { get; set; }
        public List<FeeConfiguration>? FeeConfigurations { get; set; }
        public List<DiscountTier>? DiscountTiers { get; set; }
        public List<Booking>? Bookings { get; set; }
        public List<Quotation>? Quotations { get; set; }
        public List<Invoice>? Invoices { get; set; }
        public List<Review>? Reviews { get; set; }
        public List<Notification>? Notifications { get; set; }
        public List<Dispute>? Disputes { get; set; }
        public List<Refund>? Refunds { get; set; }
        public List<Payout>? Payouts { get; set; }
        public List<Campaign>? Campaigns { get; set; }
        public List<BlockedTerm>? BlockedTerms { get; set; }
        public List<TimerConfiguration>? TimerConfigurations { get; set; }
        public List<CartItem>? CartItems { get; set; }
        public List<WishlistItem>? WishlistItems { get; set; }
    }
}
