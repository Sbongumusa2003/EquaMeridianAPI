using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingWishlistRateOptionsAndOtpPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: The following columns already existed physically on the database
            // before this migration was generated (confirmed via sys.columns), so their
            // AddColumn calls have been removed to avoid "Column names in each table
            // must be unique.":
            //   OtpCodes.Purpose
            //   Listings.DeliveryAvailable
            //   Listings.DeliveryFeeZAR
            //   Listings.DryHireAvailable
            //   Listings.PickupAvailable
            //   Listings.WetDailyRateZAR
            //   Listings.WetHireAvailable
            //   Listings.WetWeeklyRateZAR

            migrationBuilder.CreateTable(
                name: "BookingStatusHistories",
                columns: table => new
                {
                    BookingStatusHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedByUserID = table.Column<int>(type: "int", nullable: false),
                    ChangedByRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatusHistories", x => x.BookingStatusHistoryID);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistories_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistories_Users_ChangedByUserID",
                        column: x => x.ChangedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                columns: table => new
                {
                    WishlistItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistItems", x => x.WishlistItemID);
                    table.ForeignKey(
                        name: "FK_WishlistItems_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishlistItems_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_BookingID_CreatedDate",
                table: "BookingStatusHistories",
                columns: new[] { "BookingID", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_ChangedByUserID",
                table: "BookingStatusHistories",
                column: "ChangedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ContractorID_ListingID",
                table: "WishlistItems",
                columns: new[] { "ContractorID", "ListingID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ListingID",
                table: "WishlistItems",
                column: "ListingID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingStatusHistories");

            migrationBuilder.DropTable(
                name: "WishlistItems");

            // NOTE: No DropColumn calls here for Purpose/DeliveryAvailable/etc. — this
            // migration never added them (see Up()), so Down() must not remove them.
        }
    }
}