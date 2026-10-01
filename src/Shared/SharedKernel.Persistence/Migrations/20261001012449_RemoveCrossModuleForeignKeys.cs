using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharedKernel.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoice_Customer_CustomerId",
                schema: "orders",
                table: "Invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLine_Track_TrackId",
                schema: "orders",
                table: "InvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Track_Genre_GenreId",
                schema: "catalog",
                table: "Track");

            migrationBuilder.DropForeignKey(
                name: "FK_Track_MediaType_MediaTypeId",
                schema: "catalog",
                table: "Track");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Invoice_Customer_CustomerId",
                schema: "orders",
                table: "Invoice",
                column: "CustomerId",
                principalSchema: "administration",
                principalTable: "Customer",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLine_Track_TrackId",
                schema: "orders",
                table: "InvoiceLine",
                column: "TrackId",
                principalSchema: "catalog",
                principalTable: "Track",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Track_Genre_GenreId",
                schema: "catalog",
                table: "Track",
                column: "GenreId",
                principalSchema: "administration",
                principalTable: "Genre",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Track_MediaType_MediaTypeId",
                schema: "catalog",
                table: "Track",
                column: "MediaTypeId",
                principalSchema: "administration",
                principalTable: "MediaType",
                principalColumn: "Id");
        }
    }
}
