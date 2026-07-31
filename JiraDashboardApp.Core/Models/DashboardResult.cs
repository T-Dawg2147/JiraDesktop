namespace JiraDashboardApp.Core.Models;

public sealed class DashboardResult
{
    public List<WorkItem> Items { get; set; } = [];
    public List<string> ProductManagers { get; set; } = [];
    public List<string> Assignees { get; set; } = [];
}