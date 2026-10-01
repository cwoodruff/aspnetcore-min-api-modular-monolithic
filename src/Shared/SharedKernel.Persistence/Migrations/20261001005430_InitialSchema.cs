using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SharedKernel.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "Artist",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "varchar(120)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artist", x => x.Id);
                });

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
                name: "Playlist",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "varchar(120)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Album",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "varchar(160)", nullable: true),
                    ArtistId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Album", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Album_Artist_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "catalog",
                        principalTable: "Artist",
                        principalColumn: "Id");
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

            migrationBuilder.CreateTable(
                name: "Track",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "varchar(200)", nullable: true),
                    AlbumId = table.Column<int>(type: "integer", nullable: true),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: true),
                    GenreId = table.Column<int>(type: "integer", nullable: true),
                    Composer = table.Column<string>(type: "varchar(220)", nullable: true),
                    Milliseconds = table.Column<int>(type: "integer", nullable: true),
                    Bytes = table.Column<int>(type: "integer", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Track", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Track_Album_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "catalog",
                        principalTable: "Album",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Track_Genre_GenreId",
                        column: x => x.GenreId,
                        principalSchema: "administration",
                        principalTable: "Genre",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Track_MediaType_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalSchema: "administration",
                        principalTable: "MediaType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoice",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingAddress = table.Column<string>(type: "varchar(70)", nullable: true),
                    BillingCity = table.Column<string>(type: "varchar(40)", nullable: true),
                    BillingState = table.Column<string>(type: "varchar(40)", nullable: true),
                    BillingCountry = table.Column<string>(type: "varchar(40)", nullable: true),
                    BillingPostalCode = table.Column<string>(type: "varchar(10)", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoice_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "administration",
                        principalTable: "Customer",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlaylistTrack",
                schema: "catalog",
                columns: table => new
                {
                    PlaylistId = table.Column<int>(type: "integer", nullable: false),
                    TrackId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistTrack", x => new { x.PlaylistId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_PlaylistTrack_Playlist_PlaylistId",
                        column: x => x.PlaylistId,
                        principalSchema: "catalog",
                        principalTable: "Playlist",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlaylistTrack_Track_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "catalog",
                        principalTable: "Track",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLine",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: true),
                    TrackId = table.Column<int>(type: "integer", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLine_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "orders",
                        principalTable: "Invoice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceLine_Track_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "catalog",
                        principalTable: "Track",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Album_ArtistId",
                schema: "catalog",
                table: "Album",
                column: "ArtistId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_CustomerId",
                schema: "orders",
                table: "Invoice",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLine_InvoiceId",
                schema: "orders",
                table: "InvoiceLine",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLine_TrackId",
                schema: "orders",
                table: "InvoiceLine",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTrack_TrackId",
                schema: "catalog",
                table: "PlaylistTrack",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Track_AlbumId",
                schema: "catalog",
                table: "Track",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Track_GenreId",
                schema: "catalog",
                table: "Track",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_Track_MediaTypeId",
                schema: "catalog",
                table: "Track",
                column: "MediaTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceLine",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "PlaylistTrack",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Invoice",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "Playlist",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Track",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Customer",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "Album",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Genre",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "MediaType",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "Employee",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "Artist",
                schema: "catalog");
        }
    }
}
