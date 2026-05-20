using k8s;
using System.Collections.Concurrent;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Holds the latest in-memory snapshot of Workloads.
/// Updated by WorkloadWatchService; read by controllers and DashboardAggregatorService.
/// </summary>
public class WorkloadStateService
{
    private readonly IKubernetes _client;
    private readonly ILogger<WorkloadStateService> _logger;

    // Key = "namespace/name"
    private readonly ConcurrentDictionary<string, WorkloadDto> _state = new();

    public WorkloadStateService(IKubernetes client, ILogger<WorkloadStateService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public IReadOnlyCollection<WorkloadDto> GetAll() => _state.Values.ToList();

    public void Clear() => _state.Clear();

    public IReadOnlyCollection<WorkloadDto> GetByNamespace(string ns) =>
        _state.Values.Where(w => w.Namespace == ns).ToList();

    public IReadOnlyCollection<WorkloadDto> GetByStatus(string status) =>
        _state.Values.Where(w => w.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

    public void Apply(WorkloadDto dto) => _state[$"{dto.Namespace}/{dto.Name}"] = dto;

    public void Remove(string ns, string name) => _state.TryRemove($"{ns}/{name}", out _);

    /// <summary>
    /// Lists all workloads, loads them into the store, and returns the
    /// resourceVersion of the list so the caller can start a watch from
    /// exactly that point — preventing stale ADDED replays from the API server.
    /// </summary>
    public async Task<string> LoadInitialAsync()
    {
        try
        {
            var response = await _client.CustomObjects.ListClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "workloads");

            var json = JsonSerializer.Serialize(response);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Capture the resourceVersion so the watch can start from here,
            // which prevents the API server replaying ADDED events for existing objects.
            var resourceVersion = root.TryGetProperty("metadata", out var meta)
                ? KubeHelpers.GetString(meta, "resourceVersion")
                : "";

            if (!root.TryGetProperty("items", out var items)) return resourceVersion;

            // ── Per-item exception handling ──────────────────────────────────
            // A single malformed workload must NOT abort the rest of the list.
            foreach (var item in items.EnumerateArray())
            {
                try   { Apply(ParseItem(item)); }
                catch (Exception ex)
                {
                    var itemName = item.TryGetProperty("metadata", out var m)
                        ? KubeHelpers.GetString(m, "name") : "(unknown)";
                    _logger.LogWarning(ex, "Skipped workload '{Name}' due to parse error", itemName);
                }
            }

            return resourceVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial Workloads");
            return "";
        }
    }

    internal static WorkloadDto ParseItem(JsonElement item)
    {
        var metadata = item.GetProperty("metadata");
        var name = KubeHelpers.GetString(metadata, "name");
        var ns = KubeHelpers.GetString(metadata, "namespace");
        var age = KubeHelpers.ParseAge(metadata);

        // Resolve the owning Kubernetes Job name from ownerReferences.
        // Kueue sets ownerReferences[0].name = the K8s Job that created this workload.
        var jobName = "";
        if (metadata.TryGetProperty("ownerReferences", out var ownerRefs)
            && ownerRefs.ValueKind == JsonValueKind.Array)
        {
            foreach (var or in ownerRefs.EnumerateArray())
            {
                var kind = KubeHelpers.GetString(or, "kind");
                if (kind.Equals("Job", StringComparison.OrdinalIgnoreCase))
                {
                    jobName = KubeHelpers.GetString(or, "name");
                    break;
                }
            }
        }
        // Fallback: Kueue workload names follow convention "job-{jobname}"
        if (jobName == "" && name.StartsWith("job-", StringComparison.OrdinalIgnoreCase))
            jobName = name[4..];

        DateTimeOffset? createdAt = null;
        if (metadata.TryGetProperty("creationTimestamp", out var ts)
            && DateTimeOffset.TryParse(ts.GetString(), out var parsed))
            createdAt = parsed;

        string queue = "";
        string clusterQueue = "";
        string message = "";
        var parsedConditions = new List<WorkloadCondition>();
        if (item.TryGetProperty("spec", out var spec))
        {
            queue = KubeHelpers.GetString(spec, "queueName");
            // older API used "queue"
            if (queue == "") queue = KubeHelpers.GetString(spec, "queue");
        }

        if (item.TryGetProperty("status", out var status))
        {
            clusterQueue = KubeHelpers.GetString(status, "admission") is { Length: > 0 } adm
                ? adm
                : "";

            // Try admission.clusterQueue sub-field
            if (status.TryGetProperty("admission", out var admEl) && admEl.ValueKind == JsonValueKind.Object)
                clusterQueue = KubeHelpers.GetString(admEl, "clusterQueue");

            // Extract message and full conditions list for diagnostics
            if (status.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in conds.EnumerateArray())
                {
                    var msg = KubeHelpers.GetString(c, "message");
                    if (msg != "") message = msg;
                    parsedConditions.Add(new WorkloadCondition
                    {
                        Type             = KubeHelpers.GetString(c, "type"),
                        ConditionStatus  = KubeHelpers.GetString(c, "status"),
                        Reason           = KubeHelpers.GetString(c, "reason"),
                        Message          = msg,
                        LastTransitionTime = KubeHelpers.GetString(c, "lastTransitionTime"),
                    });
                }
            }
        }

        var workloadStatus = KubeHelpers.DeriveWorkloadStatus(item);
        bool admitted = workloadStatus is "Running" or "Completed" or "Failed";
        bool finished = workloadStatus is "Completed" or "Failed";

        return new WorkloadDto
        {
            Name = name,
            Namespace = ns,
            Queue = queue,
            ClusterQueue = clusterQueue,
            Status = workloadStatus,
            Message = message,
            Age = age,
            CreatedAt = createdAt,
            Admitted = admitted,
            Finished = finished,
            Conditions = parsedConditions,
            JobName = jobName,
        };
    }
}
