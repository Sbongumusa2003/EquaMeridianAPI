using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <summary>
    /// Adds structured South African address columns (AddressStreet, AddressSuburb, AddressCity,
    /// AddressProvince, AddressPostalCode) to Listings, Quotations, Bookings, CartItems and
    /// ReturnRequests - mirroring the SaAddress value object in EquaMeridian.Core.Common and the
    /// Fluent configuration now added in AppDbContext.
    ///
    /// This formalizes sql/AddStructuredSaAddress.sql into the EF migration history. That script
    /// was written to be run once, by hand, directly against the database, so it was never recorded
    /// in __EFMigrationsHistory and the model snapshot was never updated for it - meaning EF's own
    /// view of these five tables didn't match the real schema.
    ///
    /// IMPORTANT: this migration is written with the same idempotent IF NOT EXISTS guards as the
    /// original script, so it is safe to apply whether or not you already ran that script by hand:
    /// - If you already ran the script on this database: applying this migration will find every
    ///   column already present, skip the ALTER TABLEs, and just record itself in
    ///   __EFMigrationsHistory (run `dotnet ef migrations add` on top of this and it will no longer
    ///   see this history file as unmatched).
    /// - If you have NOT run the script (e.g. a fresh database, a teammate's machine): applying this
    ///   migration adds the columns from scratch.
    /// </summary>
    public partial class AddStructuredSaAddressColumns : Migration
    {
        private static readonly string[] Tables = { "Listings", "Quotations", "Bookings", "CartItems", "ReturnRequests" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'AddressStreet')
                    BEGIN
                      ALTER TABLE dbo.{table} ADD
                        AddressStreet nvarchar(120) NULL,
                        AddressSuburb nvarchar(80) NULL,
                        AddressCity nvarchar(80) NULL,
                        AddressProvince nvarchar(40) NULL,
                        AddressPostalCode nvarchar(10) NULL;
                    END
                ");
            }

            // Best-effort backfill of AddressCity from legacy free-text Location / DeliveryAddress,
            // matching sql/AddStructuredSaAddress.sql exactly (Listings and Quotations only - that
            // script never backfilled Bookings, CartItems or ReturnRequests, so those keep their
            // pre-existing behavior of starting out NULL for rows created before this migration).
            migrationBuilder.Sql(@"
                UPDATE dbo.Listings
                SET AddressCity = LEFT(LTRIM(RTRIM(Location)), 80)
                WHERE AddressCity IS NULL AND Location IS NOT NULL AND Location NOT LIKE '%,%';
            ");

            migrationBuilder.Sql(@"
                UPDATE dbo.Quotations
                SET AddressCity = LEFT(LTRIM(RTRIM(
                  CASE WHEN DeliveryAddress LIKE 'Pickup:%' THEN LTRIM(SUBSTRING(DeliveryAddress, 8, 300)) ELSE DeliveryAddress END
                )), 80)
                WHERE AddressCity IS NULL AND DeliveryAddress IS NOT NULL AND DeliveryAddress NOT LIKE '%,%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'AddressStreet')
                    BEGIN
                      ALTER TABLE dbo.{table} DROP COLUMN
                        AddressStreet, AddressSuburb, AddressCity, AddressProvince, AddressPostalCode;
                    END
                ");
            }
        }
    }
}
