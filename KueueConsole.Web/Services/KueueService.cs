using k8s;
using System.Text.Json;
using KueueConsole.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KueueConsole.Web.Services;

public class KueueService
{
    private readonly IKubernetes _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KueueService> _logger;

    public KueueService(IKubernetes client, IMemoryCache cache, ILogger<KueueService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<WorkloadRow>> GetWorkloads()
    {
        try
        {
            var workloads = await _cache.GetOrCreateAsync<List<WorkloadRow>>("workloads", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);

                var response = await _client.CustomObjects.ListClusterCustomObjectAsync(
                    "kueue.x-k8s.io",
                    "v1beta1",
                    "workloads");

                var json = JsonSerializer.Serialize(response);
                using var doc = JsonDocument.Parse(json);

                var result = new List<WorkloadRow>();

                if (!doc.RootElement.TryGetProperty("items", out var items))
                    return result;

                foreach (var item in items.EnumerateArray())
                {
                    var metadata = item.GetProperty("metadata");

                    var name = metadata.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var ns = metadata.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() ?? "" : "";

                    string queue = "";
                    if (item.TryGetProperty("spec", out var spec) && spec.TryGetProperty("queue", out var q))
                        queue = q.GetString() ?? "";

                    string reservedIn = "";
                    bool admitted = false;
                    bool finished = false;

                    if (item.TryGetProperty("status", out var status))
                    {
                        if (status.TryGetProperty("reservedIn", out var r))
                            reservedIn = r.GetString() ?? "";

                        if (status.TryGetProperty("admitted", out var a) && a.ValueKind == JsonValueKind.True)
                            admitted = true;
                        if (status.TryGetProperty("finished", out var f) && f.ValueKind == JsonValueKind.True)
                            finished = true;

                        if (status.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var c in conds.EnumerateArray())
                            {
                                if (c.TryGetProperty("type", out var t) && t.GetString() == "Admitted" && c.TryGetProperty("status", out var s) && s.GetString() == "True")
                                    admitted = true;
                                if (c.TryGetProperty("type", out var t2) && t2.GetString() == "Finished" && c.TryGetProperty("status", out var s2) && s2.GetString() == "True")
                                    finished = true;
                            }
                        }
                    }

                    string age = "";
                    if (metadata.TryGetProperty("creationTimestamp", out var created))
                    {
                        if (DateTimeOffset.TryParse(created.GetString(), out var createdAt))
                        {
                            var span = DateTimeOffset.UtcNow - createdAt.ToUniversalTime();
                            if (span.TotalDays >= 1) age = ((int)span.TotalDays).ToString() + "d";
                            else if (span.TotalHours >= 1) age = ((int)span.TotalHours).ToString() + "h";
                            else if (span.TotalMinutes >= 1) age = ((int)span.TotalMinutes).ToString() + "m";
                            else age = ((int)span.TotalSeconds).ToString() + "s";
                        }
                    }

                    result.Add(new WorkloadRow
                    {
                        Name = name,
                        Namespace = ns,
                        Queue = queue,
                        ReservedIn = reservedIn,
                        Admitted = admitted,
                        Finished = finished,
                        Age = age
                    });
                }

                return result;
            });

            return workloads ?? new List<WorkloadRow>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list workloads");
            return new List<WorkloadRow>();
        }
    }

    public async Task<JsonElement?> GetWorkloadRawAsync(string ns, string name)
    {
        try
        {
            var obj = await _client.CustomObjects.GetNamespacedCustomObjectAsync(
                "kueue.x-k8s.io",
                "v1beta1",
                ns,
                "workloads",
                name);

            var json = JsonSerializer.Serialize(obj);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workload {namespace}/{name}", ns, name);
            return null;
        }
    }
}