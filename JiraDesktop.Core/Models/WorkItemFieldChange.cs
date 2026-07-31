namespace JiraDesktop.Core.Models;

/// <summary>
/// Describes a single field-level change detected on a work item between two sync cycles.
/// </summary>
public sealed class WorkItemFieldChange
{
    /// <summary>Name of the Jira field that changed (e.g. "Status", "Assignee").</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The field value before the change, or "-" if it was empty.</summary>
    public string FromValue { get; set; } = string.Empty;

    /// <summary>The field value after the change, or "-" if it is now empty.</summary>
    public string ToValue { get; set; } = string.Empty;

    /// <summary>UTC timestamp at which the change was detected (local detection time).</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
