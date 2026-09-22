using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAreas",
                columns: table => new
                {
                    ServiceAreaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAreas", x => x.ServiceAreaID);
                });

            migrationBuilder.CreateTable(
                name: "UserServiceAreas",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ServiceAreaID = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceAreas_ServiceAreaID",
                table: "UserServiceAreas",
                column: "ServiceAreaID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserServiceAreas");

            migrationBuilder.DropTable(
                name: "ServiceAreas");
        }
    }
}
