using System.Text.Json;
using JiraDashboardApp.Core.Interfaces;
using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Infrastructure.Services;

public class FileWorkItemCacheService : IWorkItemCacheService
{
    private static readonly string CacheDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraDashboardApp");

    private static readonly string CachePath = Path.Combine(CacheDirectory, "workitem-cache.json");

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

    public async Task SaveAsync(IEnumerable<WorkItemSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);

        var dict = snapshots.ToDictionary(x => x.Key, x => x);

        await using var stream = File.Create(CachePath);
        await JsonSerializer.SerializeAsync(stream, dict, cancellationToken: cancellationToken);
    }
}