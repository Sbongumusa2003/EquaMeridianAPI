using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefundsMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DisputeID",
                table: "Refunds",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AdministratorNotes",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateUpdated",
                table: "Refunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceID",
                table: "Refunds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayReference",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    ThreadID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantOneID = table.Column<int>(type: "int", nullable: false),
                    ParticipantTwoID = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "Messages",
                columns: table => new
                {
                    MessageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThreadID = table.Column<int>(type: "int", nullable: false),
                    SenderID = table.Column<int>(type: "int", nullable: false),
                    RecipientID = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentURL = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    DateSent = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_InvoiceID",
                table: "Refunds",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RecipientID",
                table: "Messages",
                column: "RecipientID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderID",
                table: "Messages",
                column: "SenderID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadID",
                table: "Messages",
                column: "ThreadID");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantOneID",
                table: "Threads",
                column: "ParticipantOneID");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_ParticipantTwoID",
                table: "Threads",
                column: "ParticipantTwoID");

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_Invoices_InvoiceID",
                table: "Refunds",
                column: "InvoiceID",
                principalTable: "Invoices",
                principalColumn: "InvoiceID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Invoices_InvoiceID",
                table: "Refunds");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Threads");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_InvoiceID",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "AdministratorNotes",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DateUpdated",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "InvoiceID",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "GatewayReference",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastSyncedDate",
                table: "Invoices");

            migrationBuilder.AlterColumn<int>(
                name: "DisputeID",
                table: "Refunds",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
