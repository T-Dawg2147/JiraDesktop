using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Interfaces;

/// <summary>
/// Persists and retrieves snapshots of work items used for change detection between syncs.
/// </summary>
public interface IWorkItemCacheService
{
    /// <summary>
    /// Loads the previously saved work item snapshots from persistent storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary keyed by Jira issue key (e.g. "PROJ-42").</returns>
    Task<Dictionary<string, WorkItemSnapshot>> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the current set of work item snapshots to persistent storage.
    /// </summary>
    /// <param name="snapshots">The snapshots to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(IEnumerable<WorkItemSnapshot> snapshots, CancellationToken ct = default);
}
