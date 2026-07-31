using System.Text.Json;
using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Infrastructure.Services;

public class UserSettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiraDashboardApp");

    private static readonly string FilePath = Path.Combine(Dir, "settings.user.json");

    public async Task<UserWidgetSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
            return new UserWidgetSettings();

        await using var fs = File.OpenRead(FilePath);
        var settings = await JsonSerializer.DeserializeAsync<UserWidgetSettings>(fs, cancellationToken: ct);
        return settings ?? new UserWidgetSettings();
    }

    public async Task SaveAsync(UserWidgetSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Dir);
        await using var fs = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(fs, settings, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}