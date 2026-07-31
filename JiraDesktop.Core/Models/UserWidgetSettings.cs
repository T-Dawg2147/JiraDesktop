namespace JiraDesktop.Core.Models;

/// <summary>
/// User-configurable widget preferences persisted to the local profile between sessions.
/// </summary>
public class UserWidgetSettings
{
    /// <summary>Whether the window is pinned above all other windows.</summary>
    public bool AlwaysOnTop { get; set; } = false;

    /// <summary>Whether the settings drawer closes automatically when the window loses focus.</summary>
    public bool AutoHideDrawerOnFocusLoss { get; set; } = true;

    /// <summary>The product manager filter value to use for change notifications ("All" = watch everything).</summary>
    public string WatchedProductManager { get; set; } = "All";

    /// <summary>Whether desktop toast notifications are shown when watched issues change.</summary>
    public bool EnableChangeNotifications { get; set; } = true;

    /// <summary>Whether a sound is played alongside change notifications.</summary>
    public bool EnableNotificationSound { get; set; } = false;

    /// <summary>Number of items displayed per page in the data grid.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>How often (in seconds) the dashboard automatically re-syncs from Jira.</summary>
    public int SyncIntervalSeconds { get; set; } = 60;

    /// <summary>The last search text the user typed, restored on next launch.</summary>
    public string SearchText { get; set; } = "";

    /// <summary>Whether issues with status "Done" are hidden from the grid.</summary>
    public bool HideDone { get; set; } = true;

    /// <summary>The active UI theme name (e.g. "JiraLight" or "JiraDark").</summary>
    public string ThemeName { get; set; } = "JiraLight";

    /// <summary>Saved window Y position.</summary>
    public double WindowTop { get; set; } = 80;

    /// <summary>Saved window X position.</summary>
    public double WindowLeft { get; set; } = 80;

    /// <summary>Saved window width in device-independent pixels.</summary>
    public double WindowWidth { get; set; } = 760;

    /// <summary>Saved window height in device-independent pixels.</summary>
    public double WindowHeight { get; set; } = 470;
}
