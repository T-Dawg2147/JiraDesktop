using System.Net.Http.Headers;
using System.Text.Json;
using JiraDashboardApp.Core.Interfaces;
using JiraDashboardApp.Core.Models;
using JiraDashboardApp.Core.Options;
using Microsoft.Extensions.Options;

namespace JiraDashboardApp.Infrastructure.Services;

public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;
    private readonly IJiraOAuthService _oauth;

    public JiraService(HttpClient httpClient, IOptions<JiraOptions> options, IJiraOAuthService oauth)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _oauth = oauth;
    }

    public async Task<List<WorkItem>> GetWorkItemsAsync(string? productManager, string? assignee, CancellationToken cancellationToken = default)
    {
        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);

        var jql = BuildJql(productManager, assignee);

        var allWorkItems = new List<WorkItem>();
        var startAt = 0;
        const int pageSize = 100; // Jira page size

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

            // NEW endpoint: /search/jql
            var url =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/search/jql" +
                $"?jql={Uri.EscapeDataString(jql)}" +
                $"&startAt={startAt}" +
                $"&maxResults={pageSize}" +
                $"&fields={Uri.EscapeDataString(fieldsCsv)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(req, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Jira search/jql failed {(int)response.StatusCode}: {raw}");

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
            return new List<string>();

        var accessToken = await _oauth.GetValidAccessTokenAsync(cancellationToken);
        var cloudId = await _oauth.GetCloudIdAsync(cancellationToken);

        var options = new List<string>();
        var startAt = 0;
        const int pageSize = 100;

        while (true)
        {
            var url =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/field/{_options.ProductManagerFieldId}/context/{_options.ProductManagerFieldContextId}/option" +
                $"?startAt={startAt}&maxResults={pageSize}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.ParseAdd("application/json");

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"PM options load failed {(int)resp.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("values", out var valuesEl) || valuesEl.ValueKind != JsonValueKind.Array)
                break;

            var count = 0;
            foreach (var v in valuesEl.EnumerateArray())
            {
                count++;
                if (v.TryGetProperty("value", out var valueEl))
                {
                    var value = valueEl.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        options.Add(value.Trim());
                }
            }

            var total = root.TryGetProperty("total", out var totalEl) ? totalEl.GetInt32() : options.Count;
            startAt += count;

            if (count == 0 || startAt >= total)
                break;
        }

        return options.Distinct().OrderBy(x => x).ToList();
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
        if (fields.TryGetProperty("duedate", out var dueEl) &&
            dueEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(dueEl.GetString(), out var dueParsed))
        {
            dueDate = dueParsed;
        }

        var updated = DateTime.UtcNow;
        if (fields.TryGetProperty("updated", out var updEl) &&
            updEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(updEl.GetString(), out var updParsed))
        {
            updated = updParsed.ToUniversalTime();
        }

        var pm = ExtractProductManager(fields, _options.ProductManagerFieldId);

        return new WorkItem
        {
            Key = key,
            Summary = summary,
            Assignee = assigneeName,
            Priority = priority,
            Status = status,
            DueDate = dueDate,
            Updated = updated,
            ProductManager = pm,
            Url = $"{_options.BaseUrl.TrimEnd('/')}/browse/{key}"
        };
    }

    private string BuildJql(string? productManager, string? assignee)
    {
        var clauses = new List<string> { $"project = \"{_options.ProjectKey}\"" };

        if (!string.IsNullOrWhiteSpace(productManager) && productManager != "All")
            clauses.Add($"\"Product Managers\" = \"{EscapeJql(productManager)}\"");

        if (!string.IsNullOrWhiteSpace(assignee) && assignee != "All")
            clauses.Add($"assignee = \"{EscapeJql(assignee)}\"");

        // all statuses by default
        return string.Join(" AND ", clauses) + " ORDER BY created DESC";
    }

    private static string EscapeJql(string value) => value.Replace("\"", "\\\"");

    private static string ExtractProductManager(JsonElement fields, string fieldId)
    {
        if (!fields.TryGetProperty(fieldId, out var pmEl) || pmEl.ValueKind == JsonValueKind.Null)
            return string.Empty;

        if (pmEl.ValueKind == JsonValueKind.String)
            return pmEl.GetString() ?? string.Empty;

        if (pmEl.ValueKind == JsonValueKind.Object && pmEl.TryGetProperty("value", out var valueEl))
            return valueEl.GetString() ?? string.Empty;

        if (pmEl.ValueKind == JsonValueKind.Array)
        {
            var names = new List<string>();
            foreach (var item in pmEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    names.Add(item.GetString() ?? string.Empty);
                else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var v))
                    names.Add(v.GetString() ?? string.Empty);
            }
            return string.Join(", ", names.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return string.Empty;
    }
}