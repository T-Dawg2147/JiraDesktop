namespace JiraDesktop.Core.Models;

/// <summary>
/// A lightweight, serializable snapshot of a work item's key fields,
/// stored between syncs and used as the baseline for change detection.
/// </summary>
public sealed class WorkItemSnapshot
{
    /// <summary>Jira issue key, e.g. "PROJ-42".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Snapshotted summary/title text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Snapshotted assignee display name.</summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>Snapshotted priority name.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>Snapshotted workflow status name.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Snapshotted due date as an ISO date string ("yyyy-MM-dd"), or empty.</summary>
    public string DueDate { get; set; } = string.Empty;

    /// <summary>Snapshotted product manager value.</summary>
    public string ProductManager { get; set; } = string.Empty;

    /// <summary>Snapshotted UTC updated timestamp from Jira.</summary>
    public DateTime Updated { get; set; }
}
