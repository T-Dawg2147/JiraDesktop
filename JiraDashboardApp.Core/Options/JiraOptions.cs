namespace JiraDashboardApp.Core.Options;

public class JiraOptions
{
    public string BaseUrl { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public string ProductManagerFieldId { get; set; } = "customfield_10055";
    public int MaxResults { get; set; } = 100;
    
    public long ProjectId { get; set; }            // Jira numeric project ID
    public long ProductManagerFieldContextId { get; set; } // custom field context ID
}