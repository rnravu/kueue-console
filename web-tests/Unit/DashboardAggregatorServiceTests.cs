using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class DashboardAggregatorServiceTests
{
    private static WorkloadStateService MakeWorkloadStore(params WorkloadDto[] items)
    {
        var store = new WorkloadStateService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    private static ClusterQueueService MakeCqStore(params ClusterQueueDto[] items)
    {
        var store = new ClusterQueueService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    private static LocalQueueStateService MakeLqStore(params LocalQueueDto[] items)
    {
        var store = new LocalQueueStateService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    [Fact]
    public void GetSummary_CountsStatusesCorrectly()
    {
        var workloads = MakeWorkloadStore(
            new WorkloadDto { Name = "a", Namespace = "ns", Status = "Running" },
            new WorkloadDto { Name = "b", Namespace = "ns", Status = "Running" },
            new WorkloadDto { Name = "c", Namespace = "ns", Status = "Pending" },
            new WorkloadDto { Name = "d", Namespace = "ns", Status = "Failed" },
            new WorkloadDto { Name = "e", Namespace = "ns", Status = "Completed" }
        );
        var cqs = MakeCqStore(
            new ClusterQueueDto { Name = "cq1" },
            new ClusterQueueDto { Name = "cq2" }
        );
        var lqs = MakeLqStore(
            new LocalQueueDto { Name = "lq1", Namespace = "ns" }
        );

        var svc = new DashboardAggregatorService(workloads, cqs, lqs);
        var summary = svc.GetSummary();

        Assert.Equal(2, summary.TotalClusterQueues);
        Assert.Equal(1, summary.TotalLocalQueues);
        Assert.Equal(2, summary.ActiveWorkloads);
        Assert.Equal(1, summary.PendingWorkloads);
        Assert.Equal(1, summary.FailedWorkloads);
        Assert.Equal(1, summary.CompletedWorkloads);
    }

    [Fact]
    public void GetSummary_EmptyStores_ReturnsZeroCounts()
    {
        var svc = new DashboardAggregatorService(
            MakeWorkloadStore(),
            MakeCqStore(),
            MakeLqStore());

        var summary = svc.GetSummary();
        Assert.Equal(0, summary.TotalClusterQueues);
        Assert.Equal(0, summary.ActiveWorkloads);
        Assert.Equal(0, summary.PendingWorkloads);
    }

    // Mirrors the real cluster state that originally showed Completed=0:
    // 1 Pending workload + 3 Completed workloads.
    [Fact]
    public void GetSummary_ThreeCompletedWorkloads_CountsAllThree()
    {
        var workloads = MakeWorkloadStore(
            new WorkloadDto { Name = "job-mjob-1",      Namespace = "default", Status = "Pending" },
            new WorkloadDto { Name = "job-sample-job1", Namespace = "demo",    Status = "Completed" },
            new WorkloadDto { Name = "job-sample-job2", Namespace = "demo",    Status = "Completed" },
            new WorkloadDto { Name = "job-sample-job3", Namespace = "demo",    Status = "Completed" }
        );

        var svc = new DashboardAggregatorService(workloads, MakeCqStore(), MakeLqStore());
        var summary = svc.GetSummary();

        Assert.Equal(3, summary.CompletedWorkloads);
        Assert.Equal(1, summary.PendingWorkloads);
        Assert.Equal(0, summary.ActiveWorkloads);
        Assert.Equal(0, summary.FailedWorkloads);
    }
}
