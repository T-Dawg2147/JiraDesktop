namespace JiraDashboardApp.Core.Models;

public class UserWidgetSettings
{
    public bool AlwaysOnTop { get; set; } = false;
    public bool AutoHideDrawerOnFocusLoss { get; set; } = true;

    public string WatchedProductManager { get; set; } = "All";
    public bool EnableChangeNotifications { get; set; } = true;
    public bool EnableNotificationSound { get; set; } = false;

    public int PageSize { get; set; } = 50;
    public int SyncIntervalSeconds { get; set; } = 60;

    public string SearchText { get; set; } = "";
    public bool HideDone { get; set; } = true;

    public string ThemeName { get; set; } = "JiraLight";

    public double WindowTop { get; set; } = 80;
    public double WindowLeft { get; set; } = 80;
    public double WindowWidth { get; set; } = 760;
    public double WindowHeight { get; set; } = 470;
}