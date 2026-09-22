using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Threads_ParticipantOneID",
                table: "Threads");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ThreadID",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Listings_SupplierID",
                table: "Listings");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Refunds",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AvailabilityStatus",
                table: "Listings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Invoices",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Disputes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_LastActivityDate",
                table: "Threads",
                column: "LastActivityDate");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantOneID_ParticipantTwoID",
                table: "Threads",
                columns: new[] { "ParticipantOneID", "ParticipantTwoID" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProcessedDate",
                table: "Refunds",
                column: "ProcessedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Status",
                table: "Refunds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadID_DateSent",
                table: "Messages",
                columns: new[] { "ThreadID", "DateSent" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_AvailabilityStatus",
                table: "Listings",
                column: "AvailabilityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedDate",
                table: "Listings",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SupplierID_AvailabilityStatus",
                table: "Listings",
                columns: new[] { "SupplierID", "AvailabilityStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PaymentStatus",
                table: "Invoices",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RaisedDate",
                table: "Disputes",
                column: "RaisedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status",
                table: "Disputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Threads_LastActivityDate",
                table: "Threads");

            migrationBuilder.DropIndex(
                name: "IX_Threads_ParticipantOneID_ParticipantTwoID",
                table: "Threads");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_ProcessedDate",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_Status",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ThreadID_DateSent",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Listings_AvailabilityStatus",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_CreatedDate",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_SupplierID_AvailabilityStatus",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PaymentStatus",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_RaisedDate",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_Status",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "AvailabilityStatus",
                table: "Listings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Disputes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantOneID",
                table: "Threads",
                column: "ParticipantOneID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadID",
                table: "Messages",
                column: "ThreadID");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SupplierID",
                table: "Listings",
                column: "SupplierID");
        }
    }
}
