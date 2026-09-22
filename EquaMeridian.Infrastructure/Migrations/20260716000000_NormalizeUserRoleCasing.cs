using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquaMeridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserRoleCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize any casing variant of "contractor" to the canonical "Contractor"
            // so that ordinal / case-sensitive comparisons and displayed values are consistent.
            migrationBuilder.Sql(@"
                UPDATE Users
                SET Role = 'Contractor'
                WHERE Role = 'contractor' COLLATE Latin1_General_CS_AS
                   OR Role = 'CONTRACTOR' COLLATE Latin1_General_CS_AS
                   OR Role = 'contRACTOR' COLLATE Latin1_General_CS_AS;
            ");

            // While we're at it, normalize Supplier the same way for consistency.
            migrationBuilder.Sql(@"
                UPDATE Users
                SET Role = 'Supplier'
                WHERE Role = 'supplier' COLLATE Latin1_General_CS_AS
                   OR Role = 'SUPPLIER' COLLATE Latin1_General_CS_AS;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-fix migration: no schema change to revert, and we don't want to
            // reintroduce the bad casing on downgrade.
        }
    }
}
