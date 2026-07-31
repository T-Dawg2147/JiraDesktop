namespace JiraDesktop.Core.Interfaces;

/// <summary>
/// Manages OAuth 2.0 authentication with the Atlassian identity platform,
/// including token acquisition, refresh, and cloud-site resolution.
/// </summary>
public interface IJiraOAuthService
{
    /// <summary>
    /// Ensures the user is authenticated, performing an interactive browser login
    /// or silent token refresh as needed.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureLoggedInAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a valid (non-expired) OAuth access token, refreshing silently if necessary.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A bearer access token string.</returns>
    Task<string> GetValidAccessTokenAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolves and caches the Atlassian Cloud ID (site identifier) for the configured Jira site URL.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Atlassian Cloud ID string.</returns>
    Task<string> GetCloudIdAsync(CancellationToken ct = default);
}
