using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class JobDiagnosticsServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkloadStateService MakeWorkloadStore(params WorkloadDto[] items)
    {
        var store = new WorkloadStateService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    private static LocalQueueStateService MakeLocalQueueStore(params LocalQueueDto[] items)
    {
        var store = new LocalQueueStateService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    private static ClusterQueueService MakeClusterQueueStore(params ClusterQueueDto[] items)
    {
        var store = new ClusterQueueService(null!, null!);
        foreach (var item in items) store.Apply(item);
        return store;
    }

    private static JobDiagnosticsService MakeService(
        WorkloadStateService workloads,
        LocalQueueStateService? localQueues = null,
        ClusterQueueService? clusterQueues = null)
    {
        return new JobDiagnosticsService(
            workloads,
            localQueues ?? MakeLocalQueueStore(),
            clusterQueues ?? MakeClusterQueueStore());
    }

    // ── BuildDiagnostics — not found ──────────────────────────────────────────

    [Fact]
    public void BuildDiagnostics_UnknownWorkload_ReturnsNull()
    {
        var svc = MakeService(MakeWorkloadStore());
        Assert.Null(svc.BuildDiagnostics("demo", "does-not-exist"));
    }

    // ── BuildDiagnostics — Running ────────────────────────────────────────────

    [Fact]
    public void BuildDiagnostics_RunningWorkload_ReturnsDiagnosticWithRunningText()
    {
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "wl-run", Namespace = "demo", Queue = "user-queue",
                ClusterQueue = "cluster-queue", Status = "Running",
                Admitted = true, Finished = false,
            }));

        var result = svc.BuildDiagnostics("demo", "wl-run");

        Assert.NotNull(result);
        Assert.Equal("Running", result!.Status);
        Assert.Contains("Running", result.DiagnosticSummary);
        Assert.Contains("cluster-queue", result.DiagnosticSummary);
        Assert.False(result.JobSuspended);
        Assert.Equal("Running", result.JobPhase);
    }

    // ── BuildDiagnostics — Completed ──────────────────────────────────────────

    [Fact]
    public void BuildDiagnostics_CompletedWorkload_ReturnsDiagnosticCompleted()
    {
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "wl-done", Namespace = "demo", Status = "Completed",
                Admitted = true, Finished = true,
            }));

        var result = svc.BuildDiagnostics("demo", "wl-done");

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        Assert.Equal("Complete", result.JobPhase);
        Assert.Contains("Completed", result.DiagnosticSummary);
        Assert.Contains("successfully", result.DiagnosticSummary);
    }

    // ── BuildDiagnostics — Failed ─────────────────────────────────────────────

    [Fact]
    public void BuildDiagnostics_FailedWorkload_ReturnsDiagnosticFailed()
    {
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "wl-fail", Namespace = "demo", Status = "Failed",
                Message = "Pod exited with code 1",
                Admitted = true, Finished = true,
                Conditions =
                [
                    new WorkloadCondition { Type = "Finished", ConditionStatus = "True",
                        Reason = "Failed", Message = "Pod exited with code 1" },
                ],
            }));

        var result = svc.BuildDiagnostics("demo", "wl-fail");

        Assert.NotNull(result);
        Assert.Equal("Failed", result!.Status);
        Assert.Equal("Failed", result.JobPhase);
        Assert.Contains("Failed", result.DiagnosticSummary);
        Assert.Contains("Pod exited with code 1", result.DiagnosticSummary);
    }

    // ── BuildDiagnostics — Pending (no conditions) ───────────────────────────

    [Fact]
    public void BuildDiagnostics_PendingNoConditions_ReturnsNotEvaluatedMessage()
    {
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto { Name = "wl-new", Namespace = "default", Status = "Pending" }));

        var result = svc.BuildDiagnostics("default", "wl-new");

        Assert.NotNull(result);
        Assert.True(result!.JobSuspended);
        Assert.Equal("Suspended", result.JobPhase);
        Assert.Contains("not been evaluated", result.DiagnosticSummary);
    }

    // ── BuildDiagnostics — Pending: missing LocalQueue ────────────────────────

    [Fact]
    public void BuildDiagnostics_PendingMissingLocalQueue_ReturnsLocalQueueExplanation()
    {
        // This mirrors the real bug: job in `default` namespace, queue `user-queue`
        // which only exists in `demo` → Kueue message "LocalQueue user-queue doesn't exist"
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "job-mjob-1-abc", Namespace = "default",
                Queue = "user-queue", Status = "Pending",
                Message = "LocalQueue user-queue doesn't exist in namespace default",
                Conditions =
                [
                    new WorkloadCondition
                    {
                        Type = "Admitted", ConditionStatus = "False",
                        Reason = "Inadmissible",
                        Message = "LocalQueue user-queue doesn't exist in namespace default",
                    },
                ],
            }));

        var result = svc.BuildDiagnostics("default", "job-mjob-1-abc");

        Assert.NotNull(result);
        Assert.Equal("Pending", result!.Status);
        Assert.Contains("Pending because", result.DiagnosticSummary);
        Assert.Contains("default", result.DiagnosticSummary);  // namespace mentioned
        Assert.Contains("LocalQueue", result.DiagnosticSummary);
    }

    // ── BuildDiagnostics — Pending: quota exceeded ───────────────────────────

    [Fact]
    public void BuildDiagnostics_PendingQuotaExceeded_ReturnsQuotaMessage()
    {
        var svc = MakeService(MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "wl-big", Namespace = "demo", Status = "Pending",
                Message = "insufficient quota for cpu",
                Conditions =
                [
                    new WorkloadCondition
                    {
                        Type = "Admitted", ConditionStatus = "False",
                        Reason = "Inadmissible",
                        Message = "insufficient quota for cpu",
                    },
                ],
            }));

        var result = svc.BuildDiagnostics("demo", "wl-big");

        Assert.NotNull(result);
        Assert.Contains("quota", result!.DiagnosticSummary.ToLowerInvariant());
    }

    // ── BuildDiagnostics — queue context enrichment ───────────────────────────

    [Fact]
    public void BuildDiagnostics_EnrichesLocalAndClusterQueueStatus()
    {
        var workloads = MakeWorkloadStore(
            new WorkloadDto
            {
                Name = "wl-q", Namespace = "demo", Queue = "user-queue",
                ClusterQueue = "cluster-queue", Status = "Running",
            });

        var localQueues = MakeLocalQueueStore(
            new LocalQueueDto { Name = "user-queue", Namespace = "demo",
                Status = "Active", ClusterQueue = "cluster-queue" });

        var clusterQueues = MakeClusterQueueStore(
            new ClusterQueueDto { Name = "cluster-queue", Status = "Active" });

        var svc = new JobDiagnosticsService(workloads, localQueues, clusterQueues);
        var result = svc.BuildDiagnostics("demo", "wl-q");

        Assert.NotNull(result);
        Assert.Equal("Active", result!.LocalQueueStatus);
        Assert.Equal("cluster-queue", result.ClusterQueueName);
        Assert.Equal("Active", result.ClusterQueueStatus);
    }

    // ── TranslatePendingMessage unit tests ────────────────────────────────────

    [Theory]
    [InlineData("LocalQueue user-queue doesn't exist", "Pending because")]
    [InlineData("LocalQueue user-queue does not exist in namespace demo", "Pending because")]
    [InlineData("insufficient quota for memory", "quota")]
    [InlineData("ClusterQueue is inactive", "inactive")]
    [InlineData("some other reason", "Pending:")]
    public void TranslatePendingMessage_VariousMessages_ReturnsExpectedPrefix(
        string message, string expectedFragment)
    {
        var result = JobDiagnosticsService.TranslatePendingMessage(message, "demo");
        Assert.Contains(expectedFragment, result, StringComparison.OrdinalIgnoreCase);
    }
}
