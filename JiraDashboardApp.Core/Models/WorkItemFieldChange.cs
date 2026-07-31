namespace JiraDashboardApp.Core.Models;

public sealed class WorkItemFieldChange
{
    public string Field { get; set; } = string.Empty;
    public string FromValue { get; set; } = string.Empty;
    public string ToValue { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}