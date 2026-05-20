using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Computes dashboard summary metrics from live in-memory state.
/// </summary>
public class DashboardAggregatorService
{
    private readonly WorkloadStateService _workloads;
    private readonly ClusterQueueService _clusterQueues;
    private readonly LocalQueueStateService _localQueues;

    public DashboardAggregatorService(
        WorkloadStateService workloads,
        ClusterQueueService clusterQueues,
        LocalQueueStateService localQueues)
    {
        _workloads = workloads;
        _clusterQueues = clusterQueues;
        _localQueues = localQueues;
    }

    public DashboardSummaryDto GetSummary()
    {
        var workloads = _workloads.GetAll();

        return new DashboardSummaryDto
        {
            TotalClusterQueues = _clusterQueues.GetAll().Count,
            TotalLocalQueues = _localQueues.GetAll().Count,
            ActiveWorkloads = workloads.Count(w => w.Status == "Running"),
            PendingWorkloads = workloads.Count(w => w.Status == "Pending"),
            FailedWorkloads = workloads.Count(w => w.Status == "Failed"),
            CompletedWorkloads = workloads.Count(w => w.Status == "Completed"),
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}
