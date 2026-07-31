using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Core.Interfaces;

public interface IWorkItemCacheService
{
    Task<Dictionary<string, WorkItemSnapshot>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IEnumerable<WorkItemSnapshot> snapshots, CancellationToken ct = default);
}