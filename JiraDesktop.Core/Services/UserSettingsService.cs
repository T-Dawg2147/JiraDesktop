using System.Text.Json;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Services;

/// <summary>
/// Loads and saves <see cref="UserWidgetSettings"/> as an indented JSON file
/// in the user's local application data folder, so preferences persist across sessions.
/// </summary>
public class UserSettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiraDesktop");

    private static readonly string FilePath = Path.Combine(Dir, "settings.user.json");

    /// <summary>
    /// Loads user settings from disk, or returns a default <see cref="UserWidgetSettings"/> instance
    /// if no saved file exists.
    /// </summary>
    public async Task<UserWidgetSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
            return new UserWidgetSettings();

        await using var fs = File.OpenRead(FilePath);
        var settings = await JsonSerializer.DeserializeAsync<UserWidgetSettings>(fs, cancellationToken: ct);
        return settings ?? new UserWidgetSettings();
    }

    /// <summary>
    /// Persists the supplied settings to disk as indented JSON.
    /// </summary>
    public async Task SaveAsync(UserWidgetSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Dir);
        await using var fs = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(fs, settings, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}
