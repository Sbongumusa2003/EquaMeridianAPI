using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PasswordReset_UserID",
                table: "PasswordReset",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordReset_Users_UserID",
                table: "PasswordReset",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PasswordReset_Users_UserID",
                table: "PasswordReset");

            migrationBuilder.DropIndex(
                name: "IX_PasswordReset_UserID",
                table: "PasswordReset");
        }
    }
}
