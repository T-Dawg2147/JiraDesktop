namespace JiraDashboardApp.Core.Models;

public sealed class WorkItemSnapshot
{
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string ProductManager { get; set; } = string.Empty;
    public DateTime Updated { get; set; }
}