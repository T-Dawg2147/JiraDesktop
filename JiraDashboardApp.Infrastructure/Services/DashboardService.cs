using JiraDashboardApp.Core.Interfaces;
using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Infrastructure.Services;

public sealed class DashboardService
{
    private readonly IJiraService _jiraService;
    private readonly IWorkItemCacheService _cacheService;

    public DashboardService(IJiraService jiraService, IWorkItemCacheService cacheService)
    {
        _jiraService = jiraService;
        _cacheService = cacheService;
    }

    public Task<List<string>> GetAllProductManagerOptionsAsync(CancellationToken ct = default)
        => _jiraService.GetAllProductManagerOptionsAsync(ct);
    
    public async Task<DashboardResult> LoadDashboardAsync(string? productManager, string? assignee,
        CancellationToken ct = default)
    {
        var previous = await _cacheService.LoadAsync(ct);
        var current = await _jiraService.GetWorkItemsAsync(productManager, assignee, ct);

        foreach (var item in current)
        {
            if (previous.TryGetValue(item.Key, out var prev))
            {
                AddIfChanged(item, "summary", prev.Summary, item.Summary);
                AddIfChanged(item, "assignee", prev.Assignee, item.Assignee);
                AddIfChanged(item, "priority", prev.Priority, item.Priority);
                AddIfChanged(item, "status", prev.Status, item.Status);
                AddIfChanged(item, "duedate", prev.DueDate, item.DueDate?.ToString("yyyy-MM-dd") ?? "");
                AddIfChanged(item, "Product Managers", prev.ProductManager, item.ProductManager);
            }
        }

        var snapshots = current.Select(x => new WorkItemSnapshot
        {
            Key = x.Key,
            Summary = x.Summary,
            Assignee = x.Assignee,
            Priority = x.Priority,
            Status = x.Status,
            DueDate = x.DueDate?.ToString("yyyy-MM-dd") ?? "",
            ProductManager = x.ProductManager,
            Updated = x.Updated
        });

        await _cacheService.SaveAsync(snapshots, ct);

        var result = new DashboardResult
        {
            Items = current.OrderByDescending(x => x.Updated).ToList(),
            ProductManagers = current.SelectMany(x => x.ProductManager.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().Order().ToList(),
            Assignees = current.Select(x => x.Assignee).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToList()
        };

        return result;
    }

    private static void AddIfChanged(WorkItem item, string field, string oldValue, string newValue)
    {
        if (!string.Equals(oldValue.Trim(), newValue?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            item.Changes.Add(new WorkItemFieldChange
            {
                Field = field,
                FromValue = string.IsNullOrWhiteSpace(oldValue) ? "-" : oldValue,
                ToValue = string.IsNullOrWhiteSpace(newValue) ? "-" : newValue,
                ChangedAt = DateTime.UtcNow
            });
        }
    }
}