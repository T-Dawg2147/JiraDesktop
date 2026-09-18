using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraDesktop.Core.Configuration;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Models;
using Microsoft.Extensions.Options;

namespace JiraDesktop.Core.Services;

public sealed class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;
    private readonly IJiraOAuthService _oauth;
    private const string JiraApiBase = "https://api.atlassian.com/ex/jira/{0}/rest/api/3";

    public JiraService(HttpClient httpClient, IOptions<JiraOptions> options, IJiraOAuthService oauth)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _oauth = oauth;
    }

    public async Task<List<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);
        var allWorkItems = new List<WorkItem>();
        var startAt = 0;
        var pageSize = Math.Clamp(_options.MaxResults, 1, 100);

        while (true)
        {
            var fieldsCsv = string.Join(",",
                "summary",
                "assignee",
                "priority",
                "status",
                "duedate",
                "updated",
                _options.ProductManagerFieldId);

            var url =
                $"{string.Format(JiraApiBase, cloudId)}/search/jql" +
                $"?jql={Uri.EscapeDataString(BuildJql())}" +
                $"&startAt={startAt}" +
                $"&maxResults={pageSize}" +
                $"&fields={Uri.EscapeDataString(fieldsCsv)}";

            using var req = CreateRequest(HttpMethod.Get, url, accessToken);
            using var response = await _httpClient.SendAsync(req, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Jira search failed {(int)response.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("issues", out var issuesEl) || issuesEl.ValueKind != JsonValueKind.Array)
                break;

            var pageCount = 0;
            foreach (var issue in issuesEl.EnumerateArray())
            {
                pageCount++;
                allWorkItems.Add(MapIssue(issue));
            }

            var total = root.TryGetProperty("total", out var totalEl) ? totalEl.GetInt32() : 0;
            startAt += pageCount;

            if (pageCount == 0 || startAt >= total)
                break;
        }

        return allWorkItems;
    }

    public async Task<List<string>> GetAllProductManagerOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ProductManagerFieldId) || _options.ProductManagerFieldContextId <= 0)
            return [];

        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);
        var options = new List<string>();
        var startAt = 0;
        const int pageSize = 100;

        while (true)
        {
            var url =
                $"{string.Format(JiraApiBase, cloudId)}/field/{_options.ProductManagerFieldId}/context/{_options.ProductManagerFieldContextId}/option" +
                $"?startAt={startAt}&maxResults={pageSize}";

            using var req = CreateRequest(HttpMethod.Get, url, accessToken);
            using var response = await _httpClient.SendAsync(req, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Product manager option load failed {(int)response.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("values", out var valuesEl) || valuesEl.ValueKind != JsonValueKind.Array)
                break;

            var count = 0;
            foreach (var valueEl in valuesEl.EnumerateArray())
            {
                count++;
                if (!valueEl.TryGetProperty("value", out var optionEl))
                    continue;

                var value = optionEl.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    options.Add(value.Trim());
            }

            var total = root.TryGetProperty("total", out var totalEl) ? totalEl.GetInt32() : options.Count;
            startAt += count;
            if (count == 0 || startAt >= total)
                break;
        }

        return options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<List<JiraStatusTransition>> GetAvailableTransitionsAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
            return [];

        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);
        var url = $"{string.Format(JiraApiBase, cloudId)}/issue/{Uri.EscapeDataString(issueKey)}/transitions";

        using var req = CreateRequest(HttpMethod.Get, url, accessToken);
        using var response = await _httpClient.SendAsync(req, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Transition load failed {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("transitions", out var transitionsEl) || transitionsEl.ValueKind != JsonValueKind.Array)
            return [];

        return transitionsEl.EnumerateArray()
            .Select(MapTransition)
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task UpdateWorkItemStatusAsync(string issueKey, string transitionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
            throw new ArgumentException("Issue key is required.", nameof(issueKey));
        if (string.IsNullOrWhiteSpace(transitionId))
            throw new ArgumentException("Transition id is required.", nameof(transitionId));

        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);
        var url = $"{string.Format(JiraApiBase, cloudId)}/issue/{Uri.EscapeDataString(issueKey)}/transitions";

        using var req = CreateRequest(HttpMethod.Post, url, accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            transition = new { id = transitionId }
        }), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(req, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Status update failed {(int)response.StatusCode}: {raw}");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.ParseAdd("application/json");
        return req;
    }

    private WorkItem MapIssue(JsonElement issue)
    {
        var key = issue.GetProperty("key").GetString() ?? string.Empty;
        var fields = issue.GetProperty("fields");

        var summary = fields.TryGetProperty("summary", out var summaryEl)
            ? summaryEl.GetString() ?? string.Empty
            : string.Empty;

        var assigneeName = "Unassigned";
        if (fields.TryGetProperty("assignee", out var assigneeEl) && assigneeEl.ValueKind != JsonValueKind.Null)
            assigneeName = assigneeEl.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "Unassigned" : "Unassigned";

        var priority = fields.TryGetProperty("priority", out var priorityEl) && priorityEl.ValueKind != JsonValueKind.Null
            ? (priorityEl.TryGetProperty("name", out var pn) ? pn.GetString() ?? "-" : "-")
            : "-";

        var status = fields.TryGetProperty("status", out var statusEl) && statusEl.ValueKind != JsonValueKind.Null
            ? (statusEl.TryGetProperty("name", out var sn) ? sn.GetString() ?? "-" : "-")
            : "-";

        DateTime? dueDate = null;
        if (fields.TryGetProperty("duedate", out var dueEl) && dueEl.ValueKind == JsonValueKind.String && DateTime.TryParse(dueEl.GetString(), out var dueParsed))
            dueDate = dueParsed;

        var updated = DateTime.UtcNow;
        if (fields.TryGetProperty("updated", out var updatedEl) && updatedEl.ValueKind == JsonValueKind.String && DateTime.TryParse(updatedEl.GetString(), out var updatedParsed))
            updated = updatedParsed.ToUniversalTime();

        return new WorkItem
        {
            Key = key,
            Summary = summary,
            Assignee = assigneeName,
            Priority = priority,
            Status = status,
            DueDate = dueDate,
            Updated = updated,
            ProductManager = ExtractProductManager(fields, _options.ProductManagerFieldId),
            Url = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? string.Empty
                : $"{_options.BaseUrl.TrimEnd('/')}/browse/{key}"
        };
    }

    private static JiraStatusTransition MapTransition(JsonElement transition)
    {
        var name = transition.TryGetProperty("to", out var toEl) && toEl.ValueKind == JsonValueKind.Object && toEl.TryGetProperty("name", out var toNameEl)
            ? toNameEl.GetString() ?? string.Empty
            : transition.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? string.Empty
                : string.Empty;

        return new JiraStatusTransition
        {
            Id = transition.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
            Name = name
        };
    }

    private string BuildJql()
        => $"project = \"{EscapeJql(_options.ProjectKey)}\" ORDER BY updated DESC";

    private static string EscapeJql(string value) => (value ?? string.Empty).Replace("\"", "\\\"");

    private static string ExtractProductManager(JsonElement fields, string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId) || !fields.TryGetProperty(fieldId, out var pmEl) || pmEl.ValueKind == JsonValueKind.Null)
            return string.Empty;

        if (pmEl.ValueKind == JsonValueKind.String)
            return pmEl.GetString() ?? string.Empty;

        if (pmEl.ValueKind == JsonValueKind.Object && pmEl.TryGetProperty("value", out var valueEl))
            return valueEl.GetString() ?? string.Empty;

        if (pmEl.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var values = new List<string>();
        foreach (var item in pmEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                values.Add(item.GetString() ?? string.Empty);
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var nestedValueEl))
                values.Add(nestedValueEl.GetString() ?? string.Empty);
        }

        return string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
