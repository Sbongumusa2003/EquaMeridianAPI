using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingMasterLeaseAgreementAndBackfillMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- New columns for this change (per-listing Master Lease Agreement acceptance) ---
            migrationBuilder.AddColumn<bool>(
                name: "MasterLeaseAgreementAccepted",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MasterLeaseAgreementAcceptedDate",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            // --- Backfill: the columns below were added to the C# model and to
            // AppDbContextModelSnapshot.cs back in 20260802084635_AddTrackingWishlistRateOptionsAndOtpPurpose,
            // but that migration's AddColumn calls for them were manually stripped out at the time because
            // they'd been added to the database out-of-band already existed on the database being used then.
            // Because the model snapshot still lists them as present, EF's diff-based tooling (Add-Migration)
            // will never regenerate AddColumn calls for them on a fresh/rebuilt database — so this uses raw,
            // guarded SQL instead, safe to run on both a database that already has them (e.g. a teammate who
            // never had their DB reset) and one that doesn't (e.g. after a DataSeeder-triggered reset).
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Listings', 'DryHireAvailable') IS NULL
    ALTER TABLE dbo.Listings ADD DryHireAvailable BIT NOT NULL CONSTRAINT DF_Listings_DryHireAvailable DEFAULT (1);

IF COL_LENGTH('dbo.Listings', 'WetHireAvailable') IS NULL
    ALTER TABLE dbo.Listings ADD WetHireAvailable BIT NOT NULL CONSTRAINT DF_Listings_WetHireAvailable DEFAULT (0);

IF COL_LENGTH('dbo.Listings', 'WetDailyRateZAR') IS NULL
    ALTER TABLE dbo.Listings ADD WetDailyRateZAR DECIMAL(18,2) NULL;

IF COL_LENGTH('dbo.Listings', 'WetWeeklyRateZAR') IS NULL
    ALTER TABLE dbo.Listings ADD WetWeeklyRateZAR DECIMAL(18,2) NULL;

IF COL_LENGTH('dbo.Listings', 'PickupAvailable') IS NULL
    ALTER TABLE dbo.Listings ADD PickupAvailable BIT NOT NULL CONSTRAINT DF_Listings_PickupAvailable DEFAULT (1);

IF COL_LENGTH('dbo.Listings', 'DeliveryAvailable') IS NULL
    ALTER TABLE dbo.Listings ADD DeliveryAvailable BIT NOT NULL CONSTRAINT DF_Listings_DeliveryAvailable DEFAULT (0);

IF COL_LENGTH('dbo.Listings', 'DeliveryFeeZAR') IS NULL
    ALTER TABLE dbo.Listings ADD DeliveryFeeZAR DECIMAL(18,2) NULL;

IF COL_LENGTH('dbo.OtpCodes', 'Purpose') IS NULL
    ALTER TABLE dbo.OtpCodes ADD Purpose NVARCHAR(MAX) NOT NULL CONSTRAINT DF_OtpCodes_Purpose DEFAULT ('Login');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MasterLeaseAgreementAccepted",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "MasterLeaseAgreementAcceptedDate",
                table: "Listings");

            // No DropColumn calls for the backfilled columns — same reasoning as the migration this
            // backfills: they may be relied on by data that predates this migration, and Down() isn't
            // part of this project's normal workflow.
        }
    }
}
