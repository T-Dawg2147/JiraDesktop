using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Services;

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

    public async Task<DashboardResult> LoadDashboardAsync(string profileId, CancellationToken ct = default)
    {
        var previous = await _cacheService.LoadAsync(profileId, ct);
        var current = await _jiraService.GetWorkItemsAsync(ct);

        foreach (var item in current)
        {
            if (!previous.TryGetValue(item.Key, out var snapshot))
                continue;

            AddIfChanged(item, "Summary", snapshot.Summary, item.Summary);
            AddIfChanged(item, "Assignee", snapshot.Assignee, item.Assignee);
            AddIfChanged(item, "Priority", snapshot.Priority, item.Priority);
            AddIfChanged(item, "Status", snapshot.Status, item.Status);
            AddIfChanged(item, "Due Date", snapshot.DueDate, item.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty);
            AddIfChanged(item, "Product Manager", snapshot.ProductManager, item.ProductManager);

            if (item.Changes.Count > 0)
            {
                item.HasDetectedChanges = true;
                item.IsPulseActive = true;
            }
        }

        var snapshots = current.Select(x => new WorkItemSnapshot
        {
            Key = x.Key,
            Summary = x.Summary,
            Assignee = x.Assignee,
            Priority = x.Priority,
            Status = x.Status,
            DueDate = x.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductManager = x.ProductManager,
            Updated = x.Updated
        });

        await _cacheService.SaveAsync(profileId, snapshots, ct);

        return new DashboardResult
        {
            Items = current,
            ProductManagers = current
                .SelectMany(x => SplitCsv(x.ProductManager))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            Assignees = current
                .Select(x => x.Assignee)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList()
        };
    }

    private static IEnumerable<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void AddIfChanged(WorkItem item, string field, string oldValue, string newValue)
    {
        var before = string.IsNullOrWhiteSpace(oldValue) ? "-" : oldValue.Trim();
        var after = string.IsNullOrWhiteSpace(newValue) ? "-" : newValue.Trim();
        if (string.Equals(before, after, StringComparison.OrdinalIgnoreCase))
            return;

        item.Changes.Add(new WorkItemFieldChange
        {
            Field = field,
            FromValue = before,
            ToValue = after,
            ChangedAt = DateTime.UtcNow
        });
    }
}
