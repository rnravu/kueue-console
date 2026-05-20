using System.Text.Json;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class WorkloadStateServiceTests
{
    [Fact]
    public void ParseItem_PendingWorkload_ReturnsStatusPending()
    {
        var json = """
        {
          "metadata": { "name": "wl-1", "namespace": "team-a", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "local-q" },
          "status": {}
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("wl-1", dto.Name);
        Assert.Equal("team-a", dto.Namespace);
        Assert.Equal("local-q", dto.Queue);
        Assert.Equal("Pending", dto.Status);
    }

    [Fact]
    public void ParseItem_AdmittedWorkload_ReturnsStatusRunning()
    {
        var json = """
        {
          "metadata": { "name": "wl-2", "namespace": "team-a", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "local-q" },
          "status": {
            "conditions": [
              { "type": "Admitted", "status": "True", "message": "Admitted by scheduler" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Running", dto.Status);
    }

    [Fact]
    public void ParseItem_FinishedWithFailedReason_ReturnsStatusFailed()
    {
        var json = """
        {
          "metadata": { "name": "wl-3", "namespace": "team-a", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "local-q" },
          "status": {
            "conditions": [
              { "type": "Admitted", "status": "True", "message": "ok" },
              { "type": "Finished", "status": "True", "reason": "Failed", "message": "Pod failed" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Failed", dto.Status);
        Assert.Equal("Pod failed", dto.Message);
    }

    [Fact]
    public void ParseItem_FinishedSuccessfully_ReturnsStatusCompleted()
    {
        var json = """
        {
          "metadata": { "name": "wl-4", "namespace": "team-a", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "local-q" },
          "status": {
            "conditions": [
              { "type": "Admitted", "status": "True", "message": "ok" },
              { "type": "Finished", "status": "True", "reason": "Succeeded", "message": "All pods done" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Completed", dto.Status);
    }

    [Fact]
    public void InMemoryState_ApplyAndRemove_WorksCorrectly()
    {
        var store = new WorkloadStateService(null!, null!);

        var dto = new WorkloadDto { Name = "wl-1", Namespace = "ns", Status = "Running" };
        store.Apply(dto);

        Assert.Single(store.GetAll());
        Assert.Equal("Running", store.GetAll().First().Status);

        store.Remove("ns", "wl-1");
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void GetByNamespace_FiltersCorrectly()
    {
        var store = new WorkloadStateService(null!, null!);
        store.Apply(new WorkloadDto { Name = "w1", Namespace = "ns-a", Status = "Running" });
        store.Apply(new WorkloadDto { Name = "w2", Namespace = "ns-b", Status = "Pending" });

        var result = store.GetByNamespace("ns-a");
        Assert.Single(result);
        Assert.Equal("w1", result.First().Name);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var store = new WorkloadStateService(null!, null!);
        store.Apply(new WorkloadDto { Name = "w1", Namespace = "ns", Status = "Running" });
        store.Apply(new WorkloadDto { Name = "w2", Namespace = "ns", Status = "Pending" });

        store.Clear();

        Assert.Empty(store.GetAll());
    }

    // Real Kueue uses reason "JobFinished" (not "Succeeded") when a batch/v1 Job completes.
    // This test guards against a regression where only "Succeeded" was recognised.
    [Fact]
    public void ParseItem_FinishedWithJobFinishedReason_ReturnsStatusCompleted()
    {
        var json = """
        {
          "metadata": { "name": "wl-5", "namespace": "demo", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "user-queue" },
          "status": {
            "conditions": [
              { "type": "Admitted", "status": "True", "message": "ok" },
              { "type": "Finished", "status": "True", "reason": "JobFinished", "message": "Job finished successfully" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Completed", dto.Status);
    }

    // When both Admitted=True and Finished=True are present, Finished must take
    // precedence so completed workloads are never misclassified as Running.
    [Fact]
    public void ParseItem_FinishedTrueAndAdmittedTrue_PrioritisesFinished()
    {
        var json = """
        {
          "metadata": { "name": "wl-6", "namespace": "demo", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "user-queue" },
          "status": {
            "conditions": [
              { "type": "Admitted", "status": "True", "message": "ok" },
              { "type": "Finished", "status": "True", "reason": "JobFinished", "message": "done" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        // Must be Completed, not Running
        Assert.Equal("Completed", dto.Status);
    }

    // ── Real-world Kueue structure: status.admission is a JSON OBJECT ────────
    // In real clusters, admitted/running/completed workloads have
    // status.admission = { clusterQueue: "...", podSetAssignments: [...] }.
    // Previously, KubeHelpers.GetString called v.GetString() on that Object element,
    // throwing InvalidOperationException. The per-item try/catch would catch it and
    // silently skip ALL admitted workloads — showing Pending=1, Running=0, Completed=0.
    // These tests are the regression guard.

    [Fact]
    public void ParseItem_RunningWithRealWorldAdmissionObject_ReturnsStatusRunning()
    {
        var json = """
        {
          "metadata": { "name": "wl-real-run", "namespace": "demo", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "user-queue" },
          "status": {
            "admission": {
              "clusterQueue": "cluster-queue",
              "podSetAssignments": [{ "name": "main", "count": 1 }]
            },
            "conditions": [
              { "type": "Admitted", "status": "True", "reason": "ByClusterQueue", "message": "Admitted by clusterQueue cluster-queue" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Running", dto.Status);
        Assert.Equal("cluster-queue", dto.ClusterQueue);
        Assert.Equal("user-queue", dto.Queue);
    }

    [Fact]
    public void ParseItem_CompletedWithRealWorldAdmissionObject_ReturnsStatusCompleted()
    {
        // Mirrors the real demo/sample-job1/2/3 workload structure after completion.
        var json = """
        {
          "metadata": { "name": "job-sample-job1-abc", "namespace": "demo", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": { "queueName": "user-queue" },
          "status": {
            "admission": {
              "clusterQueue": "cluster-queue",
              "podSetAssignments": [{ "name": "main", "count": 1 }]
            },
            "conditions": [
              { "type": "Admitted", "status": "True",  "reason": "ByClusterQueue", "message": "Admitted by clusterQueue cluster-queue" },
              { "type": "Finished", "status": "True",  "reason": "JobFinished",    "message": "Job finished successfully" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = WorkloadStateService.ParseItem(doc.RootElement);

        Assert.Equal("Completed", dto.Status);
        Assert.Equal("cluster-queue", dto.ClusterQueue);
        Assert.True(dto.Finished);
        Assert.Equal(2, dto.Conditions.Count);
    }
}
