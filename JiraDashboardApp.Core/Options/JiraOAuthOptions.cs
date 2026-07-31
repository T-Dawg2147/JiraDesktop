namespace JiraDashboardApp.Core.Options;

public sealed class JiraOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://127.0.0.1:51234/callback";
    public string Scopes { get; set; } = "read:jira read:jira-user offline_access";
    public string AuthUrl { get; set; } = "https://auth.atlassian.com/authorize";
    public string TokenUrl { get; set; } = "https://auth.atlassian.com/oauth/token";
    public string Audience { get; set; } = "api.atlassian.com";
    public string? SiteUrl { get; set; } // https://slingsby.atlassian.net
}