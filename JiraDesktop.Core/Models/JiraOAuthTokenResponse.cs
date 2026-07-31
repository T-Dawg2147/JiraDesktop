namespace JiraDesktop.Core.Models;

/// <summary>
/// Represents the OAuth 2.0 token set returned by the Atlassian token endpoint,
/// augmented with a computed expiry timestamp.
/// </summary>
public sealed class JiraOAuthTokenResponse
{
    /// <summary>The short-lived bearer access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The long-lived refresh token used to obtain new access tokens without re-login.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Token lifetime in seconds as returned by the server.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>UTC timestamp at which the access token expires (computed on receipt).</summary>
    public DateTime ExpiresAt { get; set; }
}
