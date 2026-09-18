namespace JiraDesktop.Core.Models;

public sealed class UserWidgetSettings
{
    public bool AlwaysOnTop { get; set; }
    public bool EnableChangeNotifications { get; set; } = true;
    public bool EnableNotificationSound { get; set; }
    public bool HideDone { get; set; } = true;
    public bool ShowChangedOnly { get; set; }
    public string ThemeName { get; set; } = "JiraLight";
    public int SyncIntervalSeconds { get; set; } = 60;
    public string SearchText { get; set; } = string.Empty;
    public string SelectedProductManager { get; set; } = "All";
    public string SelectedAssignee { get; set; } = "All";
    public string SortField { get; set; } = "Updated";
    public bool SortAscending { get; set; }
    public double WindowTop { get; set; } = 80;
    public double WindowLeft { get; set; } = 80;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 760;
}
