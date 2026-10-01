using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Admin.Module.Data.Migrations
{
    /// <inheritdoc />
    internal partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.CreateTable(
                name: "Employee",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastName = table.Column<string>(type: "varchar(20)", nullable: true),
                    FirstName = table.Column<string>(type: "varchar(20)", nullable: true),
                    Title = table.Column<string>(type: "varchar(30)", nullable: true),
                    ReportsTo = table.Column<int>(type: "integer", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HireDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Address = table.Column<string>(type: "varchar(70)", nullable: true),
                    City = table.Column<string>(type: "varchar(40)", nullable: true),
                    State = table.Column<string>(type: "varchar(40)", nullable: true),
                    Country = table.Column<string>(type: "varchar(40)", nullable: true),
                    PostalCode = table.Column<string>(type: "varchar(10)", nullable: true),
                    Phone = table.Column<string>(type: "varchar(24)", nullable: true),
                    Fax = table.Column<string>(type: "varchar(24)", nullable: true),
                    Email = table.Column<string>(type: "varchar(60)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employee_Employee_ReportsTo",
                        column: x => x.ReportsTo,
                        principalSchema: "administration",
                        principalTable: "Employee",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Genre",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "varchar(120)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genre", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaType",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "varchar(120)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "varchar(40)", nullable: true),
                    LastName = table.Column<string>(type: "varchar(20)", nullable: true),
                    Company = table.Column<string>(type: "varchar(80)", nullable: true),
                    Address = table.Column<string>(type: "varchar(70)", nullable: true),
                    City = table.Column<string>(type: "varchar(40)", nullable: true),
                    State = table.Column<string>(type: "varchar(40)", nullable: true),
                    Country = table.Column<string>(type: "varchar(40)", nullable: true),
                    PostalCode = table.Column<string>(type: "varchar(10)", nullable: true),
                    Phone = table.Column<string>(type: "varchar(24)", nullable: true),
                    Fax = table.Column<string>(type: "varchar(24)", nullable: true),
                    Email = table.Column<string>(type: "varchar(60)", nullable: true),
                    SupportRepId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customer_Employee_SupportRepId",
                        column: x => x.SupportRepId,
                        principalSchema: "administration",
                        principalTable: "Employee",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_SupportRepId",
                schema: "administration",
                table: "Customer",
                column: "SupportRepId");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_ReportsTo",
                schema: "administration",
                table: "Employee",
                column: "ReportsTo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customer",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "Genre",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "MediaType",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "Employee",
                schema: "administration");
        }
    }
}
