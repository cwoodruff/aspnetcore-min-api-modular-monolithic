namespace Identity.Contracts;

/// <summary>
/// Permission names. Each is a claim value in a user's "permissions" claim and also the name of the
/// authorization policy that requires it. Modules reference these constants, never the strings
/// (ADR-0011): a rename is a compile error, not a silent 403.
/// </summary>
public static class Permissions
{
    // Catalog
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";

    // Orders
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";

    // Administration
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdministrationRead = "administration.read";
    public const string AdministrationWrite = "administration.write";

    // Reporting
    public const string ReportView = "report.view";
}
