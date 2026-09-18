using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Interfaces;

public interface IWorkItemCacheService
{
    Task<Dictionary<string, WorkItemSnapshot>> LoadAsync(string profileId, CancellationToken cancellationToken = default);
    Task SaveAsync(string profileId, IEnumerable<WorkItemSnapshot> snapshots, CancellationToken cancellationToken = default);
}
