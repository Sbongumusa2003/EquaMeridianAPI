using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Migration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "Listings",
                type: "decimal(3,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedByUserID",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedDate",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConditionOnReturn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingID);
                    table.ForeignKey(
                        name: "FK_Bookings_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    CampaignID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BannerImageURL = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DiscountCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FeaturedListingID = table.Column<int>(type: "int", nullable: true),
                    CreatedByAdminID = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByAdminID = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.CampaignID);
                    table.ForeignKey(
                        name: "FK_Campaigns_Listings_FeaturedListingID",
                        column: x => x.FeaturedListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Campaigns_Users_CreatedByAdminID",
                        column: x => x.CreatedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeConfigurations",
                columns: table => new
                {
                    FeeConfigurationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MinFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VATInclusive = table.Column<bool>(type: "bit", nullable: false),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeConfigurations", x => x.FeeConfigurationID);
                    table.ForeignKey(
                        name: "FK_FeeConfigurations_Users_UpdatedByAdminID",
                        column: x => x.UpdatedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    InspectionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    RequestedByAdminID = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OutcomeConfirmedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inspections", x => x.InspectionID);
                    table.ForeignKey(
                        name: "FK_Inspections_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inspections_Users_RequestedByAdminID",
                        column: x => x.RequestedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.DeliveryID);
                    table.ForeignKey(
                        name: "FK_Deliveries_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepositDeductions",
                columns: table => new
                {
                    DepositDeductionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    DamageDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedRepairCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PhotoEvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositDeductions", x => x.DepositDeductionID);
                    table.ForeignKey(
                        name: "FK_DepositDeductions_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disputes",
                columns: table => new
                {
                    DisputeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ListingID = table.Column<int>(type: "int", nullable: true),
                    BookingID = table.Column<int>(type: "int", nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BookingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RaisedByUserID = table.Column<int>(type: "int", nullable: false),
                    RespondentID = table.Column<int>(type: "int", nullable: true),
                    ReasonCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComplaintDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DesiredResolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RaisedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolutionType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedByAdminID = table.Column<int>(type: "int", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disputes", x => x.DisputeID);
                    table.ForeignKey(
                        name: "FK_Disputes_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_RaisedByUserID",
                        column: x => x.RaisedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_ResolvedByAdminID",
                        column: x => x.ResolvedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_RespondentID",
                        column: x => x.RespondentID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    QuotationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreferredContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DailyRateZAR = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WeeklyRateZAR = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeliveryFee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuoteValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotesToCustomer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookingID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.QuotationID);
                    table.ForeignKey(
                        name: "FK_Quotations_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotations_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotations_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotations_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    ReturnRequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreferredPickupDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeWindow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EarlyReturnFeeApplies = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.ReturnRequestID);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    MachineryID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    OverallRating = table.Column<int>(type: "int", nullable: false),
                    AspectRatingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewID);
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Listings_MachineryID",
                        column: x => x.MachineryID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisputeID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestedByUserID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentGatewayReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.RefundID);
                    table.ForeignKey(
                        name: "FK_Refunds_Disputes_DisputeID",
                        column: x => x.DisputeID,
                        principalTable: "Disputes",
                        principalColumn: "DisputeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformFeePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierPayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedByAdminID = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceID);
                    table.ForeignKey(
                        name: "FK_Invoices_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Quotations_QuotationID",
                        column: x => x.QuotationID,
                        principalTable: "Quotations",
                        principalColumn: "QuotationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_GeneratedByAdminID",
                        column: x => x.GeneratedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaseAgreements",
                columns: table => new
                {
                    LeaseAgreementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgreementNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QuotationID = table.Column<int>(type: "int", nullable: false),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    ListingID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    ContractorID = table.Column<int>(type: "int", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StandardTerms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecialConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationPolicy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiabilityAndInsuranceTerms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DamageAndMaintenanceProvisions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierSigned = table.Column<bool>(type: "bit", nullable: false),
                    SupplierSignatureName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierDigitalSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierSignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractorSigned = table.Column<bool>(type: "bit", nullable: false),
                    ContractorSignatureName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractorDigitalSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractorSignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseAgreements", x => x.LeaseAgreementID);
                    table.ForeignKey(
                        name: "FK_LeaseAgreements_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaseAgreements_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaseAgreements_Quotations_QuotationID",
                        column: x => x.QuotationID,
                        principalTable: "Quotations",
                        principalColumn: "QuotationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaseAgreements_Users_ContractorID",
                        column: x => x.ContractorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaseAgreements_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobDocuments",
                columns: table => new
                {
                    JobDocumentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaseAgreementID = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserID = table.Column<int>(type: "int", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDocuments", x => x.JobDocumentID);
                    table.ForeignKey(
                        name: "FK_JobDocuments_LeaseAgreements_LeaseAgreementID",
                        column: x => x.LeaseAgreementID,
                        principalTable: "LeaseAgreements",
                        principalColumn: "LeaseAgreementID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobDocuments_Users_UploadedByUserID",
                        column: x => x.UploadedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerifiedByUserID",
                table: "Documents",
                column: "VerifiedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ContractorID",
                table: "Bookings",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ListingID",
                table: "Bookings",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SupplierID",
                table: "Bookings",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CreatedByAdminID",
                table: "Campaigns",
                column: "CreatedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_DiscountCode",
                table: "Campaigns",
                column: "DiscountCode");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_FeaturedListingID",
                table: "Campaigns",
                column: "FeaturedListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_BookingID",
                table: "Deliveries",
                column: "BookingID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepositDeductions_BookingID",
                table: "DepositDeductions",
                column: "BookingID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_BookingID",
                table: "Disputes",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ContractorID",
                table: "Disputes",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ListingID",
                table: "Disputes",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RaisedByUserID",
                table: "Disputes",
                column: "RaisedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedByAdminID",
                table: "Disputes",
                column: "ResolvedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RespondentID",
                table: "Disputes",
                column: "RespondentID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_SupplierID",
                table: "Disputes",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_FeeConfigurations_UpdatedByAdminID",
                table: "FeeConfigurations",
                column: "UpdatedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_ListingID",
                table: "Inspections",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_RequestedByAdminID",
                table: "Inspections",
                column: "RequestedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractorID",
                table: "Invoices",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_GeneratedByAdminID",
                table: "Invoices",
                column: "GeneratedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ListingID",
                table: "Invoices",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_QuotationID",
                table: "Invoices",
                column: "QuotationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierID",
                table: "Invoices",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_JobDocuments_LeaseAgreementID",
                table: "JobDocuments",
                column: "LeaseAgreementID");

            migrationBuilder.CreateIndex(
                name: "IX_JobDocuments_UploadedByUserID",
                table: "JobDocuments",
                column: "UploadedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_AgreementNumber",
                table: "LeaseAgreements",
                column: "AgreementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_BookingID",
                table: "LeaseAgreements",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_ContractorID",
                table: "LeaseAgreements",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_ListingID",
                table: "LeaseAgreements",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_QuotationID",
                table: "LeaseAgreements",
                column: "QuotationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseAgreements_SupplierID",
                table: "LeaseAgreements",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_BookingID",
                table: "Quotations",
                column: "BookingID",
                unique: true,
                filter: "[BookingID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ContractorID",
                table: "Quotations",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_ListingID",
                table: "Quotations",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_SupplierID",
                table: "Quotations",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_DisputeID",
                table: "Refunds",
                column: "DisputeID");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RequestedByUserID",
                table: "Refunds",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_BookingID",
                table: "ReturnRequests",
                column: "BookingID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_ContractorID",
                table: "ReturnRequests",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingID",
                table: "Reviews",
                column: "BookingID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ContractorID",
                table: "Reviews",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MachineryID_Status",
                table: "Reviews",
                columns: new[] { "MachineryID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_SupplierID",
                table: "Reviews",
                column: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_VerifiedByUserID",
                table: "Documents",
                column: "VerifiedByUserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_VerifiedByUserID",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DepositDeductions");

            migrationBuilder.DropTable(
                name: "FeeConfigurations");

            migrationBuilder.DropTable(
                name: "Inspections");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "JobDocuments");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ReturnRequests");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "LeaseAgreements");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Documents_VerifiedByUserID",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserID",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VerifiedDate",
                table: "Documents");
        }
    }
}
