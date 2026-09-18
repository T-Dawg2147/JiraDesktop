using System.Text.Json;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Services;

public sealed class FileWorkItemCacheService : IWorkItemCacheService
{
    private static readonly string RootDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraDesktop", "profiles");

    public async Task<Dictionary<string, WorkItemSnapshot>> LoadAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachePath(profileId);
        if (!File.Exists(cachePath))
            return new Dictionary<string, WorkItemSnapshot>(StringComparer.OrdinalIgnoreCase);

        await using var stream = File.OpenRead(cachePath);
        var result = await JsonSerializer.DeserializeAsync<Dictionary<string, WorkItemSnapshot>>(
            stream,
            cancellationToken: cancellationToken);

        return result ?? new Dictionary<string, WorkItemSnapshot>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(string profileId, IEnumerable<WorkItemSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachePath(profileId);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        var dict = snapshots
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

        await using var stream = File.Create(cachePath);
        await JsonSerializer.SerializeAsync(stream, dict, cancellationToken: cancellationToken);
    }

    private static string GetCachePath(string profileId)
        => Path.Combine(RootDirectory, profileId, "workitem-cache.json");
}
