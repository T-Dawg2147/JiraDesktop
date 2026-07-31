namespace JiraDesktop.Core.Configuration;

/// <summary>
/// Configuration options for the Jira project and API behaviour.
/// Bound from the "Jira" section of <c>appsettings.json</c>.
/// </summary>
public class JiraOptions
{
    /// <summary>Base URL of the Jira site, e.g. "https://example.atlassian.net".</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The Jira project key used to scope all JQL queries, e.g. "PROJ".</summary>
    public string ProjectKey { get; set; } = "";

    /// <summary>
    /// The API field ID for the Product Manager custom field,
    /// e.g. "customfield_10055".
    /// </summary>
    public string ProductManagerFieldId { get; set; } = "customfield_10055";

    /// <summary>Maximum number of issues to retrieve in a single Jira API page request.</summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>The numeric Jira project ID (used for custom field context lookups).</summary>
    public long ProjectId { get; set; }

    /// <summary>The custom field context ID for the Product Manager field (used to enumerate options).</summary>
    public long ProductManagerFieldContextId { get; set; }
}
