using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admin.Module.Data.Migrations
{
    /// <inheritdoc />
    internal partial class AddCustomerPurchaseSummaryAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerPurchaseSummary",
                schema: "administration",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    InvoiceCount = table.Column<int>(type: "integer", nullable: false),
                    LastPurchaseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPurchaseSummary", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessage",
                schema: "administration",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandlerName = table.Column<string>(type: "varchar(300)", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessage", x => new { x.EventId, x.HandlerName });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPurchaseSummary",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "InboxMessage",
                schema: "administration");
        }
    }
}
