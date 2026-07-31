using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Core.Interfaces;

public interface IJiraService
{
    Task<List<WorkItem>> GetWorkItemsAsync(string? productManager, string? assignee, CancellationToken cancellationToken = default);
    
    Task<List<string>> GetAllProductManagerOptionsAsync(CancellationToken cancellationToken = default);
}