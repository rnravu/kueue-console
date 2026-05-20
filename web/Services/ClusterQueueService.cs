using k8s;
using System.Collections.Concurrent;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Holds the latest in-memory snapshot of ClusterQueues.
/// Updated by ClusterQueueWatchService; read by controllers and DashboardAggregatorService.
/// </summary>
public class ClusterQueueService
{
    private readonly IKubernetes _client;
    private readonly ILogger<ClusterQueueService> _logger;

    // Keyed by ClusterQueue name
    private readonly ConcurrentDictionary<string, ClusterQueueDto> _state = new();

    public ClusterQueueService(IKubernetes client, ILogger<ClusterQueueService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public IReadOnlyCollection<ClusterQueueDto> GetAll() => _state.Values.ToList();

    public void Clear() => _state.Clear();

    public void Apply(ClusterQueueDto dto) => _state[dto.Name] = dto;

    public void Remove(string name) => _state.TryRemove(name, out _);

    /// <summary>Loads all ClusterQueues from the API once (used on startup before watch begins).</summary>
    public async Task LoadInitialAsync()
    {
        Console.WriteLine("Url: " + _client.BaseUri);
        try
        {
            var response = await _client.CustomObjects.ListClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "clusterqueues");

            var json = JsonSerializer.Serialize(response);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items)) return;

            foreach (var item in items.EnumerateArray())
                Apply(ParseItem(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial ClusterQueues");
        }
    }

    internal static ClusterQueueDto ParseItem(JsonElement item)
    {
        var metadata = item.GetProperty("metadata");
        var name = KubeHelpers.GetString(metadata, "name");
        var age = KubeHelpers.ParseAge(metadata);

        string status = "Unknown";
        int pendingWorkloads = 0;
        int admittedWorkloads = 0;
        string nominalCpu = "";
        string nominalMemory = "";
        string usedCpu = "";
        string usedMemory = "";

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
                    {
                        status = "Active";
                    }
                }
            }

            if (st.TryGetProperty("flavorUsage", out var flavors) && flavors.ValueKind == JsonValueKind.Array)
            {
                foreach (var flavor in flavors.EnumerateArray())
                {
                    if (!flavor.TryGetProperty("resources", out var resources)) continue;
                    foreach (var res in resources.EnumerateArray())
                    {
                        var resName = KubeHelpers.GetString(res, "name");
                        var total = KubeHelpers.GetString(res, "nominalQuota");
                        var used = KubeHelpers.GetString(res, "usage");
                        if (resName == "cpu") { nominalCpu = total; usedCpu = used; }
                        if (resName == "memory") { nominalMemory = total; usedMemory = used; }
                    }
                }
            }
        }

        // Fallback: read nominalQuota from spec if status didn't have flavorUsage
        if (nominalCpu == "" && item.TryGetProperty("spec", out var spec)
            && spec.TryGetProperty("resourceGroups", out var rgs) && rgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var rg in rgs.EnumerateArray())
            {
                if (!rg.TryGetProperty("flavors", out var flavors)) continue;
                foreach (var flavor in flavors.EnumerateArray())
                {
                    if (!flavor.TryGetProperty("resources", out var resources)) continue;
                    foreach (var res in resources.EnumerateArray())
                    {
                        var resName = KubeHelpers.GetString(res, "name");
                        var quota = KubeHelpers.GetString(res, "nominalQuota");
                        if (resName == "cpu") nominalCpu = quota;
                        if (resName == "memory") nominalMemory = quota;
                    }
                }
            }
        }

        return new ClusterQueueDto
        {
            Name = name,
            Status = status,
            PendingWorkloads = pendingWorkloads,
            AdmittedWorkloads = admittedWorkloads,
            NominalCpu = nominalCpu,
            NominalMemory = nominalMemory,
            UsedCpu = usedCpu,
            UsedMemory = usedMemory,
            Age = age
        };
    }
}
