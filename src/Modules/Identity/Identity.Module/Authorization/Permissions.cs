namespace Identity.Modules.Authorization;

// Note: In a later iteration, move these to SharedKernel to share constants across modules.
public static class Permissions
{
    // Music
    public const string MusicRead = "music.read";
    public const string MusicWrite = "music.write";

    // Orders
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";

    // Administration
    public const string AdminUsersManage = "admin.users.manage";

    // Reporting
    public const string ReportView = "report.view";
}
