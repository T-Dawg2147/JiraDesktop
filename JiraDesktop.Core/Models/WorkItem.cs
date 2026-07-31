namespace JiraDesktop.Core.Models;

/// <summary>
/// Represents a single Jira issue displayed on the dashboard.
/// </summary>
public sealed class WorkItem
{
    /// <summary>Jira issue key, e.g. "PROJ-42".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Issue summary/title text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Display name of the assigned user, or "Unassigned".</summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>Priority name (e.g. "High", "Medium"), or "-" if not set.</summary>
    public string Priority { get; set; } = "-";

    /// <summary>Workflow status name (e.g. "In Progress", "Done"), or "-" if not set.</summary>
    public string Status { get; set; } = "-";

    /// <summary>Optional due date for the issue.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>UTC timestamp of the last update in Jira.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Full browser URL to this issue on the Jira site.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Value of the Product Manager custom field.</summary>
    public string ProductManager { get; set; } = string.Empty;

    /// <summary>Field-level changes detected since the last sync.</summary>
    public List<WorkItemFieldChange> Changes { get; set; } = [];

    /// <summary>Indicates that changes were detected on the most recent sync cycle.</summary>
    public bool HasDetectedChanges { get; set; }

    /// <summary>Controls the pulsing highlight animation until the row is clicked.</summary>
    public bool IsPulseActive { get; set; }

    /// <summary>True when at least one field change has been detected.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Human-readable summary of the number of changed fields.</summary>
    public string ChangeSummary => Changes.Count == 1 ? "1 field changed" : $"{Changes.Count} fields changed";
}
