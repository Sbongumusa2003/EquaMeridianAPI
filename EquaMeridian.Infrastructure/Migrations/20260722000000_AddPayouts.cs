using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    PayoutID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<int>(type: "int", nullable: false),
                    SupplierID = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayoutAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdministratorNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedByAdminID = table.Column<int>(type: "int", nullable: true),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                        name: "FK_Payouts_Users_SupplierID",
                        column: x => x.SupplierID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payouts_Users_ProcessedByAdminID",
                        column: x => x.ProcessedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payouts");
        }
    }
}
