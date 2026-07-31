namespace JiraDesktop.Core.Models;

/// <summary>
/// Represents an Atlassian Cloud site returned from the accessible-resources endpoint.
/// </summary>
public sealed class JiraAccessibleResource
{
    /// <summary>The unique Atlassian Cloud ID for this site.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The base URL of the Jira site (e.g. "https://example.atlassian.net").</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The display name of the Jira site.</summary>
    public string Name { get; set; } = string.Empty;
}
