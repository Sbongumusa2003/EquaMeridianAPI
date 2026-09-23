using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    DocTypeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeName = table.Column<string>(type: "text", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AppliesToRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.DocTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionID);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ProvinceID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ProvinceID);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAreas",
                columns: table => new
                {
                    ServiceAreaID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAreas", x => x.ServiceAreaID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    AccountStatus = table.Column<string>(type: "text", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanyName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BankName = table.Column<string>(type: "text", nullable: true),
                    BankAccountName = table.Column<string>(type: "text", nullable: true),
                    BankAccountNumber = table.Column<string>(type: "text", nullable: true),
                    BankBranchCode = table.Column<string>(type: "text", nullable: true),
                    BankAccountType = table.Column<string>(type: "text", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MasterLeaseAgreementSigned = table.Column<bool>(type: "boolean", nullable: false),
                    MasterLeaseAgreementSignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MasterLeaseAgreementSignatureName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProvinceID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityID);
                    table.ForeignKey(
                        name: "FK_Cities_Provinces_ProvinceID",
                        column: x => x.ProvinceID,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "integer", nullable: false),
                    PermissionID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleID, x.PermissionID });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionID",
                        column: x => x.PermissionID,
                        principalTable: "Permissions",
                        principalColumn: "PermissionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleID",
                        column: x => x.RoleID,
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    AdminID = table.Column<int>(type: "integer", nullable: true),
                    ListingID = table.Column<int>(type: "integer", nullable: true),
                    TransactionType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PreviousValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IPAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditID);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "BlockedTerms",
                columns: table => new
                {
                    BlockedTermID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Term = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AddedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedTerms", x => x.BlockedTermID);
                    table.ForeignKey(
                        name: "FK_BlockedTerms_Users_AddedByAdminID",
                        column: x => x.AddedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DiscountTiers",
                columns: table => new
                {
                    DiscountTierID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryID = table.Column<int>(type: "integer", nullable: true),
                    MinDays = table.Column<int>(type: "integer", nullable: false),
                    MaxDays = table.Column<int>(type: "integer", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocTypeID = table.Column<int>(type: "integer", nullable: false),
                    DocName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    VerificationStatus = table.Column<string>(type: "text", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedByUserID = table.Column<int>(type: "integer", nullable: true),
                    VerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocID);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTypes_DocTypeID",
                        column: x => x.DocTypeID,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocTypeID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Users_VerifiedByUserID",
                        column: x => x.VerifiedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeConfigurations",
                columns: table => new
                {
                    FeeConfigurationID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MinFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATInclusive = table.Column<bool>(type: "boolean", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DeliveryBaseFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeliveryFreeRadiusKm = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    DeliveryRatePerKm = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "Listings",
                columns: table => new
                {
                    ListingID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingTitle = table.Column<string>(type: "text", nullable: false),
                    CategoryID = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityStatus = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MakeBrand = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    OperatingWeight = table.Column<string>(type: "text", nullable: true),
                    EnginePower = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    DailyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WeeklyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CommissionRateSnapshot = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DryHireAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    WetHireAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    WetDailyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    WetWeeklyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PickupAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryFeeZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DuplicateFlag = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    AverageRating = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    PricingMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Fixed"),
                    UnitsOwned = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UnitsAvailable = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UnitsReserved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UnitsUnderMaintenance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastReviewedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    LastReviewedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MasterLeaseAgreementAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    MasterLeaseAgreementAcceptedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.ListingID);
                    table.ForeignKey(
                        name: "FK_Listings_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RelatedEntityID = table.Column<int>(type: "integer", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationID);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    OtpCodeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    ExpiryTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpCodes", x => x.OtpCodeID);
                    table.ForeignKey(
                        name: "FK_OtpCodes_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordReset",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiryTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordReset", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PasswordReset_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    ThreadID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParticipantOneID = table.Column<int>(type: "integer", nullable: false),
                    ParticipantTwoID = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => x.ThreadID);
                    table.ForeignKey(
                        name: "FK_Threads_Users_ParticipantOneID",
                        column: x => x.ParticipantOneID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Threads_Users_ParticipantTwoID",
                        column: x => x.ParticipantTwoID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimerConfigurations",
                columns: table => new
                {
                    TimerConfigurationID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    QuoteExpiryHours = table.Column<int>(type: "integer", nullable: false),
                    SessionIdleMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunExpiredCount = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimerConfigurations", x => x.TimerConfigurationID);
                    table.ForeignKey(
                        name: "FK_TimerConfigurations_Users_UpdatedByAdminID",
                        column: x => x.UpdatedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserServiceAreas",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    ServiceAreaID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServiceAreas", x => new { x.UserID, x.ServiceAreaID });
                    table.ForeignKey(
                        name: "FK_UserServiceAreas_ServiceAreas_ServiceAreaID",
                        column: x => x.ServiceAreaID,
                        principalTable: "ServiceAreas",
                        principalColumn: "ServiceAreaID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServiceAreas_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suburbs",
                columns: table => new
                {
                    SuburbID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CityID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suburbs", x => x.SuburbID);
                    table.ForeignKey(
                        name: "FK_Suburbs_Cities_CityID",
                        column: x => x.CityID,
                        principalTable: "Cities",
                        principalColumn: "CityID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    CampaignID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Audience = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    BannerImageURL = table.Column<string>(type: "text", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DiscountCode = table.Column<string>(type: "text", nullable: true),
                    FeaturedListingID = table.Column<int>(type: "integer", nullable: true),
                    CreatedByAdminID = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "CartItems",
                columns: table => new
                {
                    CartItemID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inspections",
                columns: table => new
                {
                    InspectionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    RequestedByAdminID = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OutcomeConfirmedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "ListingImages",
                columns: table => new
                {
                    ImageID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingImages", x => x.ImageID);
                    table.ForeignKey(
                        name: "FK_ListingImages_Listings_ListingID",
                        column: x => x.ListingID,
                        principalTable: "Listings",
                        principalColumn: "ListingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishlistItems",
                columns: table => new
                {
                    WishlistItemID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThreadID = table.Column<int>(type: "integer", nullable: false),
                    SenderID = table.Column<int>(type: "integer", nullable: false),
                    RecipientID = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    AttachmentURL = table.Column<string>(type: "text", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    DateSent = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageID);
                    table.ForeignKey(
                        name: "FK_Messages_Threads_ThreadID",
                        column: x => x.ThreadID,
                        principalTable: "Threads",
                        principalColumn: "ThreadID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Users_RecipientID",
                        column: x => x.RecipientID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Users_SenderID",
                        column: x => x.SenderID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingConditionInspections",
                columns: table => new
                {
                    BookingConditionInspectionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    InspectionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedByUserID = table.Column<int>(type: "integer", nullable: false),
                    ChecklistData = table.Column<string>(type: "text", nullable: false),
                    PhotoUrls = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DamageDescription = table.Column<string>(type: "text", nullable: true),
                    EstimatedRepairCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SupplierAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ContractorAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingConditionInspections", x => x.BookingConditionInspectionID);
                    table.ForeignKey(
                        name: "FK_BookingConditionInspections_Users_CompletedByUserID",
                        column: x => x.CompletedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConditionOnReturn = table.Column<string>(type: "text", nullable: true),
                    ReturnConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OffHireDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandoverInspectionID = table.Column<int>(type: "integer", nullable: true),
                    ReturnInspectionID = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserID = table.Column<int>(type: "integer", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancellationFeeApplies = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingID);
                    table.ForeignKey(
                        name: "FK_Bookings_BookingConditionInspections_HandoverInspectionID",
                        column: x => x.HandoverInspectionID,
                        principalTable: "BookingConditionInspections",
                        principalColumn: "BookingConditionInspectionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_BookingConditionInspections_ReturnInspectionID",
                        column: x => x.ReturnInspectionID,
                        principalTable: "BookingConditionInspections",
                        principalColumn: "BookingConditionInspectionID",
                        onDelete: ReferentialAction.Restrict);
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
                name: "BookingStatusHistories",
                columns: table => new
                {
                    BookingStatusHistoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedByUserID = table.Column<int>(type: "integer", nullable: false),
                    ChangedByRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    DepositDeductionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    DamageDescription = table.Column<string>(type: "text", nullable: false),
                    EstimatedRepairCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PhotoEvidenceUrls = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    DisputeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ListingID = table.Column<int>(type: "integer", nullable: true),
                    BookingID = table.Column<int>(type: "integer", nullable: true),
                    BookingReference = table.Column<string>(type: "text", nullable: false),
                    BookingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RaisedByUserID = table.Column<int>(type: "integer", nullable: false),
                    RespondentID = table.Column<int>(type: "integer", nullable: true),
                    ReasonCategory = table.Column<string>(type: "text", nullable: false),
                    ComplaintDescription = table.Column<string>(type: "text", nullable: false),
                    DesiredResolution = table.Column<string>(type: "text", nullable: true),
                    EvidenceUrls = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RaisedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolutionType = table.Column<string>(type: "text", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    ResolvedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    QuotationID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: false),
                    SpecialRequirements = table.Column<string>(type: "text", nullable: true),
                    PreferredContact = table.Column<string>(type: "text", nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DailyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    WeeklyRateZAR = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryDistanceKm = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    RentalSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PriceExclVat = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    PriceInclVat = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SecurityDeposit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DamageWaiverFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    QuoteValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotesToCustomer = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    BookingID = table.Column<int>(type: "integer", nullable: true)
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
                    ReturnRequestID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    PreferredPickupDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeWindow = table.Column<string>(type: "text", nullable: false),
                    PickupLocation = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    EarlyReturnFeeApplies = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
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
                    ReviewID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    MachineryID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    AspectRatingsJson = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ReviewText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "Invoices",
                columns: table => new
                {
                    InvoiceID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuotationID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PlatformFeePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    PlatformFeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierPayableAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    GeneratedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GatewayReference = table.Column<string>(type: "text", nullable: true),
                    LastSyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentMethod = table.Column<string>(type: "text", nullable: true),
                    EftProofPath = table.Column<string>(type: "text", nullable: true),
                    EftProofOriginalName = table.Column<string>(type: "text", nullable: true)
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
                    LeaseAgreementID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementNumber = table.Column<string>(type: "text", nullable: false),
                    QuotationID = table.Column<int>(type: "integer", nullable: false),
                    BookingID = table.Column<int>(type: "integer", nullable: false),
                    ListingID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    ContractorID = table.Column<int>(type: "integer", nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PaymentDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    StandardTerms = table.Column<string>(type: "text", nullable: false),
                    SpecialConditions = table.Column<string>(type: "text", nullable: true),
                    CancellationPolicy = table.Column<string>(type: "text", nullable: false),
                    LiabilityAndInsuranceTerms = table.Column<string>(type: "text", nullable: false),
                    DamageAndMaintenanceProvisions = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SupplierSigned = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierSignatureName = table.Column<string>(type: "text", nullable: true),
                    SupplierDigitalSignature = table.Column<string>(type: "text", nullable: true),
                    SupplierSignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractorSigned = table.Column<bool>(type: "boolean", nullable: false),
                    ContractorSignatureName = table.Column<string>(type: "text", nullable: true),
                    ContractorDigitalSignature = table.Column<string>(type: "text", nullable: true),
                    ContractorSignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserID = table.Column<int>(type: "integer", nullable: true)
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
                name: "Payouts",
                columns: table => new
                {
                    PayoutID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceID = table.Column<int>(type: "integer", nullable: false),
                    SupplierID = table.Column<int>(type: "integer", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PayoutAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SupplierNotes = table.Column<string>(type: "text", nullable: true),
                    AdministratorNotes = table.Column<string>(type: "text", nullable: true),
                    DeclineReason = table.Column<string>(type: "text", nullable: true),
                    ProcessedByAdminID = table.Column<int>(type: "integer", nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payouts", x => x.PayoutID);
                    table.ForeignKey(
                        name: "FK_Payouts_Invoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payouts_Users_ProcessedByAdminID",
                        column: x => x.ProcessedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payouts_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisputeID = table.Column<int>(type: "integer", nullable: true),
                    InvoiceID = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequestedByUserID = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    AdministratorNotes = table.Column<string>(type: "text", nullable: true),
                    PaymentGatewayReference = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                        name: "FK_Refunds_Invoices_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Refunds_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobDocuments",
                columns: table => new
                {
                    JobDocumentID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeaseAgreementID = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserID = table.Column<int>(type: "integer", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
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
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserID",
                table: "AuditLogs",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedTerms_AddedByAdminID",
                table: "BlockedTerms",
                column: "AddedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedTerms_Term",
                table: "BlockedTerms",
                column: "Term",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingConditionInspections_BookingID_InspectionType",
                table: "BookingConditionInspections",
                columns: new[] { "BookingID", "InspectionType" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingConditionInspections_CompletedByUserID",
                table: "BookingConditionInspections",
                column: "CompletedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ContractorID",
                table: "Bookings",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_HandoverInspectionID",
                table: "Bookings",
                column: "HandoverInspectionID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ListingID",
                table: "Bookings",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ReturnInspectionID",
                table: "Bookings",
                column: "ReturnInspectionID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SupplierID",
                table: "Bookings",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_BookingID_CreatedDate",
                table: "BookingStatusHistories",
                columns: new[] { "BookingID", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_ChangedByUserID",
                table: "BookingStatusHistories",
                column: "ChangedByUserID");

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
                name: "IX_CartItems_ContractorID",
                table: "CartItems",
                column: "ContractorID");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ListingID",
                table: "CartItems",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_ProvinceID_Name",
                table: "Cities",
                columns: new[] { "ProvinceID", "Name" },
                unique: true);

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
                name: "IX_DiscountTiers_CategoryID",
                table: "DiscountTiers",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountTiers_UpdatedByAdminID",
                table: "DiscountTiers",
                column: "UpdatedByAdminID");

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
                name: "IX_Disputes_RaisedDate",
                table: "Disputes",
                column: "RaisedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedByAdminID",
                table: "Disputes",
                column: "ResolvedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RespondentID",
                table: "Disputes",
                column: "RespondentID");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status",
                table: "Disputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_SupplierID",
                table: "Disputes",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocTypeID",
                table: "Documents",
                column: "DocTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserID",
                table: "Documents",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerifiedByUserID",
                table: "Documents",
                column: "VerifiedByUserID");

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
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

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
                name: "IX_Invoices_PaymentStatus",
                table: "Invoices",
                column: "PaymentStatus");

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
                name: "IX_ListingImages_ListingID",
                table: "ListingImages",
                column: "ListingID");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_AvailabilityStatus",
                table: "Listings",
                column: "AvailabilityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedDate",
                table: "Listings",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PricingMode",
                table: "Listings",
                column: "PricingMode");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SupplierID_AvailabilityStatus",
                table: "Listings",
                columns: new[] { "SupplierID", "AvailabilityStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RecipientID",
                table: "Messages",
                column: "RecipientID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderID",
                table: "Messages",
                column: "SenderID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadID_DateSent",
                table: "Messages",
                columns: new[] { "ThreadID", "DateSent" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserID_IsRead",
                table: "Notifications",
                columns: new[] { "UserID", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Reference",
                table: "OtpCodes",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_UserID",
                table: "OtpCodes",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordReset_UserID",
                table: "PasswordReset",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_InvoiceID",
                table: "Payouts",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ProcessedByAdminID",
                table: "Payouts",
                column: "ProcessedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_RequestedDate",
                table: "Payouts",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_Status",
                table: "Payouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_SupplierID",
                table: "Payouts",
                column: "SupplierID");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionKey",
                table: "Permissions",
                column: "PermissionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Name",
                table: "Provinces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_BookingID",
                table: "Quotations",
                column: "BookingID",
                unique: true);

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
                name: "IX_Refunds_InvoiceID",
                table: "Refunds",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProcessedDate",
                table: "Refunds",
                column: "ProcessedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RequestedByUserID",
                table: "Refunds",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Status",
                table: "Refunds",
                column: "Status");

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

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionID",
                table: "RolePermissions",
                column: "PermissionID");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suburbs_CityID_Name",
                table: "Suburbs",
                columns: new[] { "CityID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Threads_LastActivityDate",
                table: "Threads",
                column: "LastActivityDate");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantOneID_ParticipantTwoID",
                table: "Threads",
                columns: new[] { "ParticipantOneID", "ParticipantTwoID" });

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantTwoID",
                table: "Threads",
                column: "ParticipantTwoID");

            migrationBuilder.CreateIndex(
                name: "IX_TimerConfigurations_TimerKey",
                table: "TimerConfigurations",
                column: "TimerKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimerConfigurations_UpdatedByAdminID",
                table: "TimerConfigurations",
                column: "UpdatedByAdminID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "Users",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceAreas_ServiceAreaID",
                table: "UserServiceAreas",
                column: "ServiceAreaID");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ContractorID_ListingID",
                table: "WishlistItems",
                columns: new[] { "ContractorID", "ListingID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_ListingID",
                table: "WishlistItems",
                column: "ListingID");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingConditionInspections_Bookings_BookingID",
                table: "BookingConditionInspections",
                column: "BookingID",
                principalTable: "Bookings",
                principalColumn: "BookingID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingConditionInspections_Users_CompletedByUserID",
                table: "BookingConditionInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_ContractorID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_SupplierID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Users_SupplierID",
                table: "Listings");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingConditionInspections_Bookings_BookingID",
                table: "BookingConditionInspections");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BlockedTerms");

            migrationBuilder.DropTable(
                name: "BookingStatusHistories");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DepositDeductions");

            migrationBuilder.DropTable(
                name: "DiscountTiers");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "FeeConfigurations");

            migrationBuilder.DropTable(
                name: "Inspections");

            migrationBuilder.DropTable(
                name: "JobDocuments");

            migrationBuilder.DropTable(
                name: "ListingImages");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropTable(
                name: "PasswordReset");

            migrationBuilder.DropTable(
                name: "Payouts");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ReturnRequests");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Suburbs");

            migrationBuilder.DropTable(
                name: "TimerConfigurations");

            migrationBuilder.DropTable(
                name: "UserServiceAreas");

            migrationBuilder.DropTable(
                name: "WishlistItems");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "LeaseAgreements");

            migrationBuilder.DropTable(
                name: "Threads");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "ServiceAreas");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "BookingConditionInspections");

            migrationBuilder.DropTable(
                name: "Listings");
        }
    }
}
