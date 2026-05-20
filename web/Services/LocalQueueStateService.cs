using k8s;
using System.Collections.Concurrent;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Holds the latest in-memory snapshot of LocalQueues.
/// Updated by LocalQueueWatchService; read by controllers.
/// </summary>
public class LocalQueueStateService
{
    private readonly IKubernetes _client;
    private readonly ILogger<LocalQueueStateService> _logger;

    // Key = "namespace/name"
    private readonly ConcurrentDictionary<string, LocalQueueDto> _state = new();

    public LocalQueueStateService(IKubernetes client, ILogger<LocalQueueStateService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public IReadOnlyCollection<LocalQueueDto> GetAll() => _state.Values.ToList();

    public void Clear() => _state.Clear();

    public IReadOnlyCollection<LocalQueueDto> GetByNamespace(string ns) =>
        _state.Values.Where(q => q.Namespace == ns).ToList();

    public void Apply(LocalQueueDto dto) => _state[$"{dto.Namespace}/{dto.Name}"] = dto;

    public void Remove(string ns, string name) => _state.TryRemove($"{ns}/{name}", out _);

    public async Task LoadInitialAsync()
    {
        try
        {
            var response = await _client.CustomObjects.ListClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "localqueues");

            var json = JsonSerializer.Serialize(response);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items)) return;

            foreach (var item in items.EnumerateArray())
                Apply(ParseItem(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial LocalQueues");
        }
    }

    internal static LocalQueueDto ParseItem(JsonElement item)
    {
        var metadata = item.GetProperty("metadata");
        var name = KubeHelpers.GetString(metadata, "name");
        var ns = KubeHelpers.GetString(metadata, "namespace");
        var age = KubeHelpers.ParseAge(metadata);

        string clusterQueue = "";
        if (item.TryGetProperty("spec", out var spec))
            clusterQueue = KubeHelpers.GetString(spec, "clusterQueue");

        int pendingWorkloads = 0;
        int admittedWorkloads = 0;
        string status = "Unknown";

        if (item.TryGetProperty("status", out var st))
        {
            pendingWorkloads = KubeHelpers.GetInt(st, "pendingWorkloads");
            admittedWorkloads = KubeHelpers.GetInt(st, "admittedWorkloads");

            if (st.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in conds.EnumerateArray())
                {
                    if (KubeHelpers.GetString(c, "type") == "Active"
                        && KubeHelpers.GetString(c, "status") == "True")
                        status = "Active";
                }
            }
        }

        return new LocalQueueDto
        {
            Name = name,
            Namespace = ns,
            ClusterQueue = clusterQueue,
            PendingWorkloads = pendingWorkloads,
            AdmittedWorkloads = admittedWorkloads,
            Status = status,
            Age = age
        };
    }
}
