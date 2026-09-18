using System.Text.Json;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Services;

public sealed class UserProfileService
{
    private static readonly string StoreDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraDesktop");

    private static readonly string ProfilesPath = Path.Combine(StoreDirectory, "profiles.json");
    private static readonly string ActiveProfilePath = Path.Combine(StoreDirectory, "active-profile.txt");

    public async Task<List<UserProfile>> LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StoreDirectory);

        if (!File.Exists(ProfilesPath))
        {
            var seeded = CreateDefaultProfiles();
            await SaveProfilesAsync(seeded, cancellationToken);
            await SetActiveProfileAsync(seeded[0].Id, cancellationToken);
            return seeded;
        }

        await using var stream = File.OpenRead(ProfilesPath);
        var profiles = await JsonSerializer.DeserializeAsync<List<UserProfile>>(stream, cancellationToken: cancellationToken) ?? [];

        if (profiles.Count == 0)
        {
            profiles = CreateDefaultProfiles();
            await SaveProfilesAsync(profiles, cancellationToken);
        }

        var active = await GetActiveProfileIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(active) || profiles.All(x => !string.Equals(x.Id, active, StringComparison.OrdinalIgnoreCase)))
            await SetActiveProfileAsync(profiles[0].Id, cancellationToken);

        return profiles.OrderBy(x => x.DisplayName).ToList();
    }

    public async Task<UserProfile> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);
        var activeId = await GetActiveProfileIdAsync(cancellationToken);
        return profiles.FirstOrDefault(x => string.Equals(x.Id, activeId, StringComparison.OrdinalIgnoreCase)) ?? profiles[0];
    }

    public async Task UpsertProfileAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);
        var existingIndex = profiles.FindIndex(x => string.Equals(x.Id, profile.Id, StringComparison.OrdinalIgnoreCase));

        profile.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? "New User" : profile.DisplayName.Trim();
        profile.ManagedProductManager = profile.ManagedProductManager?.Trim() ?? string.Empty;
        profile.Settings ??= new UserWidgetSettings();

        if (existingIndex >= 0)
            profiles[existingIndex] = profile;
        else
            profiles.Add(profile);

        await SaveProfilesAsync(profiles, cancellationToken);

        var activeId = await GetActiveProfileIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(activeId))
            await SetActiveProfileAsync(profile.Id, cancellationToken);
    }

    public async Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var profiles = await LoadProfilesAsync(cancellationToken);
        if (profiles.Count <= 1)
            throw new InvalidOperationException("At least one user profile must remain.");

        var remaining = profiles.Where(x => !string.Equals(x.Id, profileId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == profiles.Count)
            return;

        await SaveProfilesAsync(remaining, cancellationToken);

        var activeId = await GetActiveProfileIdAsync(cancellationToken);
        if (string.Equals(activeId, profileId, StringComparison.OrdinalIgnoreCase))
            await SetActiveProfileAsync(remaining[0].Id, cancellationToken);
    }

    public async Task SetActiveProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StoreDirectory);
        await File.WriteAllTextAsync(ActiveProfilePath, profileId, cancellationToken);
    }

    private static async Task<string> GetActiveProfileIdAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(ActiveProfilePath))
            return string.Empty;

        return (await File.ReadAllTextAsync(ActiveProfilePath, cancellationToken)).Trim();
    }

    private static async Task SaveProfilesAsync(List<UserProfile> profiles, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(StoreDirectory);
        await using var stream = File.Create(ProfilesPath);
        await JsonSerializer.SerializeAsync(stream, profiles.OrderBy(x => x.DisplayName), new JsonSerializerOptions
        {
            WriteIndented = true
        }, cancellationToken);
    }

    private static List<UserProfile> CreateDefaultProfiles()
    {
        var displayName = Environment.UserName;
        return
        [
            new UserProfile
            {
                Id = Guid.NewGuid().ToString("n"),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Admin User" : displayName,
                Role = UserRole.Admin,
                Settings = new UserWidgetSettings()
            }
        ];
    }
}
