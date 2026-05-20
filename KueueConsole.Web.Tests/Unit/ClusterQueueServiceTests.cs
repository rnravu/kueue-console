using System.Text.Json;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class ClusterQueueServiceTests
{
    [Fact]
    public void ParseItem_ActiveQueue_ParsesCorrectly()
    {
        var json = """
        {
          "metadata": { "name": "cq-main", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": {
            "resourceGroups": [{
              "flavors": [{
                "resources": [
                  { "name": "cpu", "nominalQuota": "10" },
                  { "name": "memory", "nominalQuota": "40Gi" }
                ]
              }]
            }]
          },
          "status": {
            "pendingWorkloads": 3,
            "admittedWorkloads": 7,
            "conditions": [
              { "type": "Active", "status": "True" }
            ]
          }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = ClusterQueueService.ParseItem(doc.RootElement);

        Assert.Equal("cq-main", dto.Name);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(3, dto.PendingWorkloads);
        Assert.Equal(7, dto.AdmittedWorkloads);
        Assert.Equal("10", dto.NominalCpu);
        Assert.Equal("40Gi", dto.NominalMemory);
    }

    [Fact]
    public void ParseItem_MissingStatusConditions_ReturnsUnknown()
    {
        var json = """
        {
          "metadata": { "name": "cq-empty", "creationTimestamp": "2026-01-01T00:00:00Z" },
          "spec": {},
          "status": {}
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var dto = ClusterQueueService.ParseItem(doc.RootElement);

        Assert.Equal("Unknown", dto.Status);
        Assert.Equal(0, dto.PendingWorkloads);
    }

    [Fact]
    public void InMemoryState_ApplyAndRemove_WorksCorrectly()
    {
        var store = new ClusterQueueService(null!, null!);
        store.Apply(new ClusterQueueDto { Name = "cq-1", Status = "Active" });

        Assert.Single(store.GetAll());

        store.Remove("cq-1");
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var store = new ClusterQueueService(null!, null!);
        store.Apply(new ClusterQueueDto { Name = "cq-1", Status = "Active" });
        store.Apply(new ClusterQueueDto { Name = "cq-2", Status = "Unknown" });

        store.Clear();

        Assert.Empty(store.GetAll());
    }
}
