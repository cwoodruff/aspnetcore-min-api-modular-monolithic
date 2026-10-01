using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Reporting.Modules.Data;

#nullable disable

namespace Reporting.Module.Data.Migrations
{
    /// <inheritdoc />
    internal partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "IntegrityFinding",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckName = table.Column<string>(type: "varchar(100)", nullable: false),
                    SourceSchema = table.Column<string>(type: "varchar(63)", nullable: false),
                    SourceTable = table.Column<string>(type: "varchar(63)", nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: false),
                    MissingReference = table.Column<string>(type: "varchar(200)", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrityFinding", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrityFinding_Open",
                schema: "reporting",
                table: "IntegrityFinding",
                columns: new[] { "CheckName", "SourceId" },
                unique: true,
                filter: "\"ResolvedAt\" IS NULL");

            // The read model over the other modules' schemas (ADR-0014), and the read-only role that queries it.
            migrationBuilder.Sql(ReportingSql.CreateSalesByGenreV1);
            migrationBuilder.Sql(ReportingSql.CreateInvoiceLinesWithNamesV1);
            migrationBuilder.Sql(ReportingSql.GrantReaderV1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The role is server-wide and may serve other databases; only this database's grants are revoked.
            migrationBuilder.Sql(ReportingSql.RevokeReaderV1);
            migrationBuilder.Sql("DROP VIEW reporting.invoice_lines_with_names; DROP VIEW reporting.sales_by_genre;");

            migrationBuilder.DropTable(
                name: "IntegrityFinding",
                schema: "reporting");
        }
    }
}
