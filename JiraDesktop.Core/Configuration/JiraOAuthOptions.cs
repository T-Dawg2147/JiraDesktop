namespace JiraDesktop.Core.Configuration;

/// <summary>
/// OAuth 2.0 (Authorization Code + PKCE) configuration for authenticating with Atlassian.
/// Bound from the "JiraOAuth" section of <c>appsettings.json</c>.
/// </summary>
public sealed class JiraOAuthOptions
{
    /// <summary>OAuth 2.0 client ID registered in the Atlassian developer console.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret registered in the Atlassian developer console.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Local HTTP redirect URI that the Atlassian auth server posts the authorization code to.
    /// Must match the registered callback URL exactly (default: "http://127.0.0.1:51234/callback").
    /// </summary>
    public string RedirectUri { get; set; } = "http://127.0.0.1:51234/callback";

    /// <summary>Space-separated list of OAuth scopes to request.</summary>
    public string Scopes { get; set; } = "read:jira read:jira-user offline_access";

    /// <summary>Atlassian authorization endpoint URL.</summary>
    public string AuthUrl { get; set; } = "https://auth.atlassian.com/authorize";

    /// <summary>Atlassian token exchange/refresh endpoint URL.</summary>
    public string TokenUrl { get; set; } = "https://auth.atlassian.com/oauth/token";

    /// <summary>The API audience value required by Atlassian's OAuth server.</summary>
    public string Audience { get; set; } = "api.atlassian.com";

    /// <summary>
    /// The target Jira site URL (e.g. "https://example.atlassian.net").
    /// Used to select the correct cloud site from the accessible-resources list.
    /// </summary>
    public string? SiteUrl { get; set; }
}
