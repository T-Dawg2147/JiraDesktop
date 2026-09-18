using JiraDesktop.Core.Models;

namespace JiraDesktop.Core.Interfaces;

public interface IJiraService
{
    Task<List<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetAllProductManagerOptionsAsync(CancellationToken cancellationToken = default);
    Task<List<JiraStatusTransition>> GetAvailableTransitionsAsync(string issueKey, CancellationToken cancellationToken = default);
    Task UpdateWorkItemStatusAsync(string issueKey, string transitionId, CancellationToken cancellationToken = default);
}
