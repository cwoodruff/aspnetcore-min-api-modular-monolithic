using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Module.Data.Migrations
{
    /// <inheritdoc />
    internal partial class AddInvoiceStatusAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "orders",
                table: "Invoice",
                type: "varchar(20)",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "varchar(300)", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "varchar(2000)", nullable: true),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_DeadLetteredAt",
                schema: "orders",
                table: "OutboxMessage",
                column: "DeadLetteredAt",
                filter: "\"DeadLetteredAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_NextAttemptAt_OccurredAt",
                schema: "orders",
                table: "OutboxMessage",
                columns: new[] { "NextAttemptAt", "OccurredAt" },
                filter: "\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "orders");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "orders",
                table: "Invoice");
        }
    }
}
