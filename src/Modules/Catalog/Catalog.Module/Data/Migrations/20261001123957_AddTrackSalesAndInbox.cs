using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Module.Data.Migrations
{
    /// <inheritdoc />
    internal partial class AddTrackSalesAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessage",
                schema: "catalog",
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

            migrationBuilder.CreateTable(
                name: "TrackSales",
                schema: "catalog",
                columns: table => new
                {
                    TrackId = table.Column<int>(type: "integer", nullable: false),
                    TimesSold = table.Column<int>(type: "integer", nullable: false),
                    LastSoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackSales", x => x.TrackId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessage",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TrackSales",
                schema: "catalog");
        }
    }
}
