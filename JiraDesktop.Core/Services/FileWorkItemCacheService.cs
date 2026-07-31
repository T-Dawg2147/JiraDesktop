using System.Text.Json;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Services;

/// <summary>
/// Persists and loads work item snapshots as a JSON file in the user's local application data folder.
/// Used by <see cref="DashboardService"/> to compare the current Jira state against the previous sync.
/// </summary>
public class FileWorkItemCacheService : IWorkItemCacheService
{
    private static readonly string CacheDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraDesktop");

    private static readonly string CachePath = Path.Combine(CacheDirectory, "workitem-cache.json");

    /// <inheritdoc/>
    public async Task<Dictionary<string, WorkItemSnapshot>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(CachePath))
            return new Dictionary<string, WorkItemSnapshot>();

        await using var stream = File.OpenRead(CachePath);
        var result = await JsonSerializer.DeserializeAsync<Dictionary<string, WorkItemSnapshot>>(
            stream,
            cancellationToken: cancellationToken);

        return result ?? new Dictionary<string, WorkItemSnapshot>();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(IEnumerable<WorkItemSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);

        var dict = snapshots.ToDictionary(x => x.Key, x => x);

        await using var stream = File.Create(CachePath);
        await JsonSerializer.SerializeAsync(stream, dict, cancellationToken: cancellationToken);
    }
}
