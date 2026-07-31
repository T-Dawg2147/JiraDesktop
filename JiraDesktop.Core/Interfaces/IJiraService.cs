using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Interfaces;

/// <summary>
/// Provides access to Jira work items and related metadata via the Jira REST API.
/// </summary>
public interface IJiraService
{
    /// <summary>
    /// Retrieves work items from Jira, optionally filtered by product manager and/or assignee.
    /// </summary>
    /// <param name="productManager">Product manager filter value, or <c>null</c>/"All" to include all.</param>
    /// <param name="assignee">Assignee display-name filter, or <c>null</c>/"All" to include all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="WorkItem"/> objects matching the filters.</returns>
    Task<List<WorkItem>> GetWorkItemsAsync(string? productManager, string? assignee, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all valid option values for the Product Manager custom field from Jira.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A sorted, distinct list of product manager names.</returns>
    Task<List<string>> GetAllProductManagerOptionsAsync(CancellationToken cancellationToken = default);
}
