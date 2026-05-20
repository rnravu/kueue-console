using k8s;
using System.Text.Json;
using KueueConsole.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KueueConsole.Web.Services;

public class LocalQueueService
{
    private readonly IKubernetes _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalQueueService> _logger;

    public LocalQueueService(IKubernetes client, IMemoryCache cache, ILogger<LocalQueueService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<LocalQueueRow>> GetLocalQueues()
    {
        try
        {
            var list = await _cache.GetOrCreateAsync<List<LocalQueueRow>>("localqueues", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);

                var response = await _client.CustomObjects.ListClusterCustomObjectAsync(
                    "kueue.x-k8s.io",
                    "v1beta1",
                    "localqueues");

                var json = JsonSerializer.Serialize(response);
                using var doc = JsonDocument.Parse(json);

                var result = new List<LocalQueueRow>();
                if (!doc.RootElement.TryGetProperty("items", out var items))
                    return result;

                foreach (var item in items.EnumerateArray())
                {
                    var metadata = item.GetProperty("metadata");
                    var name = metadata.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var ns = metadata.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() ?? "" : "";

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

                    result.Add(new LocalQueueRow
                    {
                        Name = name,
                        Namespace = ns,
                        Age = age
                    });
                }

                return result;
            });

            return list ?? new List<LocalQueueRow>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list localqueues");
            return new List<LocalQueueRow>();
        }
    }
}
