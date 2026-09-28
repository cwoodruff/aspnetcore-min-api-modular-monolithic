namespace Identity.Modules.Authorization;

// Note: In a later iteration, move these to SharedKernel to share constants across modules.
internal static class Permissions
{
    // Catalog
    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";

    // Orders
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";

    // Administration
    public const string AdminUsersManage = "admin.users.manage"; // administration.read
    public const string AdministrationRead = "administration.read";
    public const string AdministrationWrite = "administration.write";

    // Reporting
    public const string ReportView = "report.view";
}
