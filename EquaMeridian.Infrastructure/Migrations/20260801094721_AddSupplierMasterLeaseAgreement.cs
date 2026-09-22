using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierMasterLeaseAgreement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MasterLeaseAgreementSignatureName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MasterLeaseAgreementSigned",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MasterLeaseAgreementSignedDate",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DamageWaiverFee",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryDistanceKm",
                table: "Quotations",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Quotations",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceExclVat",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceInclVat",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RentalSubtotal",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDeposit",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Quotations",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminReviewNotes",
                table: "Listings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastReviewedByAdminID",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedDate",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingMode",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed");

            migrationBuilder.AddColumn<int>(
                name: "UnitsAvailable",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UnitsOwned",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UnitsReserved",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitsUnderMaintenance",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryBaseFee",
                table: "FeeConfigurations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFreeRadiusKm",
                table: "FeeConfigurations",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryRatePerKm",
                table: "FeeConfigurations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HandoverInspectionID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OffHireDateTime",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnInspectionID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingConditionInspections",
                columns: table => new
                {
                    BookingConditionInspectionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    InspectionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompletedByUserID = table.Column<int>(type: "int", nullable: false),
                    ChecklistData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DamageDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedRepairCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SupplierAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    ContractorAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingConditionInspections", x => x.BookingConditionInspectionID);
                    table.ForeignKey(
                        name: "FK_BookingConditionInspections_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingConditionInspections_Users_CompletedByUserID",
                        column: x => x.CompletedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    CartItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.CartItemID);
                    table.ForeignKey(
                        name: "FK_CartItems_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartItems_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountTiers",
                columns: table => new
                {
                    DiscountTierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryID = table.Column<int>(type: "int", nullable: true),
                    MinDays = table.Column<int>(type: "int", nullable: false),
                    MaxDays = table.Column<int>(type: "int", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountTiers", x => x.DiscountTierID);
                    table.ForeignKey(
                        name: "FK_DiscountTiers_Categories_CategoryID",
                        column: x => x.CategoryID,
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscountTiers_Users_UpdatedByAdminID",
                        column: x => x.UpdatedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PricingMode",
                table: "Listings",
                column: "PricingMode");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_HandoverInspectionID",
                table: "Bookings",
                column: "HandoverInspectionID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ReturnInspectionID",
                table: "Bookings",
                column: "ReturnInspectionID");

            migrationBuilder.CreateIndex(
                name: "IX_BookingConditionInspections_BookingID_InspectionType",
                table: "BookingConditionInspections",
                columns: new[] { "BookingID", "InspectionType" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingConditionInspections_CompletedByUserID",
                table: "BookingConditionInspections",
                column: "CompletedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ContractorID",
                table: "CartItems",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ListingID",
                table: "CartItems",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountTiers_CategoryID",
                table: "DiscountTiers",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountTiers_UpdatedByAdminID",
                table: "DiscountTiers",
                column: "UpdatedByAdminID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BookingConditionInspections_HandoverInspectionID",
                table: "Bookings",
                column: "HandoverInspectionID",
                principalTable: "BookingConditionInspections",
                principalColumn: "BookingConditionInspectionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BookingConditionInspections_ReturnInspectionID",
                table: "Bookings",
                column: "ReturnInspectionID",
                principalTable: "BookingConditionInspections",
                principalColumn: "BookingConditionInspectionID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BookingConditionInspections_HandoverInspectionID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BookingConditionInspections_ReturnInspectionID",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "BookingConditionInspections");

            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "DiscountTiers");

            migrationBuilder.DropIndex(
                name: "IX_Listings_PricingMode",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_HandoverInspectionID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ReturnInspectionID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MasterLeaseAgreementSignatureName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MasterLeaseAgreementSigned",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MasterLeaseAgreementSignedDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DamageWaiverFee",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DeliveryDistanceKm",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "PriceExclVat",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "PriceInclVat",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "RentalSubtotal",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "SecurityDeposit",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AdminReviewNotes",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LastReviewedByAdminID",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LastReviewedDate",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PricingMode",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnitsAvailable",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnitsOwned",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnitsReserved",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "UnitsUnderMaintenance",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DeliveryBaseFee",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "DeliveryFreeRadiusKm",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "DeliveryRatePerKm",
                table: "FeeConfigurations");

            migrationBuilder.DropColumn(
                name: "HandoverInspectionID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OffHireDateTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ReturnInspectionID",
                table: "Bookings");
        }
    }
}
