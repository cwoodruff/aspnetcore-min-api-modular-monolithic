namespace Reporting.Modules.Data;

/// <summary>
/// Everything Reporting knows about other modules' tables, in one place (ADR-0014, ADR-0015): the SQL of
/// its two views and the four orphan checks, plus every (schema, table, column) they reference. A test
/// checks those references against the current models of the Catalog, Orders and Administration
/// contexts, so a rename in another module fails Reporting's tests before it breaks a deploy.
/// </summary>
internal static class ReportingSql
{
    public const string ReaderRole = "reporting_reader";

    public const string SalesByGenreView = "sales_by_genre";
    public const string InvoiceLinesWithNamesView = "invoice_lines_with_names";

    // Versioned: a migration uses the version it was written with. Changing a view means adding a V2 and a
    // migration that replaces the view, never editing V1.
    public const string CreateSalesByGenreV1 = """
        CREATE VIEW reporting.sales_by_genre AS
        SELECT g."Id" AS "GenreId",
               g."Name" AS "GenreName",
               COUNT(DISTINCT t."Id") AS "TrackCount",
               COALESCE(SUM(ts."TimesSold"), 0)::bigint AS "UnitsSold"
        FROM administration."Genre" g
        LEFT JOIN catalog."Track" t ON t."GenreId" = g."Id"
        LEFT JOIN catalog."TrackSales" ts ON ts."TrackId" = t."Id"
        GROUP BY g."Id", g."Name";
        """;

    public const string CreateInvoiceLinesWithNamesV1 = """
        CREATE VIEW reporting.invoice_lines_with_names AS
        SELECT il."Id" AS "InvoiceLineId",
               il."InvoiceId" AS "InvoiceId",
               il."TrackId" AS "TrackId",
               t."Name" AS "TrackName",
               i."CustomerId" AS "CustomerId",
               c."FirstName" || ' ' || c."LastName" AS "CustomerName",
               il."UnitPrice" AS "UnitPrice",
               il."Quantity" AS "Quantity"
        FROM orders."InvoiceLine" il
        JOIN orders."Invoice" i ON i."Id" = il."InvoiceId"
        LEFT JOIN catalog."Track" t ON t."Id" = il."TrackId"
        LEFT JOIN administration."Customer" c ON c."Id" = i."CustomerId";
        """;

    /// <summary>One check per cross-module foreign key removed in phase 2 (ADR-0004 to ADR-0007).</summary>
    public static readonly IReadOnlyList<OrphanCheck> OrphanChecks =
    [
        new("invoice-line-track", "orders", "InvoiceLine", "TrackId", "catalog", "Track"),
        new("invoice-customer", "orders", "Invoice", "CustomerId", "administration", "Customer"),
        new("track-genre", "catalog", "Track", "GenreId", "administration", "Genre"),
        new("track-media-type", "catalog", "Track", "MediaTypeId", "administration", "MediaType")
    ];

    /// <summary>Every other module's column the views and checks read.</summary>
    public static readonly IReadOnlyList<ColumnReference> References =
    [
        // sales_by_genre
        new("administration", "Genre", "Id"), new("administration", "Genre", "Name"),
        new("catalog", "Track", "Id"), new("catalog", "Track", "GenreId"),
        new("catalog", "TrackSales", "TrackId"), new("catalog", "TrackSales", "TimesSold"),

        // invoice_lines_with_names
        new("orders", "InvoiceLine", "Id"), new("orders", "InvoiceLine", "InvoiceId"),
        new("orders", "InvoiceLine", "TrackId"), new("orders", "InvoiceLine", "UnitPrice"),
        new("orders", "InvoiceLine", "Quantity"), new("orders", "Invoice", "Id"),
        new("orders", "Invoice", "CustomerId"), new("catalog", "Track", "Name"),
        new("administration", "Customer", "Id"), new("administration", "Customer", "FirstName"),
        new("administration", "Customer", "LastName"),

        // orphan checks: source id and reference column, target id
        .. OrphanChecks.SelectMany(check => new ColumnReference[]
        {
            new(check.SourceSchema, check.SourceTable, "Id"),
            new(check.SourceSchema, check.SourceTable, check.ReferenceColumn),
            new(check.TargetSchema, check.TargetTable, "Id")
        })
    ];

    /// <summary>Creates the read-only role (once per server) and grants it what Reporting needs.</summary>
    public const string GrantReaderV1 = """
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'reporting_reader') THEN
                CREATE ROLE reporting_reader NOLOGIN;
            END IF;
        END
        $$;

        GRANT USAGE ON SCHEMA catalog, orders, administration TO reporting_reader;
        GRANT SELECT ON ALL TABLES IN SCHEMA catalog, orders, administration TO reporting_reader;
        ALTER DEFAULT PRIVILEGES IN SCHEMA catalog, orders, administration GRANT SELECT ON TABLES TO reporting_reader;

        GRANT USAGE ON SCHEMA reporting TO reporting_reader;
        GRANT SELECT ON reporting.sales_by_genre, reporting.invoice_lines_with_names TO reporting_reader;
        GRANT SELECT, INSERT, UPDATE ON reporting."IntegrityFinding" TO reporting_reader;
        """;

    public const string RevokeReaderV1 = """
        REVOKE ALL ON reporting."IntegrityFinding" FROM reporting_reader;
        REVOKE ALL ON SCHEMA reporting FROM reporting_reader;
        ALTER DEFAULT PRIVILEGES IN SCHEMA catalog, orders, administration REVOKE SELECT ON TABLES FROM reporting_reader;
        REVOKE SELECT ON ALL TABLES IN SCHEMA catalog, orders, administration FROM reporting_reader;
        REVOKE USAGE ON SCHEMA catalog, orders, administration FROM reporting_reader;
        """;
}

internal sealed record OrphanCheck(
    string Name,
    string SourceSchema,
    string SourceTable,
    string ReferenceColumn,
    string TargetSchema,
    string TargetTable);

internal sealed record ColumnReference(string Schema, string Table, string Column);
