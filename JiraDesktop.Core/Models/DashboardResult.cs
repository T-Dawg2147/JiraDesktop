namespace JiraDesktop.Core.Models;

/// <summary>
/// Aggregates the result of a dashboard data load, containing work items
/// and the distinct filter lists derived from those items.
/// </summary>
public sealed class DashboardResult
{
    /// <summary>Ordered list of work items to display (newest first by default).</summary>
    public List<WorkItem> Items { get; set; } = [];

    /// <summary>Distinct product manager names present in the loaded items.</summary>
    public List<string> ProductManagers { get; set; } = [];

    /// <summary>Distinct assignee display names present in the loaded items.</summary>
    public List<string> Assignees { get; set; } = [];
}
