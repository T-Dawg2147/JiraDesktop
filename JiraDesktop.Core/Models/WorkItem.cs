using System.Collections.ObjectModel;

namespace JiraDesktop.Core.Models;

public sealed class WorkItem
{
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Priority { get; set; } = "-";
    public string Status { get; set; } = "-";
    public DateTime? DueDate { get; set; }
    public DateTime Updated { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ProductManager { get; set; } = string.Empty;
    public ObservableCollection<WorkItemFieldChange> Changes { get; } = [];
    public ObservableCollection<JiraStatusTransition> AvailableTransitions { get; } = [];
    public bool HasDetectedChanges { get; set; }
    public bool IsPulseActive { get; set; }
    public string SelectedTransitionId { get; set; } = string.Empty;
    public bool HasChanges => Changes.Count > 0;
    public string ChangeSummary => Changes.Count == 1 ? "1 field changed" : $"{Changes.Count} fields changed";
}
