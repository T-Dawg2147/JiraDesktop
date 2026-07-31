namespace JiraDashboardApp.Core.Interfaces;

public interface IJiraOAuthService
{
    Task<string> GetValidAccessTokenAsync(CancellationToken ct = default);
    Task<string> GetCloudIdAsync(CancellationToken ct = default);
    Task EnsureLoggedInAsync(CancellationToken ct = default);
}