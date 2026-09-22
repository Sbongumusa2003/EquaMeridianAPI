using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquaMeridian.Infrastructure.Data;
public static class DatabaseObjectsInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.LeaseAgreements', 'PaymentMethod') IS NOT NULL
BEGIN
    UPDATE dbo.LeaseAgreements
    SET PaymentMethod = N'PayFast'
    WHERE PaymentMethod = N'EFT' AND TotalAmount <= 50000;
END
");
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Invoices', 'PaymentMethod') IS NOT NULL
BEGIN
    UPDATE dbo.Invoices
    SET PaymentMethod = N'PayFast'
    WHERE (PaymentMethod IS NULL OR PaymentMethod = N'EFT') AND TotalAmount <= 50000
      AND (EftProofPath IS NULL OR EftProofPath = N'');
    UPDATE dbo.Invoices
    SET PaymentMethod = N'EFT'
    WHERE TotalAmount > 50000 AND (PaymentMethod IS NULL OR PaymentMethod = N'');
END
");

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Users', 'BankName') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD
        BankName NVARCHAR(120) NULL,
        BankAccountName NVARCHAR(200) NULL,
        BankAccountNumber NVARCHAR(40) NULL,
        BankBranchCode NVARCHAR(20) NULL,
        BankAccountType NVARCHAR(40) NULL;
END
");
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Invoices', 'EftProofPath') IS NULL
BEGIN
    ALTER TABLE dbo.Invoices ADD
        PaymentMethod NVARCHAR(20) NULL,
        EftProofPath NVARCHAR(400) NULL,
        EftProofOriginalName NVARCHAR(260) NULL;
END
");

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.TimerConfigurations', 'SessionIdleMinutes') IS NULL
BEGIN
    ALTER TABLE dbo.TimerConfigurations
    ADD SessionIdleMinutes INT NOT NULL
        CONSTRAINT DF_TimerConfigurations_SessionIdleMinutes DEFAULT 30;
END
");

        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.CartItems', 'ReservedUntil') IS NULL
BEGIN
    ALTER TABLE dbo.CartItems ADD ReservedUntil DATETIME2 NOT NULL
        CONSTRAINT DF_CartItems_ReservedUntil DEFAULT (DATEADD(MINUTE, 30, SYSUTCDATETIME()));
END
");


        await db.Database.ExecuteSqlRawAsync(@"
CREATE OR ALTER PROCEDURE dbo.sp_ExpireStaleQuotations
    @QuoteExpiryHours INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Quotations
    SET Status = 'Expired'
    WHERE Status IN ('Requested', 'Submitted')
      AND (
            (QuoteValidUntil IS NOT NULL AND QuoteValidUntil <= GETUTCDATE())
            OR (QuoteValidUntil IS NULL AND DATEADD(HOUR, @QuoteExpiryHours, RequestedDate) <= GETUTCDATE())
          );

    SELECT @@ROWCOUNT AS ExpiredCount;
END
");

        await db.Database.ExecuteSqlRawAsync(@"
CREATE OR ALTER TRIGGER dbo.trg_Users_RoleChange_Audit
ON dbo.Users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(Role)
    BEGIN
        INSERT INTO dbo.AuditLogs (UserID, AdminID, ListingID, TransactionType, Description, PreviousValues, NewValues, Timestamp, IPAddress)
        SELECT
            i.UserID,
            NULL,
            NULL,
            'ROLE_CHANGED_DB_TRIGGER',
            'Role changed at the database level (bypassing the application audit call).',
            d.Role,
            i.Role,
            GETUTCDATE(),
            NULL
        FROM inserted i
        INNER JOIN deleted d ON d.UserID = i.UserID
        WHERE d.Role <> i.Role;
    END
END
");
    }
    public static async Task<int> RunExpireStaleQuotationsAsync(AppDbContext db, int quoteExpiryHours)
    {
        var hoursParam = new Microsoft.Data.SqlClient.SqlParameter("@QuoteExpiryHours", quoteExpiryHours);
        var result = await db.Database
            .SqlQueryRaw<int>("EXEC dbo.sp_ExpireStaleQuotations @QuoteExpiryHours", hoursParam)
            .ToListAsync();

        return result.FirstOrDefault();
    }
}
